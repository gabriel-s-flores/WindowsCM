// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsCM.Core.Lifecycle.Win32;

// Production HKCU\...\Run store (research 02: unpackaged autostart) over
// Advapi32 P/Invoke so Core stays on BCL + Microsoft.Data.Sqlite with no new
// packages. Value "<exe>" --hidden under the WindowsCM name; reads return
// null when absent, Remove is idempotent. Windows-only: every method throws
// PlatformNotSupportedException elsewhere (tests use MemoryRunKeyStore and
// never touch the real hive; real writes are manual-smoke only).
public sealed class RegistryRunKeyStore : IRunKeyStore
{
    public const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;

    public RegistryRunKeyStore(string valueName = AutostartManager.RunValueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        _valueName = valueName;
    }

    public string ValueName => _valueName;

    public string? GetCommand()
    {
        EnsureWindows();
        var status = NativeRunKey.RegOpenKeyEx(
            NativeRunKey.HKEY_CURRENT_USER, RunSubKey, 0,
            NativeRunKey.KEY_READ, out var key);
        if (status == NativeRunKey.ERROR_FILE_NOT_FOUND)
        {
            return null;
        }
        if (status != NativeRunKey.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"Could not open the Run key (Win32 {status}).");
        }
        try
        {
            var capacity = 32767;
            var data = new StringBuilder(capacity);
            var bytes = capacity * 2;
            status = NativeRunKey.RegQueryValueEx(key, _valueName, IntPtr.Zero, out _, data, ref bytes);
            if (status == NativeRunKey.ERROR_FILE_NOT_FOUND)
            {
                return null;
            }
            if (status != NativeRunKey.ERROR_SUCCESS)
            {
                throw new InvalidOperationException($"Could not read the Run value (Win32 {status}).");
            }
            return data.ToString();
        }
        finally
        {
            NativeRunKey.RegCloseKey(key);
        }
    }

    public void SetCommand(string command)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var status = NativeRunKey.RegOpenKeyEx(
            NativeRunKey.HKEY_CURRENT_USER, RunSubKey, 0,
            NativeRunKey.KEY_WRITE, out var key);
        if (status != NativeRunKey.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"Could not open the Run key for write (Win32 {status}).");
        }
        try
        {
            status = NativeRunKey.RegSetValueEx(
                key, _valueName, 0, NativeRunKey.REG_SZ,
                command, checked((command.Length + 1) * 2));
            if (status != NativeRunKey.ERROR_SUCCESS)
            {
                throw new InvalidOperationException($"Could not write the Run value (Win32 {status}).");
            }
        }
        finally
        {
            NativeRunKey.RegCloseKey(key);
        }
    }

    public void Remove()
    {
        EnsureWindows();
        var status = NativeRunKey.RegOpenKeyEx(
            NativeRunKey.HKEY_CURRENT_USER, RunSubKey, 0,
            NativeRunKey.KEY_WRITE, out var key);
        if (status != NativeRunKey.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"Could not open the Run key for write (Win32 {status}).");
        }
        try
        {
            status = NativeRunKey.RegDeleteValue(key, _valueName);
            if (status is not (NativeRunKey.ERROR_SUCCESS or NativeRunKey.ERROR_FILE_NOT_FOUND))
            {
                throw new InvalidOperationException($"Could not delete the Run value (Win32 {status}).");
            }
        }
        finally
        {
            NativeRunKey.RegCloseKey(key);
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Run key requires Windows.");
        }
    }

    private static class NativeRunKey
    {
        public static readonly IntPtr HKEY_CURRENT_USER = new(unchecked((int)0x80000001));
        public const int ERROR_SUCCESS = 0;
        public const int ERROR_FILE_NOT_FOUND = 2;
        public const int KEY_READ = 0x20019;
        public const int KEY_WRITE = 0x20006;
        public const int REG_SZ = 1;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegOpenKeyEx(
            IntPtr hKey, string lpSubKey, int ulOptions, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegCloseKey(IntPtr hKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegQueryValueEx(
            IntPtr hKey, string lpValueName, IntPtr lpReserved,
            out int lpType, StringBuilder lpData, ref int lpcbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegSetValueEx(
            IntPtr hKey, string lpValueName, int reserved, int dwType,
            string lpData, int cbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegDeleteValue(IntPtr hKey, string lpValueName);
    }
}
