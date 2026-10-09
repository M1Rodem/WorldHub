using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WorldHub.Infrastructure.Windows;

internal static class NativeProcessLauncher
{
    private const uint CreateNewConsole = 0x00000010;

    public static Process StartNewConsole(
        string commandLine,
        string workingDirectory)
    {
        var startupInfo = new STARTUPINFO
        {
            cb = Marshal.SizeOf<STARTUPINFO>()
        };

        var processInfo = new PROCESS_INFORMATION();

        var mutableCommandLine = new StringBuilder(commandLine);

        var created = CreateProcess(
            null,
            mutableCommandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            CreateNewConsole,
            IntPtr.Zero,
            workingDirectory,
            ref startupInfo,
            out processInfo);

        if (!created)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to create Minecraft server console.");
        }

        CloseHandle(processInfo.hThread);

        try
        {
            return Process.GetProcessById(
                (int)processInfo.dwProcessId);
        }
        finally
        {
            CloseHandle(processInfo.hProcess);
        }
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(
        IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }
}