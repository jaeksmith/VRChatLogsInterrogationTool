using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace VLIT.Services;

public static partial class LogParser
{
    public const long SortStrideTicks = 1000L;

    private static readonly Regex FileTimestampRegex = new(
        @"output_log_(?<stamp>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})\.txt$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EntryRegex = new(
        @"^(?<stamp>\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2})\s+(?<severity>\S+)\s+-\s{0,2}(?<message>.*)$",
        RegexOptions.Compiled);

    public static DateTime ParseStartTimestamp(string filePath)
    {
        var match = FileTimestampRegex.Match(Path.GetFileName(filePath));
        if (match.Success &&
            DateTime.TryParseExact(match.Groups["stamp"].Value, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        try
        {
            return File.GetCreationTime(filePath);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    public static IReadOnlyList<TimelineEntry> ParseFile(LogFileItem file)
    {
        var entries = new List<TimelineEntry>(4096);
        if (!File.Exists(file.FilePath))
        {
            return entries;
        }

        using var stream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        PendingEntry? pending = null;
        var sequence = 0;
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var match = EntryRegex.Match(line);
            if (match.Success && TryParseEntryTimestamp(match.Groups["stamp"].Value, out var timestamp))
            {
                FlushPending(entries, file, pending, sequence++);
                pending = new PendingEntry
                {
                    Timestamp = timestamp,
                    LineNumber = lineNumber,
                    Severity = NormalizeSeverity(match.Groups["severity"].Value),
                    Message = match.Groups["message"].Value,
                    Continuation = new StringBuilder()
                };
                continue;
            }

            if (pending is null)
            {
                pending = new PendingEntry
                {
                    Timestamp = file.StartTimestamp == DateTime.MinValue ? DateTime.Now : file.StartTimestamp,
                    LineNumber = lineNumber,
                    Severity = "Info",
                    Message = line,
                    Continuation = new StringBuilder()
                };
            }
            else
            {
                if (pending.Continuation.Length > 0)
                {
                    pending.Continuation.AppendLine();
                }

                pending.Continuation.Append(line);
            }
        }

        FlushPending(entries, file, pending, sequence);
        return entries;
    }

    private static bool TryParseEntryTimestamp(string text, out DateTime timestamp)
    {
        return DateTime.TryParseExact(text, "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out timestamp);
    }

    private static string NormalizeSeverity(string severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return "Info";
        }

        var trimmed = severity.Trim();
        return trimmed.Equals("Warn", StringComparison.OrdinalIgnoreCase) ? "Warning" : trimmed;
    }

    private static void FlushPending(List<TimelineEntry> entries, LogFileItem file, PendingEntry? pending, int sequence)
    {
        if (pending is null)
        {
            return;
        }

        var sourceAlias = string.IsNullOrWhiteSpace(file.Alias) ? file.FileName : file.Alias;
        var key = $"{file.FileKey}:{pending.LineNumber}";
        entries.Add(new TimelineEntry
        {
            LineKey = key,
            SourceFileKey = file.FileKey,
            SourceToken = file.SourceToken,
            FileAlias = sourceAlias,
            FileName = file.FileName,
            Timestamp = pending.Timestamp,
            Sequence = sequence,
            LineNumber = pending.LineNumber,
            Severity = pending.Severity,
            Message = pending.Message,
            ContinuationText = pending.Continuation.ToString(),
            SourceColor = file.Color,
            SortTicks = pending.Timestamp.Ticks + (sequence * SortStrideTicks)
        });
    }

    private sealed class PendingEntry
    {
        public DateTime Timestamp { get; init; }
        public int LineNumber { get; init; }
        public string Severity { get; init; } = "Info";
        public string Message { get; init; } = string.Empty;
        public StringBuilder Continuation { get; init; } = new();
    }
}
