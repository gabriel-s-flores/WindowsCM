// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Diagnostics;

// Append-only error log for crash diagnosis (the app used to die without a
// trace). Callers pass a short context label and the exception — never
// clipboard content. The file rotates to "<name>.1" past MaxBytes, and
// Write never throws: logging must not become the next crash.
public sealed class ErrorLog
{
    public const long DefaultMaxBytes = 512 * 1024;

    private readonly object _gate = new();
    private readonly Func<DateTime> _utcNow;

    public ErrorLog(string path, long maxBytes = DefaultMaxBytes, Func<DateTime>? utcNow = null)
    {
        Path = path;
        MaxBytes = maxBytes;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public string Path { get; }

    public long MaxBytes { get; }

    public static string DefaultPath() =>
        System.IO.Path.Combine(AppFolders.LogsDir(), "windowscm.log");

    public void Write(string context, Exception? exception = null) =>
        Append(() => Format(_utcNow(), context, exception));

    // An event worth keeping that is not an exception (a UI hang). The
    // detail follows the same rule: never clipboard content.
    public void Note(string context, string detail) =>
        Append(() => Format(_utcNow(), context, null, detail));

    private void Append(Func<string> format)
    {
        try
        {
            var entry = format();
            lock (_gate)
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                RotateIfNeeded();
                File.AppendAllText(Path, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Best effort by contract: a full disk or a locked file must not
            // turn a logged error into an unhandled one.
        }
    }

    public static string Format(DateTime utcNow, string context, Exception? exception, string? detail = null)
    {
        var builder = new StringBuilder();
        builder.Append(utcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture))
            .Append("Z [")
            .Append(context)
            .Append(']');
        if (!string.IsNullOrEmpty(detail))
        {
            builder.Append(' ').Append(detail);
        }
        if (exception is not null)
        {
            builder.Append(' ').Append(exception.GetType().FullName).Append(": ").Append(exception.Message);
            builder.AppendLine();
            builder.Append(exception.ToString());
        }
        builder.AppendLine();
        return builder.ToString();
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(Path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }
        File.Move(Path, Path + ".1", overwrite: true);
    }
}
