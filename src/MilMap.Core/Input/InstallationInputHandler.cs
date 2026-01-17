using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MilMap.Core.Mgrs;

namespace MilMap.Core.Input;

/// <summary>
/// Result of looking up an installation.
/// </summary>
public record InstallationInputResult(
    BoundingBox BoundingBox,
    double CenterLat,
    double CenterLon,
    string InstallationName,
    string InstallationId,
    string Branch,
    string Type);

/// <summary>
/// Represents a military installation with its geographic bounds.
/// </summary>
public class Installation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = new();
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "";
    
    [JsonPropertyName("state")]
    public string State { get; set; } = "";
    
    [JsonPropertyName("boundingBox")]
    public InstallationBoundingBox BoundingBox { get; set; } = new();
}

/// <summary>
/// Bounding box for an installation as stored in JSON.
/// </summary>
public class InstallationBoundingBox
{
    [JsonPropertyName("minLat")]
    public double MinLat { get; set; }
    
    [JsonPropertyName("maxLat")]
    public double MaxLat { get; set; }
    
    [JsonPropertyName("minLon")]
    public double MinLon { get; set; }
    
    [JsonPropertyName("maxLon")]
    public double MaxLon { get; set; }
}

/// <summary>
/// Database structure for installations JSON.
/// </summary>
internal class InstallationDatabase
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
    
    [JsonPropertyName("installations")]
    public List<Installation> Installations { get; set; } = new();
}

/// <summary>
/// Handles military installation name input and converts to bounding boxes.
/// </summary>
public static class InstallationInputHandler
{
    private static List<Installation>? _installations;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets all available installations.
    /// </summary>
    public static IReadOnlyList<Installation> GetAllInstallations()
    {
        EnsureLoaded();
        return _installations!;
    }

    /// <summary>
    /// Looks up an installation by name, alias, or ID.
    /// </summary>
    /// <param name="query">Installation name, alias, or ID to search for</param>
    /// <returns>The installation result if found</returns>
    /// <exception cref="ArgumentException">If the installation is not found</exception>
    public static InstallationInputResult Lookup(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        EnsureLoaded();

        var installation = FindInstallation(query);
        if (installation == null)
            throw new ArgumentException($"Installation not found: {query}", nameof(query));

        return ToResult(installation);
    }

    /// <summary>
    /// Tries to look up an installation by name, alias, or ID.
    /// </summary>
    /// <param name="query">Installation name, alias, or ID to search for</param>
    /// <param name="result">The result if found</param>
    /// <returns>True if found, false otherwise</returns>
    public static bool TryLookup(string query, out InstallationInputResult? result)
    {
        result = null;
        
        if (string.IsNullOrWhiteSpace(query))
            return false;

        EnsureLoaded();

        var installation = FindInstallation(query);
        if (installation == null)
            return false;

        result = ToResult(installation);
        return true;
    }

    /// <summary>
    /// Searches for installations matching a partial query.
    /// </summary>
    /// <param name="query">Partial name or alias to search for</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>Matching installations</returns>
    public static IEnumerable<Installation> Search(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<Installation>();

        EnsureLoaded();

        var normalized = NormalizeQuery(query);

        return _installations!
            .Where(i => MatchesQuery(i, normalized))
            .Take(maxResults);
    }

    /// <summary>
    /// Gets installations filtered by branch.
    /// </summary>
    /// <param name="branch">Military branch (Army, Navy, Air Force, Marines, Space Force, Coast Guard, Joint)</param>
    public static IEnumerable<Installation> GetByBranch(string branch)
    {
        EnsureLoaded();
        return _installations!.Where(i => 
            i.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets installations filtered by state.
    /// </summary>
    /// <param name="state">Two-letter state code</param>
    public static IEnumerable<Installation> GetByState(string state)
    {
        EnsureLoaded();
        return _installations!.Where(i => 
            i.State.Equals(state, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets installations filtered by type.
    /// </summary>
    /// <param name="type">Installation type (fort, camp, base, etc.)</param>
    public static IEnumerable<Installation> GetByType(string type)
    {
        EnsureLoaded();
        return _installations!.Where(i => 
            i.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a query matches a known installation.
    /// </summary>
    public static bool IsInstallation(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        EnsureLoaded();
        return FindInstallation(query) != null;
    }

    private static void EnsureLoaded()
    {
        if (_installations != null)
            return;

        lock (_lock)
        {
            if (_installations != null)
                return;

            _installations = LoadInstallations();
        }
    }

    private static List<Installation> LoadInstallations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "MilMap.Core.Input.Data.installations.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Fall back to file system for development
            var assemblyDir = Path.GetDirectoryName(assembly.Location) ?? ".";
            var jsonPath = Path.Combine(assemblyDir, "Input", "Data", "installations.json");
            
            // Try relative path from assembly
            if (!File.Exists(jsonPath))
            {
                // Try source location
                var sourceDir = Path.GetDirectoryName(typeof(InstallationInputHandler).Assembly.Location);
                jsonPath = Path.Combine(sourceDir ?? ".", "..", "..", "..", "Input", "Data", "installations.json");
            }
            
            if (File.Exists(jsonPath))
            {
                var jsonContent = File.ReadAllText(jsonPath);
                var db = JsonSerializer.Deserialize<InstallationDatabase>(jsonContent);
                return db?.Installations ?? new List<Installation>();
            }
            
            throw new InvalidOperationException($"Could not find installations database resource: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var database = JsonSerializer.Deserialize<InstallationDatabase>(json);
        return database?.Installations ?? new List<Installation>();
    }

    private static Installation? FindInstallation(string query)
    {
        var normalized = NormalizeQuery(query);

        // Exact match on ID
        var byId = _installations!.FirstOrDefault(i => 
            NormalizeQuery(i.Id) == normalized);
        if (byId != null) return byId;

        // Exact match on name
        var byName = _installations!.FirstOrDefault(i => 
            NormalizeQuery(i.Name) == normalized);
        if (byName != null) return byName;

        // Exact match on alias
        var byAlias = _installations!.FirstOrDefault(i => 
            i.Aliases.Any(a => NormalizeQuery(a) == normalized));
        if (byAlias != null) return byAlias;

        // Partial match on name (starts with)
        var partial = _installations!.FirstOrDefault(i => 
            NormalizeQuery(i.Name).StartsWith(normalized));
        if (partial != null) return partial;

        // Partial match on alias
        return _installations!.FirstOrDefault(i => 
            i.Aliases.Any(a => NormalizeQuery(a).StartsWith(normalized)));
    }

    private static bool MatchesQuery(Installation installation, string normalizedQuery)
    {
        if (NormalizeQuery(installation.Id).Contains(normalizedQuery))
            return true;
        if (NormalizeQuery(installation.Name).Contains(normalizedQuery))
            return true;
        if (installation.Aliases.Any(a => NormalizeQuery(a).Contains(normalizedQuery)))
            return true;
        return false;
    }

    private static string NormalizeQuery(string query)
    {
        return query
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace(".", "")
            .Replace("'", "");
    }

    private static InstallationInputResult ToResult(Installation installation)
    {
        var box = new BoundingBox(
            installation.BoundingBox.MinLat,
            installation.BoundingBox.MaxLat,
            installation.BoundingBox.MinLon,
            installation.BoundingBox.MaxLon);

        return new InstallationInputResult(
            box,
            box.CenterLat,
            box.CenterLon,
            installation.Name,
            installation.Id,
            installation.Branch,
            installation.Type);
    }
}
