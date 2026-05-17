using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace VLIT;

public static class Palette
{
    public static readonly string[] Colors =
    [
        "#4CC9F0",
        "#F72585",
        "#B8F25C",
        "#FFD166",
        "#9B5DE5",
        "#00F5D4",
        "#F15BB5",
        "#90BE6D",
        "#F9844A",
        "#43AA8B",
        "#577590",
        "#F94144"
    ];

    public static string At(int index) => Colors[Math.Abs(index) % Colors.Length];

    public static SolidColorBrush Brush(string color)
    {
        try
        {
            var converted = ColorConverter.ConvertFromString(color);
            if (converted is Color parsed)
            {
                var brush = new SolidColorBrush(parsed);
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // Fall back below.
        }

        var fallback = new SolidColorBrush(Color.FromRgb(120, 150, 180));
        fallback.Freeze();
        return fallback;
    }
}

public sealed class SourceDirectoryItem : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _token = "S1";
    private string _color = Palette.At(0);
    private string _directoryPath = string.Empty;
    private bool _isAvailable;
    private string _statusText = "Not checked";

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Token
    {
        get => _token;
        set => SetProperty(ref _token, value);
    }

    public string Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    public string DirectoryPath
    {
        get => _directoryPath;
        set => SetProperty(ref _directoryPath, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    [JsonIgnore]
    public SolidColorBrush ColorBrush => Palette.Brush(Color);

    [JsonIgnore]
    public SolidColorBrush StatusBrush => IsAvailable ? Palette.Brush("#64D68A") : Palette.Brush("#E76F51");
}

public sealed class LogFileItem : ObservableObject
{
    private string _filePath = string.Empty;
    private string _sourceId = string.Empty;
    private string _sourceToken = string.Empty;
    private string _sourceColor = Palette.At(0);
    private string _alias = string.Empty;
    private string _color = Palette.At(0);
    private bool _includeInTimeline;
    private bool _isVisible = true;
    private bool _isAvailable = true;
    private DateTime _startTimestamp;
    private DateTime _lastActivityTimestamp;
    private long _lengthBytes;
    private int _entryCount;

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(FileKey));
            }
        }
    }

    public string SourceId
    {
        get => _sourceId;
        set => SetProperty(ref _sourceId, value);
    }

    public string SourceToken
    {
        get => _sourceToken;
        set
        {
            if (SetProperty(ref _sourceToken, value))
            {
                OnPropertyChanged(nameof(MetadataText));
            }
        }
    }

    public string SourceColor
    {
        get => _sourceColor;
        set => SetProperty(ref _sourceColor, value);
    }

    public string Alias
    {
        get => _alias;
        set
        {
            if (SetProperty(ref _alias, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    public bool IncludeInTimeline
    {
        get => _includeInTimeline;
        set
        {
            if (SetProperty(ref _includeInTimeline, value))
            {
                IsVisible = value;
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(MetadataText));
            }
        }
    }

    public DateTime StartTimestamp
    {
        get => _startTimestamp;
        set
        {
            if (SetProperty(ref _startTimestamp, value))
            {
                OnPropertyChanged(nameof(TimeRangeText));
            }
        }
    }

    public DateTime LastActivityTimestamp
    {
        get => _lastActivityTimestamp;
        set
        {
            if (SetProperty(ref _lastActivityTimestamp, value))
            {
                OnPropertyChanged(nameof(TimeRangeText));
            }
        }
    }

    public long LengthBytes
    {
        get => _lengthBytes;
        set
        {
            if (SetProperty(ref _lengthBytes, value))
            {
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(MetadataText));
            }
        }
    }

    public int EntryCount
    {
        get => _entryCount;
        set
        {
            if (SetProperty(ref _entryCount, value))
            {
                OnPropertyChanged(nameof(EntryCountText));
                OnPropertyChanged(nameof(MetadataText));
            }
        }
    }

    [JsonIgnore]
    public string FileName => Path.GetFileName(FilePath);

    [JsonIgnore]
    public string FileKey => Path.GetFullPath(FilePath).ToUpperInvariant();

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? FileName : Alias;

    [JsonIgnore]
    public SolidColorBrush ColorBrush => Palette.Brush(Color);

    [JsonIgnore]
    public string TimeRangeText => $"{StartTimestamp:MM-dd HH:mm:ss} -> {LastActivityTimestamp:HH:mm:ss}";

    [JsonIgnore]
    public string SizeText => LengthBytes < 1024 * 1024
        ? $"{LengthBytes / 1024.0:0.0} KB"
        : $"{LengthBytes / 1024.0 / 1024.0:0.0} MB";

    [JsonIgnore]
    public string EntryCountText => EntryCount == 0 ? "Not parsed" : $"{EntryCount:n0} entries";

    [JsonIgnore]
    public string StatusText => IsAvailable ? "Online" : "Missing";

    [JsonIgnore]
    public string MetadataText => $"{SourceToken} | {SizeText} | {EntryCountText} | {StatusText}";
}

public sealed class RegexFilterItem : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "Filter";
    private string _pattern = string.Empty;
    private string _color = Palette.At(0);
    private bool _isEnabled = true;
    private bool _isRegex;
    private bool _isValid = true;
    private string _errorText = string.Empty;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }

    public string Color
    {
        get => _color;
        set
        {
            if (SetProperty(ref _color, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsRegex
    {
        get => _isRegex;
        set => SetProperty(ref _isRegex, value);
    }

    public bool IsValid
    {
        get => _isValid;
        set
        {
            if (SetProperty(ref _isValid, value))
            {
                OnPropertyChanged(nameof(ValidityText));
            }
        }
    }

    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    [JsonIgnore]
    public SolidColorBrush ColorBrush => Palette.Brush(Color);

    [JsonIgnore]
    public string ValidityText => IsValid ? "OK" : "Invalid";
}

public sealed class FilterBadge
{
    public required string Name { get; init; }
    public required string Color { get; init; }

    [JsonIgnore]
    public SolidColorBrush ColorBrush => Palette.Brush(Color);
}

public sealed class TimelineEntry : ObservableObject
{
    private bool _isSelectedForCopy;
    private bool _isExpanded;
    private bool _isHidden;
    private bool _isReviewedBoundary;
    private ObservableCollection<FilterBadge> _filterBadges = [];

    public required string LineKey { get; init; }
    public string SourceFileKey { get; init; } = string.Empty;
    public string SourceToken { get; init; } = string.Empty;
    public string FileAlias { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public long SortTicks { get; init; }
    public int Sequence { get; init; }
    public int LineNumber { get; init; }
    public string Severity { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
    public string ContinuationText { get; init; } = string.Empty;
    public string SourceColor { get; init; } = "#4CC9F0";
    public string FileColor { get; init; } = "#4CC9F0";
    public bool IsMarker { get; init; }
    public bool IsReviewMarker { get; init; }

    public ObservableCollection<FilterBadge> FilterBadges
    {
        get => _filterBadges;
        set => SetProperty(ref _filterBadges, value);
    }

    public bool IsSelectedForCopy
    {
        get => _isSelectedForCopy;
        set
        {
            if (SetProperty(ref _isSelectedForCopy, value))
            {
                OnPropertyChanged(nameof(SelectionBorderBrush));
                OnPropertyChanged(nameof(SelectionBorderThickness));
                OnPropertyChanged(nameof(RowBackgroundBrush));
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandGlyph));
                OnPropertyChanged(nameof(ContinuationVisibility));
            }
        }
    }

    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (SetProperty(ref _isHidden, value))
            {
                OnPropertyChanged(nameof(RowOpacity));
            }
        }
    }

    public bool IsReviewedBoundary
    {
        get => _isReviewedBoundary;
        set
        {
            if (SetProperty(ref _isReviewedBoundary, value))
            {
                OnPropertyChanged(nameof(RowBackgroundBrush));
            }
        }
    }

    [JsonIgnore]
    public string FullText => string.IsNullOrEmpty(ContinuationText)
        ? Message
        : $"{Message}{Environment.NewLine}{ContinuationText}";

    [JsonIgnore]
    public bool HasContinuation => !string.IsNullOrWhiteSpace(ContinuationText);

    [JsonIgnore]
    public string DisplayTime => Timestamp.ToString("HH:mm:ss.fff");

    [JsonIgnore]
    public string DisplaySourceTag => IsMarker ? "MARK" : SourceToken;

    [JsonIgnore]
    public string DisplayFileTag => IsMarker ? string.Empty : FileAlias;

    [JsonIgnore]
    public Visibility FileTagVisibility => IsMarker ? Visibility.Collapsed : Visibility.Visible;

    [JsonIgnore]
    public string ExpandGlyph => IsExpanded ? "▼" : "▶";

    [JsonIgnore]
    public Visibility ContinuationVisibility => IsExpanded && HasContinuation ? Visibility.Visible : Visibility.Collapsed;

    [JsonIgnore]
    public SolidColorBrush SourceBrush => IsMarker ? Palette.Brush("#F4D35E") : Palette.Brush(SourceColor);

    [JsonIgnore]
    public SolidColorBrush FileBrush => IsMarker ? Palette.Brush("#F4D35E") : Palette.Brush(FileColor);

    [JsonIgnore]
    public SolidColorBrush SeverityBrush => Severity.ToUpperInvariant() switch
    {
        "ERROR" => Palette.Brush("#E63946"),
        "EXCEPTION" => Palette.Brush("#E63946"),
        "WARNING" => Palette.Brush("#F4A261"),
        "WARN" => Palette.Brush("#F4A261"),
        "DEBUG" => Palette.Brush("#6C8EBF"),
        "LOG" => Palette.Brush("#8AB17D"),
        _ => IsMarker ? Palette.Brush("#F4D35E") : Palette.Brush("#6C757D")
    };

    [JsonIgnore]
    public SolidColorBrush RowBackgroundBrush => IsReviewedBoundary
        ? Palette.Brush("#273A22")
        : IsSelectedForCopy
            ? Palette.Brush("#172434")
        : IsMarker
            ? Palette.Brush("#2C2918")
            : Palette.Brush("#111820");

    [JsonIgnore]
    public double RowOpacity => IsHidden ? 0.45 : 1.0;

    [JsonIgnore]
    public SolidColorBrush SelectionBorderBrush => IsSelectedForCopy ? Palette.Brush("#4CC9F0") : Palette.Brush("#263542");

    [JsonIgnore]
    public Thickness SelectionBorderThickness => IsSelectedForCopy ? new Thickness(2, 1, 2, 1) : new Thickness(0, 0, 0, 1);
}

public sealed class MarkerItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; }
    public long SortTicks { get; set; }
    public string Text { get; set; } = string.Empty;
    public string AnchorLineKey { get; set; } = string.Empty;
}

public enum ChecklistNodeType
{
    Root,
    OrderedGroup,
    UnorderedGroup,
    Action,
    Expect,
    Marker
}

public sealed class ChecklistNode : ObservableObject
{
    private bool _isComplete;
    private bool _isActive;
    private string _statusText = "Pending";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ParentId { get; set; } = string.Empty;
    public ChecklistNodeType Type { get; set; }
    public int Indent { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string InsertMarker { get; set; } = string.Empty;
    public string MatchLineKey { get; set; } = string.Empty;
    public long MatchSortTicks { get; set; }
    public ObservableCollection<ChecklistNode> Children { get; } = [];

    public bool IsComplete
    {
        get => _isComplete;
        set
        {
            if (SetProperty(ref _isComplete, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(IsManualEnabled));
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    [JsonIgnore]
    public bool IsManualEnabled => Type is ChecklistNodeType.Action or ChecklistNodeType.Marker;

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        ChecklistNodeType.OrderedGroup => "ORDER",
        ChecklistNodeType.UnorderedGroup => "ANY",
        ChecklistNodeType.Action => "ACTION",
        ChecklistNodeType.Expect => "EXPECT",
        ChecklistNodeType.Marker => "MARK",
        _ => "ROOT"
    };

    [JsonIgnore]
    public Thickness IndentMargin => new(Indent * 16, 2, 2, 2);

    [JsonIgnore]
    public SolidColorBrush StatusBrush => IsComplete
        ? Palette.Brush("#64D68A")
        : IsActive
            ? Palette.Brush("#F4D35E")
            : Palette.Brush("#607080");
}

public sealed class SourceDirectoryState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Token { get; set; } = "S1";
    public string Color { get; set; } = Palette.At(0);
    public string DirectoryPath { get; set; } = string.Empty;
}

public sealed class LogFileState
{
    public string Alias { get; set; } = string.Empty;
    public string Color { get; set; } = Palette.At(0);
    public bool IncludeInTimeline { get; set; }
    public bool IsVisible { get; set; } = true;
}

public sealed class FilterState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Filter";
    public string Pattern { get; set; } = string.Empty;
    public string Color { get; set; } = Palette.At(0);
    public bool IsEnabled { get; set; } = true;
    public bool IsRegex { get; set; }
}

public sealed class AppSettingsState
{
    public List<SourceDirectoryState> Sources { get; set; } = [];
    public Dictionary<string, LogFileState> FileStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FilterState> Filters { get; set; } = [];
    public List<MarkerItem> Markers { get; set; } = [];
    public HashSet<string> HiddenLineKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string ReviewedLineKey { get; set; } = string.Empty;
    public bool ShowUnfilteredLines { get; set; }
    public bool ShowHiddenLines { get; set; }
    public bool ShowMarkers { get; set; } = true;
    public bool ShowLevelError { get; set; } = true;
    public bool ShowLevelWarning { get; set; } = true;
    public bool ShowLevelDebug { get; set; } = true;
    public bool ShowLevelLog { get; set; } = true;
    public bool ShowLevelOther { get; set; } = true;
    public string AutoScrollMode { get; set; } = "Follow If At Bottom";
    public string SearchText { get; set; } = string.Empty;
    public bool SearchUseRegex { get; set; }
    public string ChecklistText { get; set; } = string.Empty;
}
