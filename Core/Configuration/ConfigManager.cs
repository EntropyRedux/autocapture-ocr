using AutoCaptureOCR.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoCaptureOCR.Core.Configuration;

/// <summary>
/// Manages application configuration using YAML
/// </summary>
public class ConfigManager
{
    private readonly string configPath;

    public ConfigManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoCaptureOCR"
        );

        Directory.CreateDirectory(appDataPath);
        configPath = Path.Combine(appDataPath, "config.yaml");
    }

    public AppConfig LoadConfig()
    {
        try
        {
            if (!File.Exists(configPath))
            {
                var defaultConfig = AppConfig.GetDefault();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return deserializer.Deserialize<AppConfig>(yaml);
        }
        catch
        {
            return AppConfig.GetDefault();
        }
    }

    public void SaveConfig(AppConfig config)
    {
        try
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(config);
            File.WriteAllText(configPath, yaml);
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}
