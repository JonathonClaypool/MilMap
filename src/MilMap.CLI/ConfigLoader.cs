using System;
using System.IO;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MilMap.CLI;

/// <summary>
/// Configuration settings that can be loaded from a file.
/// </summary>
public class MilMapConfig
{
    /// <summary>
    /// Default map scale denominator (e.g., 25000 for 1:25000).
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Default output DPI.
    /// </summary>
    public int? Dpi { get; set; }

    /// <summary>
    /// Default output format (pdf, png, geotiff).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Directory for caching downloaded tiles.
    /// </summary>
    public string? CacheDir { get; set; }

    /// <summary>
    /// Default MGRS coordinate or grid reference.
    /// </summary>
    public string? Mgrs { get; set; }

    /// <summary>
    /// Default lat/lon bounding box.
    /// </summary>
    public string? Bounds { get; set; }

    /// <summary>
    /// Default military installation name.
    /// </summary>
    public string? Installation { get; set; }

    /// <summary>
    /// Output file path pattern (can include placeholders).
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Applies this configuration to MapOptions, using config values as defaults
    /// where command-line values are not specified.
    /// </summary>
    public MapOptions ApplyTo(MapOptions options)
    {
        return options with
        {
            Scale = options.Scale != 25000 ? options.Scale : (Scale ?? 25000),
            Dpi = options.Dpi != 300 ? options.Dpi : (Dpi ?? 300),
            Format = options.Format != OutputFormat.Pdf ? options.Format : ParseFormat(Format) ?? OutputFormat.Pdf,
            CacheDir = options.CacheDir ?? CacheDir,
            Mgrs = options.Mgrs ?? Mgrs,
            Bounds = options.Bounds ?? Bounds,
            Installation = options.Installation ?? Installation
        };
    }

    private static OutputFormat? ParseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return null;

        return format.ToLowerInvariant() switch
        {
            "pdf" => OutputFormat.Pdf,
            "png" => OutputFormat.Png,
            "geotiff" or "tiff" or "tif" => OutputFormat.GeoTiff,
            _ => null
        };
    }
}

/// <summary>
/// Loads and manages MilMap configuration files.
/// </summary>
public static class ConfigLoader
{
    private static readonly string[] ConfigFileNames = {
        "milmap.yaml",
        "milmap.yml",
        "milmap.json",
        ".milmap.yaml",
        ".milmap.yml",
        ".milmap.json"
    };

    /// <summary>
    /// Loads configuration from a specific file path.
    /// </summary>
    /// <param name="path">Path to the configuration file</param>
    /// <returns>Loaded configuration</returns>
    /// <exception cref="FileNotFoundException">If the file doesn't exist</exception>
    /// <exception cref="InvalidOperationException">If the file format is not supported</exception>
    public static MilMapConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Configuration file not found: {path}");

        var content = File.ReadAllText(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".yaml" or ".yml" => ParseYaml(content),
            ".json" => ParseJson(content),
            _ => throw new InvalidOperationException($"Unsupported config file format: {extension}")
        };
    }

    /// <summary>
    /// Tries to load configuration from a specific file path.
    /// </summary>
    /// <param name="path">Path to the configuration file</param>
    /// <param name="config">The loaded configuration if successful</param>
    /// <returns>True if configuration was loaded successfully</returns>
    public static bool TryLoadFromFile(string path, out MilMapConfig? config)
    {
        config = null;
        try
        {
            if (!File.Exists(path))
                return false;

            config = LoadFromFile(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Discovers and loads configuration from standard locations.
    /// Searches in order: current directory, parent directories up to root, home directory.
    /// </summary>
    /// <returns>Loaded configuration or null if no config file found</returns>
    public static MilMapConfig? Discover()
    {
        // Search current directory and parents
        var searchDir = Directory.GetCurrentDirectory();
        while (searchDir != null)
        {
            foreach (var fileName in ConfigFileNames)
            {
                var path = Path.Combine(searchDir, fileName);
                if (TryLoadFromFile(path, out var config))
                    return config;
            }
            searchDir = Directory.GetParent(searchDir)?.FullName;
        }

        // Search home directory
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var fileName in ConfigFileNames)
        {
            var path = Path.Combine(homeDir, fileName);
            if (TryLoadFromFile(path, out var config))
                return config;
        }

        // Search .config/milmap directory
        var configDir = Path.Combine(homeDir, ".config", "milmap");
        if (Directory.Exists(configDir))
        {
            foreach (var fileName in ConfigFileNames)
            {
                var path = Path.Combine(configDir, fileName);
                if (TryLoadFromFile(path, out var config))
                    return config;
            }
        }

        return null;
    }

    /// <summary>
    /// Discovers configuration and returns it, or an empty config if none found.
    /// </summary>
    public static MilMapConfig DiscoverOrDefault()
    {
        return Discover() ?? new MilMapConfig();
    }

    /// <summary>
    /// Gets the path where a discovered config file was found.
    /// </summary>
    public static string? GetDiscoveredPath()
    {
        var searchDir = Directory.GetCurrentDirectory();
        while (searchDir != null)
        {
            foreach (var fileName in ConfigFileNames)
            {
                var path = Path.Combine(searchDir, fileName);
                if (File.Exists(path))
                    return path;
            }
            searchDir = Directory.GetParent(searchDir)?.FullName;
        }

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var fileName in ConfigFileNames)
        {
            var path = Path.Combine(homeDir, fileName);
            if (File.Exists(path))
                return path;
        }

        var configDir = Path.Combine(homeDir, ".config", "milmap");
        if (Directory.Exists(configDir))
        {
            foreach (var fileName in ConfigFileNames)
            {
                var path = Path.Combine(configDir, fileName);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Generates a sample configuration file content.
    /// </summary>
    /// <param name="format">Output format (yaml or json)</param>
    public static string GenerateSample(string format = "yaml")
    {
        var sample = new MilMapConfig
        {
            Scale = 25000,
            Dpi = 300,
            Format = "pdf",
            CacheDir = "~/.cache/milmap"
        };

        return format.ToLowerInvariant() switch
        {
            "yaml" or "yml" => SerializeYaml(sample),
            "json" => SerializeJson(sample),
            _ => SerializeYaml(sample)
        };
    }

    private static MilMapConfig ParseYaml(string content)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<MilMapConfig>(content) ?? new MilMapConfig();
    }

    private static MilMapConfig ParseJson(string content)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        return JsonSerializer.Deserialize<MilMapConfig>(content, options) ?? new MilMapConfig();
    }

    private static string SerializeYaml(MilMapConfig config)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return $"# MilMap Configuration File\n# See documentation for all available options\n\n{serializer.Serialize(config)}";
    }

    private static string SerializeJson(MilMapConfig config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(config, options);
    }
}
