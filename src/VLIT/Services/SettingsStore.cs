using System.IO;
using System.Text.Json;

namespace VLIT.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsDirectory { get; }
    public string SettingsPath { get; }

    public SettingsStore()
    {
        SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VLIT");
        SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    }

    public AppSettingsState Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettingsState();
            }

            using var stream = File.OpenRead(SettingsPath);
            var state = JsonSerializer.Deserialize<AppSettingsState>(stream, JsonOptions) ?? new AppSettingsState();
            return Normalize(state);
        }
        catch
        {
            return new AppSettingsState();
        }
    }

    public void Save(AppSettingsState state)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var tempPath = SettingsPath + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, state, JsonOptions);
        }

        if (File.Exists(SettingsPath))
        {
            File.Replace(tempPath, SettingsPath, null);
        }
        else
        {
            File.Move(tempPath, SettingsPath);
        }
    }

    private static AppSettingsState Normalize(AppSettingsState state)
    {
        var fileStates = new Dictionary<string, LogFileState>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in state.FileStates)
        {
            fileStates[NormalizePathKey(key)] = value;
        }

        state.FileStates = fileStates;
        state.HiddenLineKeys = new HashSet<string>(state.HiddenLineKeys, StringComparer.OrdinalIgnoreCase);
        return state;
    }

    private static string NormalizePathKey(string key)
    {
        try
        {
            return Path.GetFullPath(key).ToUpperInvariant();
        }
        catch
        {
            return key.ToUpperInvariant();
        }
    }
}
