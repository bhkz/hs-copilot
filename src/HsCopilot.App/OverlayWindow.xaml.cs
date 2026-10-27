using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace HsCopilot.App;

/// <summary>
/// Panneau de conseils toujours visible au-dessus du jeu (mode fenêtré ou fenêtré sans bordure). Il ne prend jamais le focus :
/// style WS_EX_NOACTIVATE, les clics sur ses boutons n'activent pas la fenêtre et Hearthstone garde le clavier.
/// Raccourcis globaux propres à l'application (RegisterHotKey) : Ctrl+Alt+H afficher/masquer, Ctrl+Alt+R réanalyser,
/// Ctrl+Alt+P pause. Aucune entrée n'est jamais envoyée au jeu.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_NOACTIVATE = 0x08000000L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_NOREPEAT = 0x4000;
    private const int HotkeyShowHide = 1, HotkeyReanalyze = 2, HotkeyPause = 3;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int value);

    private IntPtr _hwnd;
    private readonly List<int> _registered = new();

    public OverlayWindow(OverlayViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) =>
        {
            foreach (var id in _registered) UnregisterHotKey(_hwnd, id);
        };
    }

    public event Action? ReanalyzeRequested;
    public event Action? PauseToggleRequested;
    public event Action? CompactToggleRequested;
    /// <summary>Raccourci Ctrl+Alt+H ou bouton ✕.</summary>
    public event Action? VisibilityToggleRequested;
    public event Action<double, double>? Moved;

    /// <summary>Raccourcis refusés par Windows (déjà pris par une autre application).</summary>
    public List<string> HotkeyErrors { get; } = new();

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        if (IntPtr.Size == 8)
        {
            var ex = GetWindowLongPtr64(_hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr64(_hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
        }
        else
        {
            var ex = GetWindowLong32(_hwnd, GWL_EXSTYLE);
            SetWindowLong32(_hwnd, GWL_EXSTYLE, (int)(ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
        }
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        Register(HotkeyShowHide, 'H', "Ctrl+Alt+H", VK_F9, "Ctrl+Alt+F9");
        Register(HotkeyReanalyze, 'R', "Ctrl+Alt+R", VK_F10, "Ctrl+Alt+F10");
        Register(HotkeyPause, 'P', "Ctrl+Alt+P", VK_F11, "Ctrl+Alt+F11");
        LocationChanged += (_, _) => Moved?.Invoke(Left, Top);
    }

    /// <summary>Raccourci principal, sinon raccourci de secours si une autre application a déjà pris le premier.</summary>
    private void Register(int id, char key, string label, uint fallbackKey, string fallbackLabel)
    {
        if (RegisterHotKey(_hwnd, id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, key))
        {
            _registered.Add(id);
            ActiveHotkeys[id] = label;
        }
        else if (RegisterHotKey(_hwnd, id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, fallbackKey))
        {
            _registered.Add(id);
            ActiveHotkeys[id] = fallbackLabel;
            HotkeyFallbacks.Add($"{label} déjà pris : {fallbackLabel} à la place");
        }
        else HotkeyErrors.Add($"{label} et {fallbackLabel}");
    }

    private const uint VK_F9 = 0x78, VK_F10 = 0x79, VK_F11 = 0x7A;

    /// <summary>Raccourci effectivement actif pour chaque action (1 afficher/masquer, 2 réanalyser, 3 pause).</summary>
    public Dictionary<int, string> ActiveHotkeys { get; } = new();
    public List<string> HotkeyFallbacks { get; } = new();

    public string HotkeyFor(int id) => ActiveHotkeys.TryGetValue(id, out var k) ? k : "aucun raccourci";

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;
        switch (wParam.ToInt32())
        {
            case HotkeyShowHide: VisibilityToggleRequested?.Invoke(); handled = true; break;
            case HotkeyReanalyze: ReanalyzeRequested?.Invoke(); handled = true; break;
            case HotkeyPause: PauseToggleRequested?.Invoke(); handled = true; break;
        }
        return IntPtr.Zero;
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch (InvalidOperationException) { /* bouton relâché entre-temps */ }
        }
    }

    private void Reanalyze_Click(object sender, RoutedEventArgs e) => ReanalyzeRequested?.Invoke();
    private void Pause_Click(object sender, RoutedEventArgs e) => PauseToggleRequested?.Invoke();
    private void Compact_Click(object sender, RoutedEventArgs e) => CompactToggleRequested?.Invoke();
    private void Hide_Click(object sender, RoutedEventArgs e) => VisibilityToggleRequested?.Invoke();
}
