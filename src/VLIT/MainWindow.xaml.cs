using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VLIT.Services;

namespace VLIT;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly SettingsStore _settingsStore = new();
    private readonly Dictionary<string, List<TimelineEntry>> _entriesByFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hiddenLineKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MarkerItem> _markers = [];
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly DispatcherTimer _refreshDebounce = new();
    private readonly DispatcherTimer _parseDebounce = new();
    private AppSettingsState _settings = new();
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _isDraggingTimelineSelection;
    private bool _dragSelectionValue;
    private bool _isEvaluatingChecklist;
    private bool _isChecklistPaused;
    private bool _hasCompletedInitialDiscovery;
    private string _reviewedLineKey = string.Empty;
    private string _statusText = "Ready";
    private string _searchStatusText = "0 matches";
    private int _loadedEntryCount;
    private int _visibleEntryCount;
    private bool _showUnfilteredLines;
    private bool _showHiddenLines;
    private bool _showMarkers = true;
    private bool _showLevelError = true;
    private bool _showLevelWarning = true;
    private bool _showLevelDebug = true;
    private bool _showLevelLog = true;
    private bool _showLevelOther = true;
    private string _autoScrollMode = "Follow If At Bottom";
    private string _searchText = string.Empty;
    private bool _searchUseRegex;
    private string _checklistText = string.Empty;
    private int _searchIndex = -1;
    private List<TimelineEntry> _searchMatches = [];
    private TimelineEntry? _dragStartEntry;
    private bool _dragSelectionIsAdditive;
    private TimelineEntry? _lastSelectionAnchor;
    private bool _lastSelectionValue = true;
    private GridLength _savedSourcesWidth = new(430);
    private GridLength _savedChecklistWidth = new(370);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SourceDirectoryItem> Sources { get; } = [];
    public ObservableCollection<LogFileItem> LogFiles { get; } = [];
    public ObservableCollection<RegexFilterItem> Filters { get; } = [];
    public ObservableCollection<TimelineEntry> TimelineEntries { get; } = [];
    public ObservableCollection<ChecklistNode> ChecklistItems { get; private set; } = [];
    public IReadOnlyList<string> AutoScrollOptions { get; } = ["Off", "Always On", "Follow If At Bottom"];

    public bool HasReviewedMarker => !string.IsNullOrWhiteSpace(_reviewedLineKey);

    public string ChecklistPauseButtonText => _isChecklistPaused ? "▶ Unpause" : "⏸ Pause";

    public SolidColorBrush ChecklistPauseButtonBrush => _isChecklistPaused ? Palette.Brush("#F4D35E") : Palette.Brush("#1B2733");

    public SolidColorBrush ChecklistPauseButtonForeground => _isChecklistPaused ? Palette.Brush("#0B1117") : Palette.Brush("#E8EEF4");

    public bool IsChecklistStepEnabled => _isChecklistPaused;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string SearchStatusText
    {
        get => _searchStatusText;
        set => SetProperty(ref _searchStatusText, value);
    }

    public int LoadedEntryCount
    {
        get => _loadedEntryCount;
        set => SetProperty(ref _loadedEntryCount, value);
    }

    public int VisibleEntryCount
    {
        get => _visibleEntryCount;
        set => SetProperty(ref _visibleEntryCount, value);
    }

    public bool ShowUnfilteredLines
    {
        get => _showUnfilteredLines;
        set
        {
            if (SetProperty(ref _showUnfilteredLines, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public bool ShowHiddenLines
    {
        get => _showHiddenLines;
        set
        {
            if (SetProperty(ref _showHiddenLines, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public bool ShowMarkers
    {
        get => _showMarkers;
        set
        {
            if (SetProperty(ref _showMarkers, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public bool ShowLevelError
    {
        get => _showLevelError;
        set
        {
            if (SetProperty(ref _showLevelError, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public bool ShowLevelWarning
    {
        get => _showLevelWarning;
        set
        {
            if (SetProperty(ref _showLevelWarning, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public bool ShowLevelDebug
    {
        get => _showLevelDebug;
        set
        {
            if (SetProperty(ref _showLevelDebug, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public bool ShowLevelLog
    {
        get => _showLevelLog;
        set
        {
            if (SetProperty(ref _showLevelLog, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public bool ShowLevelOther
    {
        get => _showLevelOther;
        set
        {
            if (SetProperty(ref _showLevelOther, value))
            {
                ApplyTimelineFilters();
                SaveSettings();
            }
        }
    }

    public string AutoScrollMode
    {
        get => _autoScrollMode;
        set
        {
            if (SetProperty(ref _autoScrollMode, value))
            {
                SaveSettings();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                UpdateSearchMatches();
                SaveSettings();
            }
        }
    }

    public bool SearchUseRegex
    {
        get => _searchUseRegex;
        set
        {
            if (SetProperty(ref _searchUseRegex, value))
            {
                UpdateSearchMatches();
                SaveSettings();
            }
        }
    }

    public string ChecklistText
    {
        get => _checklistText;
        set
        {
            if (SetProperty(ref _checklistText, value))
            {
                SaveSettings();
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _refreshDebounce.Interval = TimeSpan.FromMilliseconds(450);
        _refreshDebounce.Tick += async (_, _) =>
        {
            _refreshDebounce.Stop();
            await RefreshAllAsync();
        };

        _parseDebounce.Interval = TimeSpan.FromMilliseconds(350);
        _parseDebounce.Tick += async (_, _) =>
        {
            _parseDebounce.Stop();
            await ParseIncludedLogsAsync();
        };

        Sources.CollectionChanged += Sources_CollectionChanged;
        Filters.CollectionChanged += Filters_CollectionChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoading = true;
        _settings = _settingsStore.Load();

        foreach (var sourceState in _settings.Sources)
        {
            var source = new SourceDirectoryItem
            {
                Id = string.IsNullOrWhiteSpace(sourceState.Id) ? Guid.NewGuid().ToString("N") : sourceState.Id,
                Token = sourceState.Token,
                Color = sourceState.Color,
                DirectoryPath = sourceState.DirectoryPath
            };
            WireSource(source);
            Sources.Add(source);
        }

        if (_settings.Filters.Count == 0)
        {
            AddDefaultFilters();
        }
        else
        {
            foreach (var filterState in _settings.Filters)
            {
                var filter = new RegexFilterItem
                {
                    Id = filterState.Id,
                    Name = filterState.Name,
                    Pattern = filterState.Pattern,
                    Color = filterState.Color,
                    IsEnabled = filterState.IsEnabled,
                    IsRegex = filterState.IsRegex
                };
                WireFilter(filter);
                Filters.Add(filter);
            }
        }

        _markers.Clear();
        _markers.AddRange(_settings.Markers);
        _hiddenLineKeys.Clear();
        foreach (var key in _settings.HiddenLineKeys)
        {
            _hiddenLineKeys.Add(key);
        }

        _reviewedLineKey = _settings.ReviewedLineKey;
        OnPropertyChanged(nameof(HasReviewedMarker));
        _showUnfilteredLines = _settings.ShowUnfilteredLines;
        _showHiddenLines = _settings.ShowHiddenLines;
        _showMarkers = _settings.ShowMarkers;
        _showLevelError = _settings.ShowLevelError;
        _showLevelWarning = _settings.ShowLevelWarning;
        _showLevelDebug = _settings.ShowLevelDebug;
        _showLevelLog = _settings.ShowLevelLog;
        _showLevelOther = _settings.ShowLevelOther;
        if (!_showLevelError && !_showLevelWarning && !_showLevelDebug && !_showLevelLog && !_showLevelOther)
        {
            _showLevelError = true;
            _showLevelWarning = true;
            _showLevelDebug = true;
            _showLevelLog = true;
            _showLevelOther = true;
        }
        _autoScrollMode = string.IsNullOrWhiteSpace(_settings.AutoScrollMode) ? "Follow If At Bottom" : _settings.AutoScrollMode;
        _searchText = _settings.SearchText;
        _searchUseRegex = _settings.SearchUseRegex;
        _checklistText = _settings.ChecklistText;
        OnPropertyChanged(nameof(ShowUnfilteredLines));
        OnPropertyChanged(nameof(ShowHiddenLines));
        OnPropertyChanged(nameof(ShowMarkers));
        OnPropertyChanged(nameof(ShowLevelError));
        OnPropertyChanged(nameof(ShowLevelWarning));
        OnPropertyChanged(nameof(ShowLevelDebug));
        OnPropertyChanged(nameof(ShowLevelLog));
        OnPropertyChanged(nameof(ShowLevelOther));
        OnPropertyChanged(nameof(AutoScrollMode));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SearchUseRegex));
        OnPropertyChanged(nameof(ChecklistText));

        if (!string.IsNullOrWhiteSpace(ChecklistText))
        {
            ParseChecklist();
        }

        _isLoading = false;
        await RefreshAllAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings();
        DisposeWatchers();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!IsControlDown() || IsTextInputFocused())
        {
            return;
        }

        if (e.Key == Key.A)
        {
            SelectAllTimelineEntries();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C)
        {
            CopyEntries(CheckedOrSelectedTimelineEntries());
            e.Handled = true;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
    }

    private void AddSource_Click(object sender, RoutedEventArgs e)
    {
        var source = new SourceDirectoryItem
        {
            Token = $"S{Sources.Count + 1}",
            Color = Palette.At(Sources.Count),
            DirectoryPath = string.Empty,
            StatusText = "Choose a directory"
        };
        WireSource(source);
        Sources.Add(source);
        SaveSettings();
    }

    private async void AddLocalSource_Click(object sender, RoutedEventArgs e)
    {
        var local = FindLocalVrchatDirectory();
        if (!Directory.Exists(local))
        {
            StatusText = "Default local VRChat directory was not found.";
            return;
        }

        if (Sources.Any(s => SamePath(s.DirectoryPath, local)))
        {
            StatusText = "Default local VRChat directory is already present.";
            return;
        }

        var source = new SourceDirectoryItem
        {
            Token = $"S{Sources.Count + 1}",
            Color = Palette.At(Sources.Count),
            DirectoryPath = local
        };
        WireSource(source);
        Sources.Add(source);
        SaveSettings();
        await RefreshAllAsync();
    }

    private static string FindLocalVrchatDirectory()
    {
        var candidates = new List<string>();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataRoot = Directory.GetParent(localAppData)?.FullName;
        if (!string.IsNullOrWhiteSpace(appDataRoot))
        {
            candidates.Add(Path.Combine(appDataRoot, "LocalLow", "VRChat", "VRChat"));
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            candidates.Add(Path.Combine(userProfile, "AppData", "LocalLow", "VRChat", "VRChat"));
        }

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates.FirstOrDefault() ?? string.Empty;
    }

    private async void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SourceDirectoryItem source)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select VRChat run/log directory",
            InitialDirectory = Directory.Exists(source.DirectoryPath) ? source.DirectoryPath : string.Empty,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            source.DirectoryPath = dialog.FolderName;
            SaveSettings();
            await RefreshAllAsync();
        }
    }

    private async void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SourceDirectoryItem source)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Remove source {source.Token}? Associated cached file state for this source will be removed.",
            "Remove source",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Sources.Remove(source);
        var toRemove = LogFiles.Where(f => f.SourceId == source.Id).ToList();
        foreach (var file in toRemove)
        {
            LogFiles.Remove(file);
            _entriesByFile.Remove(file.FileKey);
            _settings.FileStates.Remove(file.FileKey);
        }

        SaveSettings();
        await RefreshAllAsync();
    }

    private async void SourcePath_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        await RefreshAllAsync();
    }

    private void CycleLogColor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not LogFileItem file)
        {
            return;
        }

        file.Color = NextPaletteColor(file.Color);
    }

    private void CycleFilterColor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RegexFilterItem filter)
        {
            return;
        }

        filter.Color = NextPaletteColor(filter.Color);
    }

    private async void DeleteSelectedLogs_Click(object sender, RoutedEventArgs e)
    {
        var selected = LogFilesList.SelectedItems.OfType<LogFileItem>().ToList();
        if (selected.Count == 0)
        {
            StatusText = "Select one or more log files first.";
            return;
        }

        var result = MessageBox.Show(
            $"Delete {selected.Count} log file(s) from disk? This cannot be undone.",
            "Delete log files",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var file in selected)
        {
            try
            {
                if (File.Exists(file.FilePath))
                {
                    File.Delete(file.FilePath);
                }

                LogFiles.Remove(file);
                _entriesByFile.Remove(file.FileKey);
                _settings.FileStates.Remove(file.FileKey);
                _hiddenLineKeys.RemoveWhere(key => key.StartsWith(file.FileKey + ":", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                StatusText = $"Could not delete {file.FileName}: {ex.Message}";
            }
        }

        ApplyTimelineFilters();
        SaveSettings();
        await RefreshAllAsync();
    }

    private void LogFileRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not LogFileItem logFile)
        {
            return;
        }

        if (!item.IsSelected)
        {
            LogFilesList.SelectedItems.Clear();
            LogFilesList.SelectedItem = logFile;
        }

        item.Focus();
        item.ContextMenu = BuildLogFileContextMenu();
    }

    private ContextMenu BuildLogFileContextMenu()
    {
        var menu = new ContextMenu();
        var delete = new MenuItem { Header = "Delete selected log file(s)" };
        delete.Click += DeleteSelectedLogs_Click;
        menu.Items.Add(delete);
        return menu;
    }

    private void AddFilter_Click(object sender, RoutedEventArgs e)
    {
        var filter = new RegexFilterItem
        {
            Name = $"Filter {Filters.Count + 1}",
            Pattern = string.Empty,
            Color = Palette.At(Filters.Count + 4),
            IsEnabled = false
        };
        WireFilter(filter);
        Filters.Add(filter);
        SaveSettings();
    }

    private void RemoveFilter_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is RegexFilterItem filter)
        {
            Filters.Remove(filter);
            ApplyTimelineFilters();
            SaveSettings();
        }
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            UpdateSearchMatches();
            GoToSearchMatch(0);
            e.Handled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchMatches();
    }

    private void SearchFirst_Click(object sender, RoutedEventArgs e) => GoToSearchMatch(0);

    private void SearchPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (_searchMatches.Count == 0)
        {
            return;
        }

        GoToSearchMatch(_searchIndex <= 0 ? _searchMatches.Count - 1 : _searchIndex - 1);
    }

    private void SearchNext_Click(object sender, RoutedEventArgs e)
    {
        if (_searchMatches.Count == 0)
        {
            return;
        }

        GoToSearchMatch(_searchIndex >= _searchMatches.Count - 1 ? 0 : _searchIndex + 1);
    }

    private void SearchLast_Click(object sender, RoutedEventArgs e) => GoToSearchMatch(_searchMatches.Count - 1);

    private void SearchToFilter_Click(object sender, RoutedEventArgs e)
    {
        var pattern = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        var filter = new RegexFilterItem
        {
            Name = $"Search {Filters.Count + 1}",
            Pattern = pattern,
            Color = Palette.At(Filters.Count + 5),
            IsEnabled = true,
            IsRegex = SearchUseRegex
        };
        WireFilter(filter);
        Filters.Add(filter);
        ShowUnfilteredLines = false;
        ApplyTimelineFilters();
        SaveSettings();
    }

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        CopyEntries(CheckedOrSelectedTimelineEntries());
    }

    private void CopyEntries(IReadOnlyList<TimelineEntry> entries)
    {
        if (entries.Count == 0)
        {
            StatusText = "No checked or selected timeline entries to copy.";
            return;
        }

        Clipboard.SetText(BuildClipboardText(entries));
        StatusText = $"Copied {entries.Count:n0} entries.";
    }

    private void HideSelected_Click(object sender, RoutedEventArgs e)
    {
        HideEntries(CheckedOrSelectedTimelineEntries());
    }

    private void HideEntries(IReadOnlyCollection<TimelineEntry> entries)
    {
        if (entries.Count == 0)
        {
            StatusText = "No checked or selected timeline entries to hide.";
            return;
        }

        foreach (var entry in entries)
        {
            _hiddenLineKeys.Add(entry.LineKey);
            entry.IsHidden = true;
        }

        ApplyTimelineFilters();
        SaveSettings();
        StatusText = $"Hid {entries.Count:n0} entries.";
    }

    private void AddMarker_Click(object sender, RoutedEventArgs e)
    {
        InsertPromptedMarker(GetMarkerAnchor());
    }

    private void ContextAddMarker_Click(object sender, RoutedEventArgs e)
    {
        InsertPromptedMarker(GetContextTimelineEntry(sender) ?? GetMarkerAnchor());
    }

    private void ContextReviewed_Click(object sender, RoutedEventArgs e)
    {
        MarkReviewed(GetContextTimelineEntry(sender));
    }

    private void MarkReviewed(TimelineEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _reviewedLineKey = entry.LineKey;
        OnPropertyChanged(nameof(HasReviewedMarker));
        SetChecklistPaused(true);
        ApplyTimelineFilters();
        SaveSettings();
        StatusText = $"Reviewed marker moved to {entry.DisplayTime}.";
    }

    private void ClearReviewed_Click(object sender, RoutedEventArgs e)
    {
        ClearReviewedMarker();
    }

    private void ClearReviewedMarker()
    {
        if (string.IsNullOrWhiteSpace(_reviewedLineKey))
        {
            return;
        }

        _reviewedLineKey = string.Empty;
        OnPropertyChanged(nameof(HasReviewedMarker));
        SetChecklistPaused(true);
        ApplyTimelineFilters();
        SaveSettings();
        StatusText = "Reviewed marker cleared.";
    }

    private void ScrollToStart_Click(object sender, RoutedEventArgs e)
    {
        ScrollTimelineTo(TimelineEntries.FirstOrDefault());
    }

    private void ScrollToReviewed_Click(object sender, RoutedEventArgs e)
    {
        ScrollTimelineTo(TimelineEntries.FirstOrDefault(e => e.LineKey.Equals(_reviewedLineKey, StringComparison.OrdinalIgnoreCase)));
    }

    private void ScrollToSelected_Click(object sender, RoutedEventArgs e)
    {
        ScrollTimelineTo(_lastSelectionAnchor
            ?? TimelineEntries.LastOrDefault(entry => entry.IsSelectedForCopy)
            ?? (TimelineList.SelectedItem as TimelineEntry));
    }

    private void ScrollToEnd_Click(object sender, RoutedEventArgs e)
    {
        ScrollTimelineTo(TimelineEntries.LastOrDefault());
    }

    private void ScrollTimelineTo(TimelineEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        TimelineList.SelectedItem = entry;
        TimelineList.ScrollIntoView(entry);
        TimelineList.Focus();
    }

    private void RemoveMarkers(IReadOnlyCollection<TimelineEntry> entries)
    {
        var markerIds = entries
            .Where(entry => entry.IsMarker && entry.LineKey.StartsWith("marker:", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.LineKey["marker:".Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (markerIds.Count == 0)
        {
            return;
        }

        var removed = _markers.RemoveAll(marker => markerIds.Contains(marker.Id));
        if (removed == 0)
        {
            return;
        }

        foreach (var entry in entries)
        {
            _hiddenLineKeys.Remove(entry.LineKey);
        }

        ApplyTimelineFilters();
        SaveSettings();
        StatusText = removed == 1 ? "Marker removed." : $"{removed:n0} markers removed.";
    }

    private void TimelineRow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var original = e.OriginalSource as DependencyObject;
        if (FindAncestor<ButtonBase>(original) is { } button)
        {
            if (button.Name == "ExpandToggleButton" &&
                button.DataContext is TimelineEntry toggleEntry)
            {
                toggleEntry.IsExpanded = !toggleEntry.IsExpanded;
                e.Handled = true;
            }

            return;
        }

        if (FindAncestor<TextBoxBase>(original) is not null)
        {
            return;
        }

        if (GetTimelineEntryFromSender(sender) is not { } entry)
        {
            return;
        }

        if (IsShiftDown())
        {
            var preserveExisting = IsControlDown() || !_lastSelectionValue;
            SelectTimelineRange(_lastSelectionAnchor ?? TimelineEntries.FirstOrDefault(e => e.IsSelectedForCopy) ?? entry, entry, _lastSelectionValue, preserveExisting);
            e.Handled = true;
            return;
        }

        _dragSelectionValue = IsControlDown() ? !entry.IsSelectedForCopy : true;
        _dragSelectionIsAdditive = IsControlDown() || !_dragSelectionValue;
        if (!_dragSelectionIsAdditive)
        {
            ClearTimelineSelection();
        }

        _isDraggingTimelineSelection = true;
        _dragStartEntry = entry;
        entry.IsSelectedForCopy = _dragSelectionValue;
        _lastSelectionAnchor = entry;
        _lastSelectionValue = _dragSelectionValue;
        Mouse.Capture(sender as IInputElement);
        e.Handled = true;
    }

    private void TimelineRow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingTimelineSelection || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (GetTimelineEntryFromSender(sender) is { } entry)
        {
            SelectTimelineRange(_dragStartEntry, entry, _dragSelectionValue, _dragSelectionIsAdditive);
        }
    }

    private void TimelineRow_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTimelineSelection = false;
        _dragStartEntry = null;
        Mouse.Capture(null);
    }

    private void ExpandToggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TimelineEntry entry)
        {
            entry.IsExpanded = !entry.IsExpanded;
            e.Handled = true;
        }
    }

    private void TimelineCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TimelineEntry entry &&
            sender is CheckBox checkBox)
        {
            entry.IsSelectedForCopy = checkBox.IsChecked == true;
            _lastSelectionAnchor = entry;
            _lastSelectionValue = entry.IsSelectedForCopy;
        }
    }

    private void TimelineRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not TimelineEntry entry)
        {
            return;
        }

        if (!entry.IsSelectedForCopy)
        {
            if (!IsControlDown())
            {
                ClearTimelineSelection();
            }

            entry.IsSelectedForCopy = true;
            _lastSelectionAnchor = entry;
            _lastSelectionValue = true;
        }

        TimelineList.SelectedItem = entry;
        item.Focus();
        item.ContextMenu = BuildTimelineContextMenu(entry);
    }

    private ContextMenu BuildTimelineContextMenu(TimelineEntry entry)
    {
        var contextEntries = GetTimelineContextEntries(entry);
        var anchor = TimelineEntry.OrderForTimeline(contextEntries).LastOrDefault() ?? entry;
        var menu = new ContextMenu();
        var copy = new MenuItem { Header = contextEntries.Count == 1 ? "Copy selected line" : $"Copy {contextEntries.Count:n0} selected lines" };
        copy.Click += (_, _) => CopyEntries(contextEntries);
        var hide = new MenuItem { Header = contextEntries.Count == 1 ? "Hide selected line" : $"Hide {contextEntries.Count:n0} selected lines" };
        hide.Click += (_, _) => HideEntries(contextEntries);
        var marker = new MenuItem { Header = contextEntries.Count == 1 ? "Insert marker after this line" : "Insert marker after last selected line" };
        marker.Click += (_, _) => InsertPromptedMarker(anchor);
        var reviewed = new MenuItem { Header = contextEntries.Count == 1 ? "Reviewed up to here" : "Reviewed through selected lines" };
        reviewed.Click += (_, _) => MarkReviewed(anchor);

        menu.Items.Add(copy);
        menu.Items.Add(hide);
        menu.Items.Add(new Separator());
        menu.Items.Add(marker);
        menu.Items.Add(reviewed);

        if (HasReviewedMarker)
        {
            var clearReviewed = new MenuItem { Header = "Clear reviewed marker" };
            clearReviewed.Click += (_, _) => ClearReviewedMarker();
            menu.Items.Add(clearReviewed);
        }

        if (contextEntries.Any(e => e.IsMarker))
        {
            var removeMarker = new MenuItem { Header = "Remove Marker(s)" };
            removeMarker.Click += (_, _) => RemoveMarkers(contextEntries);
            menu.Items.Add(new Separator());
            menu.Items.Add(removeMarker);
        }

        return menu;
    }

    private List<TimelineEntry> GetTimelineContextEntries(TimelineEntry fallback)
    {
        var entries = CheckedOrSelectedTimelineEntries();
        return entries.Count > 0 ? entries : [fallback];
    }

    private void ClearTimelineSelection()
    {
        foreach (var timelineEntry in TimelineEntries.Where(e => e.IsSelectedForCopy))
        {
            timelineEntry.IsSelectedForCopy = false;
        }
    }

    private void SelectAllTimelineEntries()
    {
        foreach (var entry in TimelineEntries)
        {
            entry.IsSelectedForCopy = true;
        }

        _lastSelectionAnchor = TimelineEntries.FirstOrDefault();
        _lastSelectionValue = true;
        TimelineList.Focus();
        StatusText = $"Selected {TimelineEntries.Count:n0} visible entries.";
    }

    private void SelectTimelineRange(TimelineEntry? start, TimelineEntry end, bool selected, bool preserveExisting)
    {
        if (start is null)
        {
            end.IsSelectedForCopy = selected;
            return;
        }

        if (!preserveExisting)
        {
            ClearTimelineSelection();
        }

        var startIndex = TimelineEntries.IndexOf(start);
        var endIndex = TimelineEntries.IndexOf(end);
        if (startIndex < 0 || endIndex < 0)
        {
            end.IsSelectedForCopy = selected;
            return;
        }

        var low = Math.Min(startIndex, endIndex);
        var high = Math.Max(startIndex, endIndex);
        for (var i = low; i <= high; i++)
        {
            TimelineEntries[i].IsSelectedForCopy = selected;
        }
    }

    private static bool IsControlDown()
    {
        return Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
    }

    private static bool IsShiftDown()
    {
        return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
    }

    private static bool IsTextInputFocused()
    {
        return Keyboard.FocusedElement is DependencyObject focused &&
            FindAncestor<TextBoxBase>(focused) is not null;
    }

    private void ToggleSources_Click(object sender, RoutedEventArgs e)
    {
        var isOpen = SourcesPanel.Visibility == Visibility.Visible;
        if (isOpen)
        {
            if (SourcesColumn.Width.Value > 0)
            {
                _savedSourcesWidth = SourcesColumn.Width;
            }

            SourcesPanel.Visibility = Visibility.Collapsed;
            SourcesSplitter.Visibility = Visibility.Collapsed;
            SourcesColumn.MinWidth = 0;
            SourcesSplitterColumn.Width = new GridLength(0);
            SourcesColumn.Width = new GridLength(0);
        }
        else
        {
            SourcesPanel.Visibility = Visibility.Visible;
            SourcesSplitter.Visibility = Visibility.Visible;
            SourcesColumn.MinWidth = 330;
            SourcesSplitterColumn.Width = new GridLength(5);
            SourcesColumn.Width = _savedSourcesWidth.Value > 0 ? _savedSourcesWidth : new GridLength(430);
        }
    }

    private void ToggleChecklistPause_Click(object sender, RoutedEventArgs e)
    {
        SetChecklistPaused(!_isChecklistPaused);
        if (!_isChecklistPaused)
        {
            EvaluateChecklist();
        }
    }

    private void ChecklistStep_Click(object sender, RoutedEventArgs e)
    {
        if (!_isChecklistPaused)
        {
            return;
        }

        EvaluateChecklist(singleStep: true);
    }

    private void SetChecklistPaused(bool value)
    {
        if (_isChecklistPaused == value)
        {
            return;
        }

        _isChecklistPaused = value;
        OnPropertyChanged(nameof(ChecklistPauseButtonText));
        OnPropertyChanged(nameof(ChecklistPauseButtonBrush));
        OnPropertyChanged(nameof(ChecklistPauseButtonForeground));
        OnPropertyChanged(nameof(IsChecklistStepEnabled));
        if (_isChecklistPaused)
        {
            StatusText = "Checklist paused.";
        }
    }

    private void ToggleChecklist_Click(object sender, RoutedEventArgs e)
    {
        var isOpen = ChecklistPanel.Visibility == Visibility.Visible;
        if (isOpen)
        {
            if (ChecklistColumn.Width.Value > 0)
            {
                _savedChecklistWidth = ChecklistColumn.Width;
            }

            ChecklistPanel.Visibility = Visibility.Collapsed;
            ChecklistSplitter.Visibility = Visibility.Collapsed;
            ChecklistSplitterColumn.Width = new GridLength(0);
            ChecklistColumn.Width = new GridLength(0);
        }
        else
        {
            ChecklistPanel.Visibility = Visibility.Visible;
            ChecklistSplitter.Visibility = Visibility.Visible;
            ChecklistSplitterColumn.Width = new GridLength(5);
            ChecklistColumn.Width = _savedChecklistWidth.Value > 0 ? _savedChecklistWidth : new GridLength(370);
        }
    }

    private void ParseChecklist_Click(object sender, RoutedEventArgs e)
    {
        ParseChecklist();
        ApplyTimelineFilters();
        SaveSettings();
    }

    private void PasteChecklist_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            ChecklistText = Clipboard.GetText();
            ParseChecklist();
            ApplyTimelineFilters();
        }
    }

    private void ClearChecklist_Click(object sender, RoutedEventArgs e)
    {
        ChecklistText = string.Empty;
        ChecklistItems = [];
        OnPropertyChanged(nameof(ChecklistItems));
        SaveSettings();
    }

    private void SampleChecklist_Click(object sender, RoutedEventArgs e)
    {
        ChecklistText = """
# comments are shown in the checklist
ordered: Multiplayer smoke test
  action: Start host client => marker: Host started
  action: Start second client => marker: Second client started
  expect: Udon
  all: Startup warnings of interest
    expect: Warning
    expect: Network
  marker: Finished smoke-test pass
""";
        ParseChecklist();
        ApplyTimelineFilters();
        SaveSettings();
    }

    private void ChecklistManual_Checked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ChecklistNode node)
        {
            return;
        }

        MarkChecklistNodeComplete(node, skipped: false);
    }

    private void ChecklistRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not ChecklistNode node || node.IsComment)
        {
            return;
        }

        ChecklistList.SelectedItem = node;
        item.Focus();
        item.ContextMenu = BuildChecklistContextMenu(node);
    }

    private ContextMenu BuildChecklistContextMenu(ChecklistNode node)
    {
        var menu = new ContextMenu();
        var complete = new MenuItem { Header = "Mark complete" };
        complete.Click += (_, _) => MarkChecklistNodeComplete(node, skipped: false);
        var skip = new MenuItem { Header = "Mark skip" };
        skip.Click += (_, _) => MarkChecklistNodeComplete(node, skipped: true);
        menu.Items.Add(complete);
        menu.Items.Add(skip);

        if (CanRollbackChecklistNode(node))
        {
            var rollback = new MenuItem { Header = "Rollback" };
            rollback.Click += (_, _) => RollbackChecklistNode(node);
            menu.Items.Add(new Separator());
            menu.Items.Add(rollback);
        }

        return menu;
    }

    private void MarkChecklistNodeComplete(ChecklistNode node, bool skipped)
    {
        node.IsComplete = true;
        node.IsSkipped = skipped;
        node.StatusText = skipped ? "Skipped" : "Done";
        node.ReviewedBeforeLineKey = _reviewedLineKey;
        node.ReviewedBeforeSortTicks = GetReviewedSortTicks();
        node.MatchSortTicks = GetReviewedSortTicks();

        if (!skipped && !string.IsNullOrWhiteSpace(node.InsertMarker))
        {
            var marker = InsertChecklistMarker(node.InsertMarker, $"check:{node.Id}", applyNow: true);
            if (marker is not null)
            {
                node.MatchLineKey = MarkerLineKey(marker);
                node.MatchSortTicks = marker.SortTicks;
            }
        }

        SaveSettings();
        if (!_isChecklistPaused)
        {
            EvaluateChecklist();
        }
    }

    private bool CanRollbackChecklistNode(ChecklistNode node)
    {
        if (!(node.IsComplete || DescendantsOf(node).Any(child => child.IsComplete)))
        {
            return false;
        }

        return !AncestorsOf(node).Any(ancestor => ancestor.IsComplete);
    }

    private void RollbackChecklistNode(ChecklistNode node)
    {
        SetChecklistPaused(true);
        var previousReviewedKey = node.ReviewedBeforeLineKey;
        foreach (var target in new[] { node }.Concat(DescendantsOf(node)))
        {
            target.IsComplete = false;
            target.IsSkipped = false;
            target.IsActive = false;
            target.MatchLineKey = string.Empty;
            target.MatchSortTicks = 0;
            target.StatusText = target.Type == ChecklistNodeType.Expect ? "Watching" : "Pending";
            _markers.RemoveAll(marker => marker.AnchorLineKey.Equals($"check:{target.Id}", StringComparison.OrdinalIgnoreCase));
        }

        _reviewedLineKey = previousReviewedKey;
        OnPropertyChanged(nameof(HasReviewedMarker));
        ApplyTimelineFilters();
        SaveSettings();
        StatusText = "Checklist rolled back and paused.";
    }

    private IEnumerable<ChecklistNode> DescendantsOf(ChecklistNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var nested in DescendantsOf(child))
            {
                yield return nested;
            }
        }
    }

    private IEnumerable<ChecklistNode> AncestorsOf(ChecklistNode node)
    {
        var parentId = node.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId) && parentId != "root")
        {
            var parent = ChecklistItems.FirstOrDefault(candidate => candidate.Id == parentId);
            if (parent is null)
            {
                yield break;
            }

            yield return parent;
            parentId = parent.ParentId;
        }
    }

    private async Task RefreshAllAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            StatusText = "Scanning sources...";
            await DiscoverLogFilesAsync();
            _hasCompletedInitialDiscovery = true;
            ConfigureWatchers();
            await ParseIncludedLogsAsync();
            StatusText = $"Ready. {LogFiles.Count:n0} log files discovered.";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private Task DiscoverLogFilesAsync()
    {
        return Dispatcher.InvokeAsync(() =>
        {
            var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentByKey = LogFiles.ToDictionary(f => f.FileKey, StringComparer.OrdinalIgnoreCase);
            var next = new List<LogFileItem>();

            foreach (var source in Sources)
            {
                if (string.IsNullOrWhiteSpace(source.DirectoryPath) || !Directory.Exists(source.DirectoryPath))
                {
                    source.IsAvailable = false;
                    source.StatusText = string.IsNullOrWhiteSpace(source.DirectoryPath) ? "No path" : "Offline or unavailable";
                    continue;
                }

                source.IsAvailable = true;
                var files = SafeEnumerateLogs(source.DirectoryPath)
                    .OrderByDescending(f => LogParser.ParseStartTimestamp(f))
                    .ToList();
                source.StatusText = $"{files.Count:n0} logs";

                for (var index = 0; index < files.Count; index++)
                {
                    var path = files[index];
                    var key = Path.GetFullPath(path).ToUpperInvariant();
                    discovered.Add(key);
                    var info = new FileInfo(path);
                    var start = LogParser.ParseStartTimestamp(path);
                    var lastActivity = SafeLastWrite(info);

                    if (!currentByKey.TryGetValue(key, out var item))
                    {
                        var hasSavedState = _settings.FileStates.TryGetValue(key, out var saved);
                        var savedState = hasSavedState ? saved : null;
                        var includeByDefault = savedState?.IncludeInTimeline ?? _hasCompletedInitialDiscovery;
                        var showByDefault = savedState is not null
                            ? savedState.IncludeInTimeline && savedState.IsVisible
                            : _hasCompletedInitialDiscovery;
                        item = new LogFileItem
                        {
                            FilePath = path,
                            SourceId = source.Id,
                            SourceToken = source.Token,
                            SourceColor = source.Color,
                            Alias = string.IsNullOrWhiteSpace(savedState?.Alias)
                                ? LogParser.FormatTimestampToken(start, $"Client {index + 1}")
                                : savedState.Alias,
                            Color = savedState?.Color ?? Palette.At(index + 2),
                            IncludeInTimeline = includeByDefault,
                            IsVisible = showByDefault,
                            StartTimestamp = start,
                            LastActivityTimestamp = lastActivity,
                            LengthBytes = info.Exists ? info.Length : 0,
                            IsAvailable = info.Exists
                        };
                        WireLogFile(item);
                    }
                    else
                    {
                        item.SourceId = source.Id;
                        item.SourceToken = source.Token;
                        item.SourceColor = source.Color;
                        item.StartTimestamp = start;
                        item.LastActivityTimestamp = lastActivity;
                        item.LengthBytes = info.Exists ? info.Length : 0;
                        item.IsAvailable = info.Exists;
                    }

                    next.Add(item);
                }
            }

            foreach (var existing in LogFiles)
            {
                if (!discovered.Contains(existing.FileKey) && Sources.Any(s => s.Id == existing.SourceId))
                {
                    existing.IsAvailable = false;
                    next.Add(existing);
                }
            }

            ReplaceLogFiles(next
                .DistinctBy(f => f.FileKey, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(f => f.StartTimestamp)
                .ThenBy(f => f.FileName)
                .ToList());

            SaveSettings();
        }).Task;
    }

    private async Task ParseIncludedLogsAsync()
    {
        var included = LogFiles.Where(f => f.IncludeInTimeline && f.IsAvailable).ToList();
        StatusText = included.Count == 0 ? "No included logs to parse." : $"Parsing {included.Count:n0} included log file(s)...";

        var parsed = await Task.Run(() =>
        {
            var result = new Dictionary<string, List<TimelineEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in included)
            {
                try
                {
                    result[file.FileKey] = LogParser.ParseFile(file).ToList();
                }
                catch
                {
                    result[file.FileKey] = [];
                }
            }

            return result;
        });

        foreach (var file in LogFiles)
        {
            if (parsed.TryGetValue(file.FileKey, out var entries))
            {
                _entriesByFile[file.FileKey] = entries;
                file.EntryCount = entries.Count;
            }
            else if (!file.IncludeInTimeline)
            {
                _entriesByFile.Remove(file.FileKey);
                file.EntryCount = 0;
            }
        }

        LoadedEntryCount = _entriesByFile.Values.Sum(v => v.Count);
        ApplyTimelineFilters();
    }

    private void ApplyTimelineFilters()
    {
        if (_isLoading)
        {
            return;
        }

        var shouldAutoScroll = ShouldAutoScrollAfterRefresh();
        var activeFilters = CompileFilters();
        var visibleFiles = LogFiles
            .Where(f => f.IncludeInTimeline && f.IsVisible)
            .ToDictionary(f => f.FileKey, StringComparer.OrdinalIgnoreCase);

        var merged = new List<TimelineEntry>();
        foreach (var (fileKey, entries) in _entriesByFile)
        {
            if (!visibleFiles.ContainsKey(fileKey))
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (ShouldDisplayEntry(entry, activeFilters))
                {
                    merged.Add(entry);
                }
            }
        }

        if (ShowMarkers)
        {
            merged.AddRange(BuildMarkerEntries());
        }

        var ordered = TimelineEntry.OrderForTimeline(merged).ToList();

        TimelineEntries.Clear();
        foreach (var entry in ordered)
        {
            entry.IsHidden = _hiddenLineKeys.Contains(entry.LineKey);
            entry.IsReviewedBoundary = entry.LineKey.Equals(_reviewedLineKey, StringComparison.OrdinalIgnoreCase);
            TimelineEntries.Add(entry);
        }

        VisibleEntryCount = TimelineEntries.Count;
        UpdateSearchMatches();
        EvaluateChecklist();

        if (shouldAutoScroll)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (TimelineEntries.Count > 0)
                {
                    TimelineList.ScrollIntoView(TimelineEntries[^1]);
                }
            }, DispatcherPriority.Background);
        }
    }

    private bool ShouldDisplayEntry(TimelineEntry entry, IReadOnlyList<(RegexFilterItem Filter, Regex? Regex)> activeFilters)
    {
        entry.FilterBadges.Clear();
        entry.IsHidden = _hiddenLineKeys.Contains(entry.LineKey);
        if (entry.IsHidden && !ShowHiddenLines)
        {
            return false;
        }

        var alwaysShowByLevel = IsAlwaysShowLevel(entry.Severity);
        foreach (var (filter, regex) in activeFilters)
        {
            if (FilterMatches(entry.CopyText, filter, regex))
            {
                entry.FilterBadges.Add(new FilterBadge { Name = filter.Name, Color = filter.Color });
            }
        }

        return entry.FilterBadges.Count > 0 || alwaysShowByLevel || ShowUnfilteredLines;
    }

    private List<TimelineEntry> BuildMarkerEntries()
    {
        return _markers.Select((marker, index) => new TimelineEntry
        {
            LineKey = $"marker:{marker.Id}",
            Timestamp = marker.Timestamp,
            SortTicks = marker.SortTicks == 0 ? marker.Timestamp.Ticks : marker.SortTicks,
            Sequence = index,
            LineNumber = index,
            Severity = "Marker",
            Message = marker.Text,
            SourceColor = "#F4D35E",
            IsMarker = true
        }).Where(e => ShowHiddenLines || !_hiddenLineKeys.Contains(e.LineKey)).ToList();
    }

    private bool IsAlwaysShowLevel(string severity)
    {
        var normalized = NormalizeSeverityGroup(severity);
        return normalized switch
        {
            "Error" => ShowLevelError,
            "Warning" => ShowLevelWarning,
            "Debug" => ShowLevelDebug,
            "Log" => ShowLevelLog,
            _ => ShowLevelOther
        };
    }

    private static string NormalizeSeverityGroup(string severity)
    {
        return severity.Trim().ToUpperInvariant() switch
        {
            "ERROR" or "EXCEPTION" or "FATAL" => "Error",
            "WARNING" or "WARN" => "Warning",
            "DEBUG" or "TRACE" => "Debug",
            "LOG" or "INFO" or "INFORMATION" => "Log",
            _ => "Other"
        };
    }

    private static bool FilterMatches(string text, RegexFilterItem filter, Regex? regex)
    {
        return filter.IsRegex
            ? regex?.IsMatch(text) == true
            : text.Contains(filter.Pattern, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<(RegexFilterItem Filter, Regex? Regex)> CompileFilters()
    {
        var compiled = new List<(RegexFilterItem, Regex?)>();
        foreach (var filter in Filters.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Pattern)))
        {
            if (!filter.IsRegex)
            {
                filter.IsValid = true;
                filter.ErrorText = "Text match";
                compiled.Add((filter, null));
                continue;
            }

            try
            {
                compiled.Add((filter, new Regex(filter.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)));
                filter.IsValid = true;
                filter.ErrorText = "Regex OK";
            }
            catch (Exception ex)
            {
                filter.IsValid = false;
                filter.ErrorText = ex.Message;
            }
        }

        return compiled;
    }

    private void ParseChecklist()
    {
        ChecklistItems = ChecklistParser.Parse(ChecklistText);
        OnPropertyChanged(nameof(ChecklistItems));
        EvaluateChecklist();
    }

    private void EvaluateChecklist(bool singleStep = false)
    {
        if (_isEvaluatingChecklist || ChecklistItems.Count == 0 || (_isChecklistPaused && !singleStep))
        {
            return;
        }

        _isEvaluatingChecklist = true;
        try
        {
            foreach (var node in ChecklistItems)
            {
                node.IsActive = false;
                if (!node.IsComplete)
                {
                    node.StatusText = node.Type switch
                    {
                        ChecklistNodeType.Expect => "Watching",
                        ChecklistNodeType.Comment => string.Empty,
                        _ => "Pending"
                    };
                }
            }

            var logEntries = TimelineEntry.OrderForTimeline(TimelineEntries.Where(e => !e.IsMarker)).ToList();

            var startCursor = GetReviewedSortTicks();
            var reviewedBeforeKey = _reviewedLineKey;
            var markerAdded = false;
            var reviewedAdvancedByMarker = false;
            var consumedStep = false;
            TimelineEntry? lastMatchedEntry = null;
            var cursor = startCursor;
            foreach (var root in ChecklistParser.Roots(ChecklistItems))
            {
                if (singleStep && consumedStep)
                {
                    break;
                }

                TryEvaluateChecklistNode(root, logEntries, ref cursor, ref markerAdded, ref reviewedAdvancedByMarker, ref lastMatchedEntry, singleStep, ref consumedStep, reviewedBeforeKey, startCursor);
            }

            if (lastMatchedEntry is not null && !reviewedAdvancedByMarker)
            {
                _reviewedLineKey = lastMatchedEntry.LineKey;
                OnPropertyChanged(nameof(HasReviewedMarker));
                ApplyTimelineFilters();
                SaveSettings();
            }
            else if (reviewedAdvancedByMarker)
            {
                OnPropertyChanged(nameof(HasReviewedMarker));
                ApplyTimelineFilters();
                SaveSettings();
            }
            else if (markerAdded)
            {
                SaveSettings();
                Dispatcher.BeginInvoke(ApplyTimelineFilters, DispatcherPriority.Background);
            }

            if (singleStep && !consumedStep)
            {
                StatusText = "No checklist step matched.";
            }
        }
        finally
        {
            _isEvaluatingChecklist = false;
        }
    }

    private bool TryEvaluateChecklistNode(
        ChecklistNode node,
        IReadOnlyList<TimelineEntry> entries,
        ref long cursor,
        ref bool markerAdded,
        ref bool reviewedAdvancedByMarker,
        ref TimelineEntry? lastMatchedEntry,
        bool singleStep,
        ref bool consumedStep,
        string reviewedBeforeKey,
        long reviewedBeforeSortTicks)
    {
        if (node.Type == ChecklistNodeType.Comment)
        {
            return true;
        }

        if (node.IsSkipped)
        {
            node.IsComplete = true;
            node.StatusText = "Skipped";
            cursor = Math.Max(cursor, node.MatchSortTicks);
            return true;
        }

        if (singleStep && consumedStep && !node.IsComplete)
        {
            return false;
        }

        switch (node.Type)
        {
            case ChecklistNodeType.Expect:
                node.IsActive = !node.IsComplete;
                if (node.IsComplete)
                {
                    cursor = Math.Max(cursor, node.MatchSortTicks);
                    return true;
                }

                if (!ChecklistParser.TryCompile(node, out var regex) || regex is null)
                {
                    return false;
                }

                var cursorSnapshot = cursor;
                var match = entries.FirstOrDefault(e => e.SortTicks > cursorSnapshot && regex.IsMatch(e.CopyText));
                if (match is null)
                {
                    node.StatusText = "Watching";
                    return false;
                }

                node.IsComplete = true;
                node.ReviewedBeforeLineKey = reviewedBeforeKey;
                node.ReviewedBeforeSortTicks = reviewedBeforeSortTicks;
                node.MatchLineKey = match.LineKey;
                node.MatchSortTicks = match.SortTicks;
                node.StatusText = match.DisplayTime;
                cursor = match.SortTicks;
                lastMatchedEntry = match;
                consumedStep = true;
                if (!string.IsNullOrWhiteSpace(node.InsertMarker))
                {
                    if (InsertChecklistMarker(node.InsertMarker, $"check:{node.Id}", applyNow: false) is { } marker)
                    {
                        markerAdded = true;
                        reviewedAdvancedByMarker = true;
                        node.MatchLineKey = MarkerLineKey(marker);
                        node.MatchSortTicks = marker.SortTicks;
                    }
                }

                return true;

            case ChecklistNodeType.Action:
            case ChecklistNodeType.Marker:
                node.IsActive = !node.IsComplete;
                if (node.IsComplete)
                {
                    cursor = Math.Max(cursor, node.MatchSortTicks);
                    return true;
                }

                node.StatusText = node.Type == ChecklistNodeType.Marker ? "Insert" : "Manual";
                return false;

            case ChecklistNodeType.OrderedGroup:
            case ChecklistNodeType.UnorderedGroup:
                return TryEvaluateChecklistGroup(node, entries, ref cursor, ref markerAdded, ref reviewedAdvancedByMarker, ref lastMatchedEntry, singleStep, ref consumedStep, reviewedBeforeKey, reviewedBeforeSortTicks);

            default:
                return false;
        }
    }

    private bool TryEvaluateChecklistGroup(
        ChecklistNode node,
        IReadOnlyList<TimelineEntry> entries,
        ref long cursor,
        ref bool markerAdded,
        ref bool reviewedAdvancedByMarker,
        ref TimelineEntry? lastMatchedEntry,
        bool singleStep,
        ref bool consumedStep,
        string reviewedBeforeKey,
        long reviewedBeforeSortTicks)
    {
        node.IsActive = !node.IsComplete;
        if (node.IsComplete)
        {
            cursor = Math.Max(cursor, node.MatchSortTicks);
            return true;
        }

        var children = node.Children.Where(child => child.Type != ChecklistNodeType.Comment).ToList();
        var requiredMin = node.RequiredMin < 0 ? children.Count : Math.Min(node.RequiredMin, children.Count);
        var requiredMax = node.RequiredMax < 0 ? children.Count : node.RequiredMax;
        var completedBefore = children.Count(c => c.IsComplete);

        if (node.IsOrdered)
        {
            foreach (var child in children)
            {
                if (requiredMax is not null && children.Count(c => c.IsComplete) >= requiredMax)
                {
                    break;
                }

                if (!TryEvaluateChecklistNode(child, entries, ref cursor, ref markerAdded, ref reviewedAdvancedByMarker, ref lastMatchedEntry, singleStep, ref consumedStep, reviewedBeforeKey, reviewedBeforeSortTicks))
                {
                    break;
                }

                if (singleStep && consumedStep)
                {
                    break;
                }
            }
        }
        else
        {
            var madeProgress = true;
            while (madeProgress && (requiredMax is null || children.Count(c => c.IsComplete) < requiredMax))
            {
                madeProgress = false;
                foreach (var child in children.Where(c => !c.IsComplete).ToList())
                {
                    var childCursor = cursor;
                    if (TryEvaluateChecklistNode(child, entries, ref childCursor, ref markerAdded, ref reviewedAdvancedByMarker, ref lastMatchedEntry, singleStep, ref consumedStep, reviewedBeforeKey, reviewedBeforeSortTicks))
                    {
                        cursor = Math.Max(cursor, childCursor);
                        madeProgress = true;
                    }

                    if (singleStep && consumedStep)
                    {
                        madeProgress = false;
                        break;
                    }
                }
            }
        }

        var completed = children.Count(c => c.IsComplete);
        node.StatusText = $"{completed}/{children.Count}";
        var isComplete = children.Count == 0
            ? requiredMin == 0
            : completed >= requiredMin && (requiredMax is null || completed <= requiredMax) &&
              (node.RequiredMin >= 0 || completed == children.Count);
        if (isComplete)
        {
            node.IsComplete = true;
            node.ReviewedBeforeLineKey = reviewedBeforeKey;
            node.ReviewedBeforeSortTicks = reviewedBeforeSortTicks;
            node.MatchSortTicks = Math.Max(cursor, children.Select(c => c.MatchSortTicks).DefaultIfEmpty(cursor).Max());
            node.MatchLineKey = lastMatchedEntry?.LineKey ?? string.Empty;
            node.StatusText = "Done";
            if (completed > completedBefore)
            {
                consumedStep = true;
            }

            if (!string.IsNullOrWhiteSpace(node.InsertMarker))
            {
                if (InsertChecklistMarker(node.InsertMarker, $"check:{node.Id}", applyNow: false) is { } marker)
                {
                    markerAdded = true;
                    reviewedAdvancedByMarker = true;
                    node.MatchLineKey = MarkerLineKey(marker);
                    node.MatchSortTicks = marker.SortTicks;
                }
            }
        }

        return node.IsComplete;
    }

    private long GetReviewedSortTicks()
    {
        if (string.IsNullOrWhiteSpace(_reviewedLineKey))
        {
            return 0L;
        }

        return GetReviewedEntry()?.SortTicks ?? 0L;
    }

    private TimelineEntry? GetReviewedEntry()
    {
        if (string.IsNullOrWhiteSpace(_reviewedLineKey))
        {
            return null;
        }

        var visible = TimelineEntries.FirstOrDefault(e => e.LineKey.Equals(_reviewedLineKey, StringComparison.OrdinalIgnoreCase));
        if (visible is not null)
        {
            return visible;
        }

        var parsed = _entriesByFile.Values
            .SelectMany(entries => entries)
            .FirstOrDefault(e => e.LineKey.Equals(_reviewedLineKey, StringComparison.OrdinalIgnoreCase));
        if (parsed is not null)
        {
            return parsed;
        }

        if (_reviewedLineKey.StartsWith("marker:", StringComparison.OrdinalIgnoreCase))
        {
            var markerId = _reviewedLineKey["marker:".Length..];
            var marker = _markers.FirstOrDefault(m => m.Id.Equals(markerId, StringComparison.OrdinalIgnoreCase));
            if (marker is not null)
            {
                return new TimelineEntry
                {
                    LineKey = MarkerLineKey(marker),
                    Timestamp = marker.Timestamp,
                    SortTicks = marker.SortTicks,
                    Severity = "Marker",
                    Message = marker.Text,
                    SourceColor = "#F4D35E",
                    FileColor = "#F4D35E",
                    IsMarker = true
                };
            }
        }

        return null;
    }

    private MarkerItem? InsertChecklistMarker(string text, string markerKey, bool applyNow)
    {
        var marker = InsertMarkerInternal(text, GetReviewedEntry() ?? GetMarkerAnchor(), markerKey, applyNow: false);
        if (marker is null)
        {
            return null;
        }

        _reviewedLineKey = MarkerLineKey(marker);
        OnPropertyChanged(nameof(HasReviewedMarker));
        if (applyNow)
        {
            ApplyTimelineFilters();
            SaveSettings();
        }

        return marker;
    }

    private static string MarkerLineKey(MarkerItem marker) => $"marker:{marker.Id}";

    private MarkerItem? InsertMarkerInternal(string text, TimelineEntry? anchor, string markerKey, bool applyNow)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (markerKey.StartsWith("check:", StringComparison.OrdinalIgnoreCase) &&
            _markers.FirstOrDefault(m => m.AnchorLineKey.Equals(markerKey, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            return existing;
        }

        var timestamp = anchor?.Timestamp ?? DateTime.Now;
        var sortTicks = GetMarkerSortTicksAfter(anchor, timestamp);
        var marker = new MarkerItem
        {
            Text = text.Trim(),
            Timestamp = new DateTime(Math.Clamp(sortTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks)),
            SortTicks = sortTicks,
            AnchorLineKey = markerKey
        };
        _markers.Add(marker);

        if (applyNow)
        {
            ApplyTimelineFilters();
            SaveSettings();
        }

        return marker;
    }

    private long GetMarkerSortTicksAfter(TimelineEntry? anchor, DateTime timestamp)
    {
        if (anchor is null)
        {
            return timestamp.Ticks;
        }

        var anchorSort = anchor.SortTicks;
        var nextSort = TimelineEntries
            .Where(e => e.SortTicks > anchorSort)
            .OrderBy(e => e.SortTicks)
            .Select(e => e.SortTicks)
            .FirstOrDefault();

        if (nextSort <= anchorSort)
        {
            return anchorSort + 1;
        }

        var gap = nextSort - anchorSort;
        return gap > 1 ? anchorSort + Math.Max(1, gap / 2) : anchorSort + 1;
    }

    private void InsertPromptedMarker(TimelineEntry? anchor)
    {
        var dialog = new PromptDialog("Insert marker", "Marker text:", "Started test action") { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            var markerKey = anchor is null
                ? $"manual:{Guid.NewGuid():N}"
                : $"{anchor.LineKey}:manual:{Guid.NewGuid():N}";
            InsertMarkerInternal(dialog.Value, anchor, markerKey, applyNow: true);
            StatusText = "Marker inserted.";
        }
    }

    private TimelineEntry? GetMarkerAnchor()
    {
        return TimelineList.SelectedItem as TimelineEntry
            ?? TimelineEntries.LastOrDefault(e => e.IsSelectedForCopy)
            ?? TimelineEntries.LastOrDefault();
    }

    private List<TimelineEntry> CheckedOrSelectedTimelineEntries()
    {
        var checkedEntries = TimelineEntries.Where(e => e.IsSelectedForCopy).ToList();
        if (checkedEntries.Count > 0)
        {
            return checkedEntries;
        }

        return TimelineList.SelectedItems.OfType<TimelineEntry>().ToList();
    }

    private string BuildClipboardText(IReadOnlyList<TimelineEntry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in TimelineEntry.OrderForTimeline(entries))
        {
            builder.AppendLine(entry.CopyText);
        }

        return builder.ToString();
    }

    private void UpdateSearchMatches()
    {
        var pattern = SearchText.Trim();
        _searchIndex = -1;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            _searchMatches = [];
            SearchStatusText = "0 matches";
            return;
        }

        try
        {
            if (SearchUseRegex)
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                _searchMatches = TimelineEntries.Where(e => regex.IsMatch(e.CopyText)).ToList();
            }
            else
            {
                _searchMatches = TimelineEntries.Where(e => e.CopyText.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            SearchStatusText = $"{_searchMatches.Count:n0} matches";
        }
        catch (Exception ex)
        {
            _searchMatches = [];
            SearchStatusText = $"Invalid regex: {ex.Message}";
        }
    }

    private void GoToSearchMatch(int index)
    {
        if (_searchMatches.Count == 0 || index < 0 || index >= _searchMatches.Count)
        {
            return;
        }

        _searchIndex = index;
        var entry = _searchMatches[index];
        TimelineList.SelectedItem = entry;
        TimelineList.ScrollIntoView(entry);
        SearchStatusText = $"{index + 1:n0}/{_searchMatches.Count:n0}";
    }

    private bool ShouldAutoScrollAfterRefresh()
    {
        return AutoScrollMode switch
        {
            "Always On" => true,
            "Follow If At Bottom" => IsTimelineAtBottom(),
            _ => false
        };
    }

    private bool IsTimelineAtBottom()
    {
        var scroll = FindVisualChild<ScrollViewer>(TimelineList);
        if (scroll is null)
        {
            return true;
        }

        return scroll.ScrollableHeight <= 0 || scroll.VerticalOffset >= scroll.ScrollableHeight - 2;
    }

    private void ConfigureWatchers()
    {
        DisposeWatchers();
        foreach (var source in Sources.Where(s => s.IsAvailable && Directory.Exists(s.DirectoryPath)))
        {
            try
            {
                var watcher = new FileSystemWatcher(source.DirectoryPath, "output_log_*.txt")
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };
                watcher.Created += Watcher_Changed;
                watcher.Changed += Watcher_Changed;
                watcher.Deleted += Watcher_Changed;
                watcher.Renamed += Watcher_Changed;
                watcher.Error += Watcher_Error;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch
            {
                source.IsAvailable = false;
                source.StatusText = "Watcher unavailable";
            }
        }
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            StatusText = "File change detected.";
            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        });
    }

    private void Watcher_Error(object sender, ErrorEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            StatusText = $"Watcher error: {e.GetException().Message}";
            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        });
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private void ReplaceLogFiles(IReadOnlyList<LogFileItem> next)
    {
        LogFiles.Clear();
        foreach (var item in next)
        {
            WireLogFile(item);
            LogFiles.Add(item);
        }
    }

    private void Sources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (SourceDirectoryItem source in e.NewItems)
            {
                WireSource(source);
            }
        }

        if (!_isLoading)
        {
            SaveSettings();
        }
    }

    private void Filters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (RegexFilterItem filter in e.NewItems)
            {
                WireFilter(filter);
            }
        }

        if (!_isLoading)
        {
            ApplyTimelineFilters();
            SaveSettings();
        }
    }

    private void WireSource(SourceDirectoryItem source)
    {
        source.PropertyChanged -= Source_PropertyChanged;
        source.PropertyChanged += Source_PropertyChanged;
    }

    private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (sender is SourceDirectoryItem source &&
            e.PropertyName is nameof(SourceDirectoryItem.Token) or nameof(SourceDirectoryItem.Color))
        {
            UpdateLogFilesForSource(source);
        }

        SaveSettings();
        if (e.PropertyName is nameof(SourceDirectoryItem.Token) or nameof(SourceDirectoryItem.Color))
        {
            _parseDebounce.Stop();
            _parseDebounce.Start();
        }
    }

    private void UpdateLogFilesForSource(SourceDirectoryItem source)
    {
        foreach (var file in LogFiles.Where(f => f.SourceId == source.Id))
        {
            file.SourceToken = source.Token;
            file.SourceColor = source.Color;
        }
    }

    private void WireLogFile(LogFileItem file)
    {
        file.PropertyChanged -= LogFile_PropertyChanged;
        file.PropertyChanged += LogFile_PropertyChanged;
    }

    private void LogFile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (sender is LogFileItem file)
        {
            _settings.FileStates[file.FileKey] = new LogFileState
            {
                Alias = file.Alias,
                Color = file.Color,
                IncludeInTimeline = file.IncludeInTimeline,
                IsVisible = file.IsVisible
            };
        }

        SaveSettings();
        if (e.PropertyName is nameof(LogFileItem.IncludeInTimeline)
            or nameof(LogFileItem.IsVisible)
            or nameof(LogFileItem.Alias)
            or nameof(LogFileItem.Color))
        {
            _parseDebounce.Stop();
            _parseDebounce.Start();
        }
    }

    private void WireFilter(RegexFilterItem filter)
    {
        filter.PropertyChanged -= Filter_PropertyChanged;
        filter.PropertyChanged += Filter_PropertyChanged;
    }

    private void Filter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        ApplyTimelineFilters();
        SaveSettings();
    }

    private void AddDefaultFilters()
    {
        for (var i = 0; i < 3; i++)
        {
            var filter = new RegexFilterItem
            {
                Name = $"Filter {i + 1}",
                Pattern = string.Empty,
                Color = Palette.At(i + 4),
                IsEnabled = false,
                IsRegex = false
            };
            WireFilter(filter);
            Filters.Add(filter);
        }
    }

    private void SaveSettings()
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            var state = new AppSettingsState
            {
                Sources = Sources.Select(s => new SourceDirectoryState
                {
                    Id = s.Id,
                    Token = s.Token,
                    Color = s.Color,
                    DirectoryPath = s.DirectoryPath
                }).ToList(),
                FileStates = LogFiles.ToDictionary(
                    f => f.FileKey,
                    f => new LogFileState
                    {
                        Alias = f.Alias,
                        Color = f.Color,
                        IncludeInTimeline = f.IncludeInTimeline,
                        IsVisible = f.IsVisible
                    },
                    StringComparer.OrdinalIgnoreCase),
                Filters = Filters.Select(f => new FilterState
                {
                    Id = f.Id,
                    Name = f.Name,
                    Pattern = f.Pattern,
                    Color = f.Color,
                    IsEnabled = f.IsEnabled,
                    IsRegex = f.IsRegex
                }).ToList(),
                Markers = _markers.ToList(),
                HiddenLineKeys = new HashSet<string>(_hiddenLineKeys, StringComparer.OrdinalIgnoreCase),
                ReviewedLineKey = _reviewedLineKey,
                ShowHiddenLines = ShowHiddenLines,
                ShowMarkers = ShowMarkers,
                ShowLevelError = ShowLevelError,
                ShowLevelWarning = ShowLevelWarning,
                ShowLevelDebug = ShowLevelDebug,
                ShowLevelLog = ShowLevelLog,
                ShowLevelOther = ShowLevelOther,
                ShowUnfilteredLines = ShowUnfilteredLines,
                AutoScrollMode = AutoScrollMode,
                SearchText = SearchText,
                SearchUseRegex = SearchUseRegex,
                ChecklistText = ChecklistText
            };
            _settings = state;
            _settingsStore.Save(state);
        }
        catch (Exception ex)
        {
            StatusText = $"Could not save settings: {ex.Message}";
        }
    }

    private static IEnumerable<string> SafeEnumerateLogs(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "output_log_*.txt", SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static DateTime SafeLastWrite(FileInfo info)
    {
        try
        {
            return info.Exists ? info.LastWriteTime : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }

    private static string NextPaletteColor(string current)
    {
        var index = Array.FindIndex(Palette.Colors, c => c.Equals(current, StringComparison.OrdinalIgnoreCase));
        return Palette.At(index + 1);
    }

    private TimelineEntry? GetContextTimelineEntry(object sender)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Parent is not ContextMenu menu ||
            menu.PlacementTarget is not FrameworkElement placement)
        {
            return null;
        }

        return placement.DataContext as TimelineEntry
            ?? TimelineList.SelectedItem as TimelineEntry
            ?? TimelineEntries.FirstOrDefault(e => e.IsSelectedForCopy);
    }

    private static TimelineEntry? GetTimelineEntryFromSender(object sender)
    {
        return (sender as FrameworkElement)?.DataContext as TimelineEntry
            ?? (sender as FrameworkElement)?.Tag as TimelineEntry;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                return typed;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private bool SetProperty<T>(ref T field, T value, string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
