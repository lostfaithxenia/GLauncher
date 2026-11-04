using System.Runtime.InteropServices;
using GLauncher.Core.Input;
using GLauncher.Core.Sessions;

namespace GLauncher.Windows;

public static class DesktopInput
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    public static bool IsMinimized(nint window) => IsIconic(window);
    public static bool IsForeground(nint window) => GetForegroundWindow() == window && !IsIconic(window);
    public static SessionSnapshot? ForegroundSession(IReadOnlyList<SessionSnapshot> sessions)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        if (pid == 0) return null;
        var identity = NativeProcesses.GetIdentity(checked((int)pid));
        return identity is null ? null : sessions.FirstOrDefault(s => s.IsPossiblyAlive && s.TrackedProcesses.Contains(identity.Value));
    }
    public static bool DeleteHeld => (GetAsyncKeyState(0x2E) & 0x8000) != 0;
    public static bool ModifiersHeld => new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
    public static LauncherFocusFacts Facts(nint window, bool typing, IReadOnlyList<SessionSnapshot> sessions)
        => new(IsForeground(window), IsMinimized(window), typing, ForegroundSession(sessions) is not null, sessions.Count > 0);
}

public sealed record ControllerReading(bool Connected, int Slot, ushort Buttons, short LeftX, short LeftY)
{
    public string DisplayName => Connected ? $"Xbox-compatible controller · player {Slot + 1}" : "No XInput controller connected";
}

/// <summary>XInput baseline only. Does not claim native PlayStation/HID recognition.</summary>
public static class ControllerInput
{
    [StructLayout(LayoutKind.Sequential)] private struct Gamepad
    { public ushort buttons; public byte leftTrigger, rightTrigger; public short leftX, leftY, rightX, rightY; }
    [StructLayout(LayoutKind.Sequential)] private struct State { public uint packet; public Gamepad gamepad; }
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out State state);
    public static ControllerReading Read()
    {
        for (uint i = 0; i < 4; i++)
            if (XInputGetState(i, out var state) == 0) return new(true, (int)i, state.gamepad.buttons, state.gamepad.leftX, state.gamepad.leftY);
        return new(false, -1, 0, 0, 0);
    }
}
