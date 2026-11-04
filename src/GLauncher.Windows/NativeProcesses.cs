using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using GLauncher.Core.Games;
using GLauncher.Core.Sessions;
using Microsoft.Win32.SafeHandles;

namespace GLauncher.Windows;

internal sealed class JobHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public JobHandle() : base(true) { }
    internal void Initialize(nint value) => SetHandle(value);
    protected override bool ReleaseHandle() => NativeProcesses.CloseHandle(handle);
}

internal static class NativeProcesses
{
    private const uint Query = 0x1000, Synchronize = 0x100000, Terminate = 1;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb; public string? reserved, desktop, title;
        public uint x, y, xSize, ySize, xChars, yChars, fill, flags;
        public short show, reserved2; public nint reservedPtr, stdInput, stdOutput, stdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public nint process, thread; public uint processId, threadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string application, StringBuilder commandLine, nint pa, nint ta,
        bool inherit, uint flags, nint environment, string directory, ref StartupInfo startup, out ProcessInfo info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern JobHandle CreateJobObjectW(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(JobHandle job, nint process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryInformationJobObject(JobHandle job, int infoClass, nint data, uint length, out uint returned);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsProcessInJob(SafeProcessHandle process, JobHandle job, out bool member);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint code);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);

    internal sealed record Started(ProcessIdentity Root, JobHandle? Job, string? Warning);

    internal static Started Start(LaunchTarget target)
    {
        string exe = Path.GetFullPath(target.ExecutablePath);
        if (!File.Exists(exe)) throw new FileNotFoundException("The executable was moved or removed.", exe);
        if (!string.Equals(Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a Windows .exe file. Emulator ROMs need an emulator executable and arguments.");
        string directory = string.IsNullOrWhiteSpace(target.WorkingDirectory) ? Path.GetDirectoryName(exe)! : Path.GetFullPath(target.WorkingDirectory);
        var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
        // Suspended until assigned: a bootstrap cannot spawn an unobserved child in this gap.
        // No shell, no inherited handles, no elevation, no KILL_ON_JOB_CLOSE or breakaway flags.
        var command = new StringBuilder('"' + exe + '"' + (string.IsNullOrWhiteSpace(target.Arguments) ? "" : " " + target.Arguments));
        using var jobCandidate = CreateJobObjectW(0, null);
        if (!CreateProcessW(exe, command, 0, 0, false, 0x4, 0, directory, ref startup, out var info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        JobHandle? ownedJob = null;
        string? warning = null;
        try
        {
            using var process = new SafeProcessHandle(info.process, ownsHandle: false);
            var identity = Identity(process, checked((int)info.processId))
                ?? new ProcessIdentity(checked((int)info.processId), DateTimeOffset.MinValue);
            if (!jobCandidate.IsInvalid && AssignProcessToJobObject(jobCandidate, info.process))
            {
                // Transfer the handle to the session, never enable kill-on-close.
                ownedJob = new JobHandle();
                TransferJob(jobCandidate, ownedJob);
            }
            else warning = "Job assignment failed; only the verified root can be tracked.";
            if (ResumeThread(info.thread) == uint.MaxValue)
                warning = "Windows could not resume the new process. Use the session menu to close it explicitly.";
            return new Started(identity, ownedJob, warning);
        }
        finally { CloseHandle(info.thread); CloseHandle(info.process); }
    }

    private static void TransferJob(JobHandle source, JobHandle destination)
    {
        destination.Initialize(source.DangerousGetHandle()); source.SetHandleAsInvalid();
    }

    internal static ProcessIdentity? Identity(SafeProcessHandle process, int pid)
        => !process.IsInvalid && GetProcessTimes(process, out long created, out _, out _, out _)
            ? new ProcessIdentity(pid, new DateTimeOffset(DateTime.FromFileTimeUtc(created))) : null;

    internal static ProcessIdentity? GetIdentity(int pid)
    {
        using var process = OpenProcess(Query | Synchronize, false, unchecked((uint)pid));
        return !process.IsInvalid && WaitForSingleObject(process, 0) == 258 ? Identity(process, pid) : null;
    }

    internal static List<ProcessIdentity> Members(JobHandle? job, ProcessIdentity root)
    {
        if (job is null || job.IsClosed || job.IsInvalid)
            return GetIdentity(root.ProcessId) is { } found && found == root ? [found] : [];
        int bytes = 4096;
        while (bytes <= 4 * 1024 * 1024)
        {
            nint buffer = Marshal.AllocHGlobal(bytes);
            try
            {
                if (!QueryInformationJobObject(job, 3, buffer, (uint)bytes, out _))
                {
                    if (Marshal.GetLastWin32Error() == 234) { bytes *= 2; continue; }
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                int count = Marshal.ReadInt32(buffer, 4);
                var members = new List<ProcessIdentity>();
                for (int i = 0; i < count; i++)
                {
                    int pid = checked((int)Marshal.ReadIntPtr(buffer, 8 + i * IntPtr.Size));
                    using var process = OpenProcess(Query | Synchronize, false, (uint)pid);
                    if (!process.IsInvalid && IsProcessInJob(process, job, out bool member) && member &&
                        WaitForSingleObject(process, 0) == 258 && Identity(process, pid) is { } identity)
                        members.Add(identity);
                }
                return members.OrderBy(p => p.ProcessId).ToList();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new InvalidOperationException("The session exceeds the supported job enumeration size.");
    }

    // A stable open handle binds validation and termination to the same kernel process.
    internal static bool TryTerminate(ProcessIdentity identity, JobHandle? job, ProcessIdentity root, out bool stale)
    {
        using var process = OpenProcess(Query | Synchronize | Terminate, false, (uint)identity.ProcessId);
        if (process.IsInvalid) { stale = Marshal.GetLastWin32Error() is 87 or 1168; return false; }
        stale = Identity(process, identity.ProcessId) != identity || WaitForSingleObject(process, 0) != 258;
        if (stale) return false;
        bool owned = job is not null
            ? IsProcessInJob(process, job, out bool member) && member : identity == root;
        if (!owned) { stale = true; return false; }
        if (!TerminateProcess(process, 1)) return false;
        return WaitForSingleObject(process, 3000) == 0;
    }
}
