using VLIT;
using VLIT.Services;

var tempDir = Path.Combine(Path.GetTempPath(), "vlit-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);

try
{
    var syntheticLog = Path.Combine(tempDir, "output_log_2026-05-15_12-00-00.txt");
    File.WriteAllText(syntheticLog, """
2026.05.15 12:00:01 Debug      -  Boot line
2026.05.15 12:00:02 Warning    -  Something notable
2026.05.15 12:00:03 Error      -  Synthetic exception
  at Example.Type.Method () [0x00000] in <00000000000000000000000000000000>:0
2026.05.15 12:00:04 Debug      -  Recovery line
""");

    var item = new LogFileItem
    {
        FilePath = syntheticLog,
        SourceToken = "T1",
        Alias = "Synthetic",
        Color = Palette.At(0),
        IncludeInTimeline = true,
        IsVisible = true,
        StartTimestamp = LogParser.ParseStartTimestamp(syntheticLog),
        LastActivityTimestamp = File.GetLastWriteTime(syntheticLog),
        LengthBytes = new FileInfo(syntheticLog).Length
    };

    var entries = LogParser.ParseFile(item).ToList();
    Require(entries.Count == 4, $"Expected 4 grouped entries, got {entries.Count}.");
    Require(entries[2].Severity == "Error", "Expected third entry to be Error.");
    Require(entries[2].ContinuationText.Contains("Example.Type.Method", StringComparison.Ordinal), "Expected stack trace continuation to be grouped.");

    Console.WriteLine("Synthetic parser smoke test passed.");

    var checklist = ChecklistParser.Parse("""
# visible comment
all ordered: Startup
  action: Start client
  expect: /Boot line/
any(1-2): Optional warnings
  expect: Warning
  marker: Note optional branch
""");
    Require(checklist.Any(n => n.Type == ChecklistNodeType.Comment), "Expected comments to be parsed as checklist rows.");
    var ordered = checklist.First(n => n.Text == "Startup");
    Require(ordered.IsOrdered && ordered.RequiredMin == -1, "Expected 'all ordered' group spec.");
    var ranged = checklist.First(n => n.Text == "Optional warnings");
    Require(!ranged.IsOrdered && ranged.RequiredMin == 1 && ranged.RequiredMax == 2, "Expected 'any(1-2)' group spec.");

    Console.WriteLine("Checklist parser smoke test passed.");

    if (args.Length > 0 && Directory.Exists(args[0]))
    {
        var realLogs = Directory.EnumerateFiles(args[0], "output_log_*.txt")
            .OrderByDescending(File.GetLastWriteTime)
            .Take(3)
            .ToList();

        foreach (var path in realLogs)
        {
            var realItem = new LogFileItem
            {
                FilePath = path,
                SourceToken = "R1",
                Alias = "RealSample",
                Color = Palette.At(1),
                IncludeInTimeline = true,
                IsVisible = true,
                StartTimestamp = LogParser.ParseStartTimestamp(path),
                LastActivityTimestamp = File.GetLastWriteTime(path),
                LengthBytes = new FileInfo(path).Length
            };
            var parsed = LogParser.ParseFile(realItem);
            Console.WriteLine($"{Path.GetFileName(path)}: {parsed.Count:n0} entries, {parsed.Count(e => e.HasContinuation):n0} multiline");
        }
    }
}
finally
{
    Directory.Delete(tempDir, recursive: true);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
