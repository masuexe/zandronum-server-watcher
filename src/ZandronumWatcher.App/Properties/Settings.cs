using System.IO;
using System.Text.Json;

namespace ZandronumWatcher.App.Properties;

/// <summary>
/// Tiny JSON-backed replacement for the WPF settings designer, which the SDK does not
/// generate for this project type.
/// </summary>
internal sealed class Settings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ZandronumWatcher",
        "settings.json");

    private static Settings? _default;

    public static Settings Default => _default ??= Load();

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // Losing window position is not worth crashing over.
        }
    }

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
        }
        catch (JsonException)
        {
            // fall through to defaults
        }

        return new Settings();
    }
}
