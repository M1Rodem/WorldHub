using System.Runtime.InteropServices;

namespace WorldHub.Infrastructure.Windows;

internal sealed class WindowsConsoleService
{
    public void SendCommand(int processId, string command)
    {
        var hadConsole = GetConsoleWindow() != IntPtr.Zero;

        if (hadConsole)
        {
            FreeConsole();
        }

        try
        {
            if (!AttachConsole((uint)processId))
            {
                throw new InvalidOperationException(
                    "Failed to attach to server console.");
            }

            var consoleInput = CreateFile(
                "CONIN$",
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (consoleInput == InvalidHandleValue)
            {
                throw new InvalidOperationException(
                    "Failed to open console input.");
            }

            try
            {
                var records = new List<InputRecord>();

                foreach (var character in command)
                {
                    records.Add(CreateKey(character, true));
                    records.Add(CreateKey(character, false));
                }

                records.Add(CreateKey('\r', true));
                records.Add(CreateKey('\r', false));

                if (!WriteConsoleInput(
                    consoleInput,
                    records.ToArray(),
                    (uint)records.Count,
                    out _))
                {
                    throw new InvalidOperationException(
                        "Failed to write command to console.");
                }
            }
            finally
            {
                CloseHandle(consoleInput);
            }
        }
        finally
        {
            FreeConsole();

            if (hadConsole)
            {
                AttachConsole(0xFFFFFFFF);
            }
        }
    }

    private static InputRecord CreateKey(
        char character,
        bool keyDown)
    {
        return new InputRecord
        {
            EventType = KeyEvent,
            KeyEvent = new KeyEventRecord
            {
                KeyDown = keyDown ? 1 : 0,
                RepeatCount = 1,
                VirtualKeyCode = GetVirtualKeyCode(character),
                UnicodeChar = character
            }
        };
    }

    private static ushort GetVirtualKeyCode(char character)
    {
        return character switch
        {
            '\r' => 0x0D,
            ' ' => 0x20,
            >= 'a' and <= 'z' => (ushort)char.ToUpperInvariant(character),
            >= 'A' and <= 'Z' => character,
            _ => 0
        };
    }

    private const ushort KeyEvent = 0x0001;

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;

    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;

    private const uint OpenExisting = 3;

    private static readonly IntPtr InvalidHandleValue = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteConsoleInput(
        IntPtr consoleInput,
        [In] InputRecord[] buffer,
        uint length,
        out uint numberOfEventsWritten);

    [StructLayout(LayoutKind.Explicit, Size = 20)]
    private struct InputRecord
    {
        [FieldOffset(0)]
        public ushort EventType;

        [FieldOffset(4)]
        public KeyEventRecord KeyEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyEventRecord
    {
        public int KeyDown;
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public char UnicodeChar;
        public uint ControlKeyState;
    }
}