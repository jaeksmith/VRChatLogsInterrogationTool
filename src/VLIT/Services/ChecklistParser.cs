using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace VLIT.Services;

public static class ChecklistParser
{
    public static ObservableCollection<ChecklistNode> Parse(string text)
    {
        var root = new ChecklistNode
        {
            Id = "root",
            Type = ChecklistNodeType.Root,
            Text = "Checklist",
            Indent = -1
        };
        var stack = new Stack<ChecklistNode>();
        stack.Push(root);

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var indent = CountIndent(rawLine);
            var trimmedRaw = rawLine.Trim();
            ChecklistNode? node;
            if (trimmedRaw.StartsWith("#", StringComparison.Ordinal))
            {
                node = new ChecklistNode
                {
                    Type = ChecklistNodeType.Comment,
                    Text = trimmedRaw[1..].TrimStart(),
                    Indent = indent
                };
            }
            else
            {
                var commentIndex = rawLine.IndexOf('#');
                var uncommented = commentIndex >= 0 ? rawLine[..commentIndex] : rawLine;
                if (string.IsNullOrWhiteSpace(uncommented))
                {
                    continue;
                }

                var line = uncommented.Trim();
                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    line = line[2..].TrimStart();
                }

                node = ParseNode(line, indent);
            }

            if (node is null)
            {
                continue;
            }

            while (stack.Count > 1 && stack.Peek().Indent >= indent)
            {
                stack.Pop();
            }

            var parent = stack.Peek();
            node.ParentId = parent.Id;
            parent.Children.Add(node);
            if (node.Type is ChecklistNodeType.OrderedGroup or ChecklistNodeType.UnorderedGroup)
            {
                stack.Push(node);
            }
        }

        var flat = new ObservableCollection<ChecklistNode>();
        Flatten(root, flat);
        return flat;
    }

    public static IReadOnlyList<ChecklistNode> Roots(ObservableCollection<ChecklistNode> flat)
    {
        return flat.Where(n => n.ParentId == "root").ToList();
    }

    private static ChecklistNode? ParseNode(string line, int indent)
    {
        var split = line.Split(':', 2);
        if (split.Length != 2)
        {
            return new ChecklistNode
            {
                Type = ChecklistNodeType.Action,
                Text = line,
                Indent = indent
            };
        }

        var keyword = split[0].Trim().ToLowerInvariant();
        var body = split[1].Trim();
        var marker = ExtractMarker(ref body);

        if (TryParseGroupSpec(keyword, out var isOrdered, out var requiredMin, out var requiredMax, out var specText))
        {
            return new ChecklistNode
            {
                Type = isOrdered ? ChecklistNodeType.OrderedGroup : ChecklistNodeType.UnorderedGroup,
                Text = EmptyAs(body, specText),
                Indent = indent,
                InsertMarker = marker,
                IsOrdered = isOrdered,
                RequiredMin = requiredMin,
                RequiredMax = requiredMax,
                GroupSpecText = specText
            };
        }

        return keyword switch
        {
            "action" => new ChecklistNode
            {
                Type = ChecklistNodeType.Action,
                Text = EmptyAs(body, "Action"),
                Indent = indent,
                InsertMarker = marker
            },
            "expect" => new ChecklistNode
            {
                Type = ChecklistNodeType.Expect,
                Text = EmptyAs(ReadableRegexLabel(body), "Expected log"),
                Pattern = TrimRegexDelimiters(body),
                Indent = indent,
                InsertMarker = marker
            },
            "marker" => new ChecklistNode
            {
                Type = ChecklistNodeType.Marker,
                Text = EmptyAs(body, "Marker"),
                InsertMarker = body,
                Indent = indent
            },
            "title" => null,
            _ => new ChecklistNode
            {
                Type = ChecklistNodeType.Action,
                Text = line,
                Indent = indent,
                InsertMarker = marker
            }
        };
    }

    private static bool TryParseGroupSpec(string keyword, out bool isOrdered, out int requiredMin, out int? requiredMax, out string specText)
    {
        isOrdered = false;
        requiredMin = -1;
        requiredMax = -1;
        specText = "ALL";

        var tokens = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var sawNumber = false;
        var sawOrdering = false;
        foreach (var token in tokens)
        {
            if (token.Equals("ordered", StringComparison.OrdinalIgnoreCase))
            {
                isOrdered = true;
                sawOrdering = true;
                continue;
            }

            if (token.Equals("unordered", StringComparison.OrdinalIgnoreCase))
            {
                isOrdered = false;
                sawOrdering = true;
                continue;
            }

            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                requiredMin = -1;
                requiredMax = -1;
                sawNumber = true;
                continue;
            }

            if (token.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                requiredMin = 1;
                requiredMax = null;
                sawNumber = true;
                continue;
            }

            if (token.StartsWith("any(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(')'))
            {
                ParseAnyCount(token[4..^1], out requiredMin, out requiredMax);
                sawNumber = true;
                continue;
            }

            return false;
        }

        if (!sawNumber && !sawOrdering)
        {
            return false;
        }

        var numberText = requiredMin < 0
            ? "ALL"
            : requiredMax is null
                ? requiredMin == 0 ? "ANY*" : "ANY+"
                : requiredMin == requiredMax ? $"ANY{requiredMin}" : $"ANY{requiredMin}-{requiredMax}";
        specText = isOrdered ? $"{numberText} ORDER" : numberText;
        return true;
    }

    private static void ParseAnyCount(string count, out int requiredMin, out int? requiredMax)
    {
        count = string.IsNullOrWhiteSpace(count) ? "+" : count.Trim();
        switch (count)
        {
            case "*":
                requiredMin = 0;
                requiredMax = null;
                return;
            case "+":
                requiredMin = 1;
                requiredMax = null;
                return;
        }

        var range = count.Split('-', 2, StringSplitOptions.TrimEntries);
        if (range.Length == 2 &&
            int.TryParse(range[0], out var min) &&
            int.TryParse(range[1], out var max))
        {
            requiredMin = Math.Max(0, min);
            requiredMax = Math.Max(requiredMin, max);
            return;
        }

        if (int.TryParse(count, out var exact))
        {
            requiredMin = Math.Max(0, exact);
            requiredMax = requiredMin;
            return;
        }

        requiredMin = 1;
        requiredMax = null;
    }

    private static string ExtractMarker(ref string body)
    {
        var parts = body.Split("=>", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return string.Empty;
        }

        body = parts[0].Trim();
        var marker = parts[1].Trim();
        if (marker.StartsWith("marker:", StringComparison.OrdinalIgnoreCase))
        {
            marker = marker["marker:".Length..].Trim();
        }

        return marker;
    }

    private static string EmptyAs(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string ReadableRegexLabel(string pattern)
    {
        var trimmed = TrimRegexDelimiters(pattern);
        return trimmed.Length > 80 ? trimmed[..80] + "..." : trimmed;
    }

    private static string TrimRegexDelimiters(string pattern)
    {
        var trimmed = pattern.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '/' && trimmed.LastIndexOf('/') > 0)
        {
            var last = trimmed.LastIndexOf('/');
            return trimmed[1..last];
        }

        return trimmed.Trim('"');
    }

    private static int CountIndent(string line)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
            {
                count++;
            }
            else if (ch == '\t')
            {
                count += 2;
            }
            else
            {
                break;
            }
        }

        return count / 2;
    }

    private static void Flatten(ChecklistNode node, ObservableCollection<ChecklistNode> flat)
    {
        foreach (var child in node.Children)
        {
            flat.Add(child);
            Flatten(child, flat);
        }
    }

    public static bool TryCompile(ChecklistNode node, out Regex? regex)
    {
        regex = null;
        if (node.Type != ChecklistNodeType.Expect || string.IsNullOrWhiteSpace(node.Pattern))
        {
            return false;
        }

        try
        {
            regex = new Regex(node.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return true;
        }
        catch
        {
            node.StatusText = "Invalid regex";
            return false;
        }
    }
}
