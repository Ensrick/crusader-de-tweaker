// scripts/HiddenDesktopProcess.cs
//
// PURPOSE: Starts the game for scripts/test_headless.ps1 so that no window can reach the user's screen:
//   - on a private desktop created for the run (CreateDesktop, never switched to): any window the game
//     or a child process (a crash handler, a dialog) creates exists only there;
//   - suspended until it belongs to a job object with KILL_ON_JOB_CLOSE, so the whole process tree ends
//     with the runner (no orphan game keeps running) and no child starts before ownership is set; the job
//     also suppresses the Windows Error Reporting crash dialog (shown by a service, so not on the private desktop);
//   - at below-normal priority.
//   The runner also checks the user's desktop for windows of the job's processes while the game runs.
//
// Pattern: skyrim-mod-assistant/audit/QuietWorker.cs (private desktop + kill-on-close job), extended with
// a non-blocking handle (poll, window census, kill) for the runner's loop. Ported unchanged (desktop name aside)
// from CrusaderDEHandicap/scripts/HiddenDesktopProcess.cs (2026-10-14): a CDT run started from a detached shell
// showed the game window for about 25 s on 2026-10-11 (Start-Process -WindowStyle Hidden).
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public sealed class HiddenDesktopProcess : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Startup
    {
        public int cb;
        public string reserved, desktop, title;
        public uint x, y, width, height, xchars, ychars, fill, flags;
        public short show, reservedSize;
        public IntPtr reservedPointer, input, output, error;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr process, thread; public uint pid, tid; }
    [StructLayout(LayoutKind.Sequential)]
    struct BasicLimits
    {
        public long processTime, jobTime;
        public uint flags;
        public UIntPtr minWorkingSet, maxWorkingSet;
        public uint activeProcesses;
        public UIntPtr affinity;
        public uint priority, scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong readOps, writeOps, otherOps, readBytes, writeBytes, otherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    struct ExtendedLimits
    {
        public BasicLimits basic;
        public IoCounters io;
        public UIntPtr processMemory, jobMemory, peakProcessMemory, peakJobMemory;
    }
    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")] static extern bool EnumDesktopWindows(IntPtr desktop, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateJobObject(IntPtr security, string name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits limits, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool QueryInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint size, out uint returned);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string app, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
        bool inheritHandles, uint flags, IntPtr environment, string directory, ref Startup startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateJobObject(IntPtr job, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

    const uint CreateSuspended = 0x4, BelowNormalPriority = 0x4000, CreateNoWindow = 0x08000000;

    IntPtr _desktop, _job;
    ProcessInfo _process;
    public string DesktopName { get; private set; }
    public int Pid { get { return (int)_process.pid; } }

    public static HiddenDesktopProcess Start(string app, string arguments, string directory)
    {
        var p = new HiddenDesktopProcess();
        try { p.Launch(app, arguments, directory); }
        catch { p.Dispose(); throw; }
        return p;
    }

    void Launch(string app, string arguments, string directory)
    {
        DesktopName = "CdtSelfTest-" + Guid.NewGuid().ToString("N");
        _desktop = CreateDesktop(DesktopName, IntPtr.Zero, IntPtr.Zero, 0, 0xF01FF, IntPtr.Zero);
        if (_desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDesktop");
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObject");
        var limits = new ExtendedLimits();
        // KILL_ON_JOB_CLOSE (0x2000) | DIE_ON_UNHANDLED_EXCEPTION (0x400: SEM_NOGPFAULTERRORBOX for every process in
        // the job, so a crash ends the process instead of opening the Windows Error Reporting dialog, which the
        // WER service would show on the user's desktop, outside the private one).
        limits.basic.flags = 0x2000 | 0x400;
        if (!SetInformationJobObject(_job, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimits))))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject");
        var startup = new Startup { cb = Marshal.SizeOf(typeof(Startup)), desktop = "WinSta0\\" + DesktopName, flags = 1, show = 0 };
        if (!CreateProcess(app, new StringBuilder("\"" + app + "\" " + arguments), IntPtr.Zero, IntPtr.Zero, false,
                CreateSuspended | BelowNormalPriority | CreateNoWindow, IntPtr.Zero, directory, ref startup, out _process))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess");
        if (!AssignProcessToJobObject(_job, _process.process))
        {
            TerminateProcess(_process.process, 125);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "AssignProcessToJobObject");
        }
        if (ResumeThread(_process.thread) == uint.MaxValue)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ResumeThread");
    }

    /// <summary>True when the game process has exited (waits up to <paramref name="milliseconds"/>).</summary>
    public bool WaitForExit(uint milliseconds) { return WaitForSingleObject(_process.process, milliseconds) == 0; }

    public uint ExitCode
    {
        get { uint code; return GetExitCodeProcess(_process.process, out code) ? code : uint.MaxValue; }
    }

    /// <summary>Process ids currently in the job (the game and any child it started).</summary>
    public int[] JobPids()
    {
        const int max = 256;
        int size = 8 + IntPtr.Size * max;
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            uint returned;
            if (!QueryInformationJobObject(_job, 3, buffer, (uint)size, out returned)) return new int[0]; // JobObjectBasicProcessIdList
            int count = Marshal.ReadInt32(buffer, 4);
            var pids = new int[Math.Min(count, max)];
            for (int i = 0; i < pids.Length; i++) pids[i] = (int)Marshal.ReadIntPtr(buffer, 8 + i * IntPtr.Size).ToInt64();
            return pids;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    /// <summary>Visible top-level windows of the job's processes on the desktop this thread uses (the user's).</summary>
    public string[] WindowsOnUserDesktop()
    {
        var pids = new HashSet<uint>();
        foreach (int pid in JobPids()) pids.Add((uint)pid);
        var found = new List<string>();
        EnumWindows((hwnd, l) =>
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pids.Contains(pid) && IsWindowVisible(hwnd)) found.Add(Describe(hwnd, pid));
            return true;
        }, IntPtr.Zero);
        return found.ToArray();
    }

    /// <summary>Top-level windows on the private desktop (visible flag included): what the game created there.</summary>
    public string[] WindowsOnPrivateDesktop()
    {
        var found = new List<string>();
        EnumDesktopWindows(_desktop, (hwnd, l) =>
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            found.Add(Describe(hwnd, pid) + (IsWindowVisible(hwnd) ? " visible" : " hidden"));
            return true;
        }, IntPtr.Zero);
        return found.ToArray();
    }

    static string Describe(IntPtr hwnd, uint pid)
    {
        var title = new StringBuilder(256);
        var cls = new StringBuilder(256);
        GetWindowText(hwnd, title, 256);
        GetClassName(hwnd, cls, 256);
        return "pid " + pid + " class '" + cls + "' title '" + title + "'";
    }

    /// <summary>Ends the game and every child (the job), for a timeout or a window on the user's desktop.</summary>
    public void Kill()
    {
        if (_job != IntPtr.Zero) TerminateJobObject(_job, 124);
        WaitForSingleObject(_process.process, 10000);
    }

    public void Dispose()
    {
        // Closing the job ends anything still running in it (KILL_ON_JOB_CLOSE).
        if (_job != IntPtr.Zero) { CloseHandle(_job); _job = IntPtr.Zero; }
        if (_process.thread != IntPtr.Zero) { CloseHandle(_process.thread); _process.thread = IntPtr.Zero; }
        if (_process.process != IntPtr.Zero) { CloseHandle(_process.process); _process.process = IntPtr.Zero; }
        if (_desktop != IntPtr.Zero) { CloseDesktop(_desktop); _desktop = IntPtr.Zero; }
    }
}
