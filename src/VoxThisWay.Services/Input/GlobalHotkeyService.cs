using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using VoxThisWay.Core.Abstractions.Input;
using VoxThisWay.Core.Configuration;

namespace VoxThisWay.Services.Input;

public sealed class GlobalHotkeyService : IHotkeyService
{
    private readonly ILogger<GlobalHotkeyService> _logger;
    private readonly IUserSettingsStore _settingsStore;
    private IntPtr _hookHandle;
    private HookProc? _hookCallback;
    private bool _disposed;
    private int _isActive;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_SPACE = 0x20;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_SHIFT = 0x10;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private int _activationVk = VK_SPACE;
    private bool _requireCtrl = true;
    private bool _requireAlt;
    private bool _requireShift;
    private bool _requireWin;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger, IUserSettingsStore settingsStore)
    {
        _logger = logger;
        _settingsStore = settingsStore;
    }

    public bool IsActive => Interlocked.CompareExchange(ref _isActive, 0, 0) == 1;

    public event EventHandler? PushToTalkStarted;

    public event EventHandler? PushToTalkEnded;

    public void Start()
    {
        ThrowIfDisposed();
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        LoadHotkeyConfiguration();

        _hookCallback = HookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = GetModuleHandle(curModule?.ModuleName ?? string.Empty);
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallback, moduleHandle, 0);

        if (_hookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogError("Failed to install keyboard hook. Win32 error: {Error}", error);
            throw new InvalidOperationException("Failed to install keyboard hook.");
        }

        _logger.LogInformation("Global hotkey service started ({Hotkey} push-to-talk).", FormatHotkey());
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (!UnhookWindowsHookEx(_hookHandle))
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogWarning("Failed to remove keyboard hook. Win32 error: {Error}", error);
        }

        _hookHandle = IntPtr.Zero;
        _hookCallback = null;
        Interlocked.Exchange(ref _isActive, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            var keyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            var ctrlDown = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            var altDown = (GetKeyState(VK_MENU) & 0x8000) != 0;
            var shiftDown = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
            var winDown = (GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0;

            var isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            var isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            var modifiersMatch = (!_requireCtrl || ctrlDown)
                                 && (!_requireAlt || altDown)
                                 && (!_requireShift || shiftDown)
                                 && (!_requireWin || winDown);

            if (isKeyDown && modifiersMatch && keyInfo.vkCode == _activationVk)
            {
                if (Interlocked.Exchange(ref _isActive, 1) == 0)
                {
                    PushToTalkStarted?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (isKeyUp &&
                     (keyInfo.vkCode == _activationVk
                      || (_requireCtrl && keyInfo.vkCode == VK_CONTROL)
                      || (_requireAlt && keyInfo.vkCode == VK_MENU)
                      || (_requireShift && keyInfo.vkCode == VK_SHIFT)
                      || (_requireWin && (keyInfo.vkCode == VK_LWIN || keyInfo.vkCode == VK_RWIN))))
            {
                if (Interlocked.Exchange(ref _isActive, 0) == 1)
                {
                    PushToTalkEnded?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GlobalHotkeyService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private void LoadHotkeyConfiguration()
    {
        var settings = _settingsStore.Current;
        var vk = settings.HotkeyVirtualKey;
        _activationVk = vk != 0 ? vk : VK_SPACE;
        _requireCtrl = settings.HotkeyUseCtrl;
        _requireAlt = settings.HotkeyUseAlt;
        _requireShift = settings.HotkeyUseShift;
        _requireWin = settings.HotkeyUseWin;
    }

    private string FormatHotkey()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (_requireCtrl) parts.Add("Ctrl");
        if (_requireAlt) parts.Add("Alt");
        if (_requireShift) parts.Add("Shift");
        if (_requireWin) parts.Add("Win");

        parts.Add($"0x{_activationVk:X2}");
        return string.Join("+", parts);
    }
}
