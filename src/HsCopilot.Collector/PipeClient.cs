using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using HsCopilot.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HsCopilot.Collector
{
    /// <summary>
    /// Client named pipe : transmet les lignes GameState et les annotations HDT à l'application compagnon.
    /// </summary>
    /// <remarks>
    /// Ne bloque jamais HDT : les gestionnaires d'événements ne font qu'ajouter à une file ; un fil dédié se connecte,
    /// renvoie la partie en cours (tampon) à chaque connexion, puis transmet le flux. Pas de connexion réseau :
    /// le pipe est local ("." = cette machine).
    /// </remarks>
    internal sealed class PipeClient : IDisposable
    {
        private const int MaxQueuedLines = 200000;
        private const int MaxBacklogLines = 300000;
        private const int BatchSize = 400;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        private readonly object _lock = new object();
        private readonly List<KeyValuePair<long, string>> _backlog = new List<KeyValuePair<long, string>>();
        private readonly BlockingCollection<KeyValuePair<long, string>> _lines =
            new BlockingCollection<KeyValuePair<long, string>>(new ConcurrentQueue<KeyValuePair<long, string>>(), MaxQueuedLines);
        private readonly ConcurrentQueue<PipeMessage> _messages = new ConcurrentQueue<PipeMessage>();
        private readonly string _hdtVersion;
        private readonly string _pluginVersion;
        private Thread _thread;
        private volatile bool _running;
        private volatile bool _connected;
        private long _seq;
        private long _msgSeq;
        private long _dropped;
        private long _linesInGame;
        private PipeMessage _lastDeck;

        public PipeClient(string hdtVersion, string pluginVersion)
        {
            _hdtVersion = hdtVersion;
            _pluginVersion = pluginVersion;
        }

        public bool IsConnected => _connected;
        public long LinesForwarded => Interlocked.Read(ref _linesInGame);
        public long Dropped => Interlocked.Read(ref _dropped);
        public string LastError { get; private set; }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "HsCopilot.Collector" };
            _thread.Start();
        }

        /// <summary>Appelé par HDT pour chaque ligne GameState (fil de lecture des logs HDT).</summary>
        public void OnLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            lock (_lock)
            {
                var seq = ++_seq;
                if (line.IndexOf("GameState.DebugPrintPower() - CREATE_GAME", StringComparison.Ordinal) >= 0)
                {
                    _backlog.Clear();
                    Interlocked.Exchange(ref _linesInGame, 0);
                }
                if (_backlog.Count < MaxBacklogLines) _backlog.Add(new KeyValuePair<long, string>(seq, line));
                Interlocked.Increment(ref _linesInGame);
                if (_connected && !_lines.TryAdd(new KeyValuePair<long, string>(seq, line)))
                    Interlocked.Increment(ref _dropped);
            }
        }

        public void Send(PipeMessage message)
        {
            if (message.Type == PipeProtocol.TypeDeck) _lastDeck = message;
            if (_connected) _messages.Enqueue(message);
        }

        private void Run()
        {
            while (_running)
            {
                NamedPipeClientStream pipe = null;
                try
                {
                    pipe = new NamedPipeClientStream(".", PipeProtocol.PipeName, PipeDirection.Out, PipeOptions.None);
                    pipe.Connect(1000);
                    using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1 << 16))
                    {
                        writer.AutoFlush = false;
                        long sentUpTo;
                        List<KeyValuePair<long, string>> snapshot;
                        lock (_lock)
                        {
                            _connected = true;
                            snapshot = new List<KeyValuePair<long, string>>(_backlog);
                            sentUpTo = _seq;
                            while (_lines.TryTake(out _)) { }
                        }
                        Write(writer, new PipeMessage
                        {
                            Type = PipeProtocol.TypeHello,
                            Protocol = PipeProtocol.Version,
                            Source = "hdt-plugin",
                            HdtVersion = _hdtVersion,
                            PluginVersion = _pluginVersion,
                        });
                        if (_lastDeck != null) Write(writer, _lastDeck);
                        for (int i = 0; i < snapshot.Count; i += BatchSize)
                        {
                            var batch = new List<string>(Math.Min(BatchSize, snapshot.Count - i));
                            for (int j = i; j < Math.Min(i + BatchSize, snapshot.Count); j++) batch.Add(snapshot[j].Value);
                            Write(writer, new PipeMessage { Type = PipeProtocol.TypeLines, Lines = batch, Backlog = true });
                        }
                        writer.Flush();
                        LastError = null;

                        var lastHeartbeat = DateTime.UtcNow;
                        var reportedDrops = 0L;
                        while (_running && pipe.IsConnected)
                        {
                            var batch = new List<string>();
                            KeyValuePair<long, string> item;
                            if (_lines.TryTake(out item, 50))
                            {
                                if (item.Key > sentUpTo) batch.Add(item.Value);
                                while (batch.Count < BatchSize && _lines.TryTake(out item))
                                    if (item.Key > sentUpTo) batch.Add(item.Value);
                            }
                            if (batch.Count > 0) Write(writer, new PipeMessage { Type = PipeProtocol.TypeLines, Lines = batch });
                            PipeMessage msg;
                            while (_messages.TryDequeue(out msg)) Write(writer, msg);
                            var dropped = Dropped;
                            if (dropped != reportedDrops)
                            {
                                Write(writer, new PipeMessage { Type = PipeProtocol.TypeGap, Dropped = dropped - reportedDrops });
                                reportedDrops = dropped;
                            }
                            if (DateTime.UtcNow - lastHeartbeat > TimeSpan.FromSeconds(2))
                            {
                                Write(writer, new PipeMessage { Type = PipeProtocol.TypeHeartbeat });
                                lastHeartbeat = DateTime.UtcNow;
                            }
                            writer.Flush();
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // Application compagnon non lancée : nouvel essai.
                }
                catch (IOException ex)
                {
                    LastError = ex.Message;
                }
                catch (Exception ex)
                {
                    LastError = ex.GetType().Name + ": " + ex.Message;
                }
                finally
                {
                    _connected = false;
                    if (pipe != null) pipe.Dispose();
                }
                if (_running) Thread.Sleep(1000);
            }
        }

        private void Write(StreamWriter writer, PipeMessage message)
        {
            message.Seq = Interlocked.Increment(ref _msgSeq);
            message.SentAt = DateTime.UtcNow.ToString("o");
            writer.Write(JsonConvert.SerializeObject(message, JsonSettings));
            writer.Write('\n');
        }

        public void Dispose()
        {
            _running = false;
            if (_thread != null && !_thread.Join(2000)) { /* le fil est en arrière-plan : il s'arrêtera avec HDT */ }
        }
    }
}
