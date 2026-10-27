using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using HsCopilot.Shared;

namespace HsCopilot.Core.Transport;

/// <summary>
/// Serveur named pipe local qui reçoit les messages NDJSON du plugin HDT.
/// </summary>
/// <remarks>
/// Sécurité : le canal n'est accessible qu'en local. Le pipe est créé directement par CreateNamedPipe avec le drapeau
/// PIPE_REJECT_REMOTE_CLIENTS (vérifié : sans lui, une connexion via \\127.0.0.1\pipe ou \\NOM-PC\pipe aboutit),
/// et l'ACL n'autorise que l'utilisateur courant et refuse le groupe NETWORK (S-1-5-2).
/// Une seule connexion à la fois (un seul HDT).
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class PipeServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public PipeServer(string? pipeName = null) => _pipeName = pipeName ?? PipeProtocol.PipeName;

    public event Action<PipeMessage>? MessageReceived;
    public event Action<bool>? ConnectionChanged;
    public event Action<string>? Status;

    public bool IsConnected { get; private set; }
    public long MessagesReceived { get; private set; }
    public long MalformedMessages { get; private set; }
    public string PipeName => _pipeName;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    private static PipeSecurity CreateSecurity()
    {
        var security = new PipeSecurity();
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Utilisateur Windows inconnu.");
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }

    // --- Création du pipe avec PIPE_REJECT_REMOTE_CLIENTS -----------------------------------------------

    private const uint PipeAccessInbound = 0x00000001;
    private const uint FileFlagFirstPipeInstance = 0x00080000;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint PipeTypeByte = 0x00000000;
    private const uint PipeRejectRemoteClients = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafePipeHandle CreateNamedPipe(string name, uint openMode, uint pipeMode, uint maxInstances,
        uint outBufferSize, uint inBufferSize, uint defaultTimeout, ref SecurityAttributes securityAttributes);

    /// <summary>Crée l'instance serveur : entrée seule, première instance, asynchrone, clients distants refusés.</summary>
    public static NamedPipeServerStream CreateLocalOnlyServer(string pipeName, PipeSecurity security)
    {
        var descriptor = security.GetSecurityDescriptorBinaryForm();
        var pinned = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
        try
        {
            var sa = new SecurityAttributes
            {
                nLength = Marshal.SizeOf<SecurityAttributes>(),
                lpSecurityDescriptor = pinned.AddrOfPinnedObject(),
                bInheritHandle = 0,
            };
            var handle = CreateNamedPipe(@"\\.\pipe\" + pipeName,
                PipeAccessInbound | FileFlagFirstPipeInstance | FileFlagOverlapped,
                PipeTypeByte | PipeRejectRemoteClients,
                1, 1 << 16, 1 << 16, 0, ref sa);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw error == 5 /* ERROR_ACCESS_DENIED : instance déjà ouverte */
                    ? new UnauthorizedAccessException($"Le pipe {pipeName} est déjà utilisé (erreur Windows {error}).")
                    : new IOException($"CreateNamedPipe a échoué (erreur Windows {error}).");
            }
            return new NamedPipeServerStream(PipeDirection.In, isAsync: true, isConnected: false, handle);
        }
        finally
        {
            pinned.Free();
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = CreateLocalOnlyServer(_pipeName, CreateSecurity());
                Status?.Invoke($"En attente du plugin HDT sur \\\\.\\pipe\\{_pipeName}");
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                IsConnected = true;
                ConnectionChanged?.Invoke(true);
                Status?.Invoke("Plugin HDT connecté");
                using var reader = new StreamReader(server, new UTF8Encoding(false), false, 1 << 16, leaveOpen: true);
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line is null) break;
                    if (line.Length == 0) continue;
                    PipeMessage? msg;
                    try
                    {
                        msg = JsonSerializer.Deserialize<PipeMessage>(line, JsonOptions);
                    }
                    catch (JsonException)
                    {
                        MalformedMessages++;
                        continue;
                    }
                    if (msg is null) continue;
                    MessagesReceived++;
                    MessageReceived?.Invoke(msg);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                Status?.Invoke($"Pipe : {ex.Message}");
                await Task.Delay(1000, ct).ContinueWith(_ => { }).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                Status?.Invoke($"Pipe indisponible (une autre instance est-elle ouverte ?) : {ex.Message}");
                await Task.Delay(3000, ct).ContinueWith(_ => { }).ConfigureAwait(false);
            }
            finally
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    ConnectionChanged?.Invoke(false);
                    Status?.Invoke("Plugin HDT déconnecté");
                }
                server?.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { /* arrêt */ }
        }
        _cts.Dispose();
    }
}
