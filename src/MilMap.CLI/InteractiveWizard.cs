using System;
using System.Collections.Generic;
using System.Linq;
using MilMap.Core.Input;

namespace MilMap.CLI;

/// <summary>
/// Interactive wizard for guided map generation.
/// </summary>
public class InteractiveWizard
{
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public InteractiveWizard() : this(Console.In, Console.Out) { }

    public InteractiveWizard(TextReader input, TextWriter output)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// Runs the interactive wizard and returns the configured map options.
    /// </summary>
    /// <returns>The configured options, or null if cancelled.</returns>
    public MapOptions? Run()
    {
        PrintHeader();

        var options = new MapOptions();

        // Step 1: Choose region input method
        _output.WriteLine("Step 1: Select region input method");
        _output.WriteLine();
        var regionMethod = PromptChoice(
            "How would you like to specify the map region?",
            new[]
            {
                ("MGRS", "Enter MGRS grid reference (e.g., 18TXM)"),
                ("Bounds", "Enter latitude/longitude bounding box"),
                ("Installation", "Search for a military installation"),
            });

        if (regionMethod == null) return null;

        switch (regionMethod)
        {
            case "MGRS":
                var mgrs = PromptMgrs();
                if (mgrs == null) return null;
                options.Mgrs = mgrs;
                break;

            case "Bounds":
                var bounds = PromptBounds();
                if (bounds == null) return null;
                options.Bounds = bounds;
                break;

            case "Installation":
                var installation = PromptInstallation();
                if (installation == null) return null;
                options.Installation = installation;
                break;
        }

        _output.WriteLine();

        // Step 2: Choose scale
        _output.WriteLine("Step 2: Select map scale");
        _output.WriteLine();
        var scaleChoice = PromptChoice(
            "Which scale would you like?",
            new[]
            {
                ("10000", "1:10,000 - Very detailed (good for small areas)"),
                ("25000", "1:25,000 - Standard tactical (recommended)"),
                ("50000", "1:50,000 - Operational planning"),
                ("100000", "1:100,000 - Strategic overview"),
                ("Custom", "Enter a custom scale"),
            });

        if (scaleChoice == null) return null;

        if (scaleChoice == "Custom")
        {
            var customScale = PromptInt("Enter scale denominator (e.g., 35000 for 1:35,000)", 1000, 500000);
            if (customScale == null) return null;
            options.Scale = customScale.Value;
        }
        else
        {
            options.Scale = int.Parse(scaleChoice);
        }

        _output.WriteLine();

        // Step 3: Choose output format
        _output.WriteLine("Step 3: Select output format");
        _output.WriteLine();
        var formatChoice = PromptChoice(
            "Which format would you like?",
            new[]
            {
                ("pdf", "PDF - Best for printing"),
                ("png", "PNG - Image file"),
                ("geotiff", "GeoTIFF - Georeferenced image for GIS"),
            });

        if (formatChoice == null) return null;

        options.Format = Enum.Parse<OutputFormat>(formatChoice, ignoreCase: true);
        _output.WriteLine();

        // Step 4: Choose DPI
        _output.WriteLine("Step 4: Select output resolution");
        _output.WriteLine();
        var dpiChoice = PromptChoice(
            "Which resolution would you like?",
            new[]
            {
                ("150", "150 DPI - Screen viewing"),
                ("300", "300 DPI - Standard print quality (recommended)"),
                ("600", "600 DPI - High quality print"),
                ("Custom", "Enter a custom DPI"),
            });

        if (dpiChoice == null) return null;

        if (dpiChoice == "Custom")
        {
            var customDpi = PromptInt("Enter DPI (dots per inch)", 72, 1200);
            if (customDpi == null) return null;
            options.Dpi = customDpi.Value;
        }
        else
        {
            options.Dpi = int.Parse(dpiChoice);
        }

        _output.WriteLine();

        // Step 5: Output file
        _output.WriteLine("Step 5: Specify output file");
        _output.WriteLine();

        string defaultExtension = options.Format switch
        {
            OutputFormat.Pdf => ".pdf",
            OutputFormat.Png => ".png",
            OutputFormat.GeoTiff => ".tif",
            _ => ".pdf"
        };

        var outputPath = PromptString($"Enter output filename (default: map{defaultExtension})", $"map{defaultExtension}");
        if (outputPath == null) return null;

        // Ensure correct extension
        if (!outputPath.EndsWith(defaultExtension, StringComparison.OrdinalIgnoreCase))
        {
            outputPath += defaultExtension;
        }

        options.OutputPath = outputPath;
        _output.WriteLine();

        // Summary
        PrintSummary(options);

        var confirm = PromptConfirm("Generate map with these settings?");
        if (!confirm)
        {
            _output.WriteLine("Map generation cancelled.");
            return null;
        }

        return options;
    }

    private void PrintHeader()
    {
        _output.WriteLine();
        _output.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║                    MilMap Interactive Wizard                    ║");
        _output.WriteLine("║          Generate military-style topographic maps               ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        _output.WriteLine();
        _output.WriteLine("This wizard will guide you through creating a custom map.");
        _output.WriteLine("Press Ctrl+C at any time to cancel.");
        _output.WriteLine();
    }

    private void PrintSummary(MapOptions options)
    {
        _output.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        _output.WriteLine("│                         Summary                                 │");
        _output.WriteLine("├────────────────────────────────────────────────────────────────┤");

        if (!string.IsNullOrEmpty(options.Mgrs))
            _output.WriteLine($"│  Region:     MGRS {options.Mgrs,-46} │");
        else if (!string.IsNullOrEmpty(options.Bounds))
            _output.WriteLine($"│  Region:     Bounds {options.Bounds,-44} │");
        else if (!string.IsNullOrEmpty(options.Installation))
            _output.WriteLine($"│  Region:     {options.Installation,-51} │");

        _output.WriteLine($"│  Scale:      1:{options.Scale,-51} │");
        _output.WriteLine($"│  Format:     {options.Format,-51} │");
        _output.WriteLine($"│  Resolution: {options.Dpi} DPI{new string(' ', 46 - options.Dpi.ToString().Length)} │");
        _output.WriteLine($"│  Output:     {options.OutputPath,-51} │");
        _output.WriteLine("└────────────────────────────────────────────────────────────────┘");
        _output.WriteLine();
    }

    private string? PromptChoice(string prompt, (string value, string description)[] choices)
    {
        _output.WriteLine(prompt);
        _output.WriteLine();

        for (int i = 0; i < choices.Length; i++)
        {
            _output.WriteLine($"  [{i + 1}] {choices[i].description}");
        }

        _output.WriteLine();
        _output.Write($"Enter choice (1-{choices.Length}): ");

        var line = _input.ReadLine();
        if (string.IsNullOrEmpty(line)) return null;

        if (int.TryParse(line.Trim(), out int choice) && choice >= 1 && choice <= choices.Length)
        {
            return choices[choice - 1].value;
        }

        _output.WriteLine($"Invalid choice. Please enter a number between 1 and {choices.Length}.");
        return PromptChoice(prompt, choices);
    }

    private string? PromptMgrs()
    {
        _output.Write("Enter MGRS grid reference (e.g., 18TXM, 18TXM8546): ");
        var line = _input.ReadLine();

        if (string.IsNullOrEmpty(line)) return null;

        var mgrs = line.Trim().ToUpperInvariant();

        // Basic validation
        if (mgrs.Length < 3)
        {
            _output.WriteLine("Invalid MGRS format. Please try again.");
            return PromptMgrs();
        }

        return mgrs;
    }

    private string? PromptBounds()
    {
        _output.WriteLine("Enter bounding box coordinates:");
        _output.WriteLine("  Format: minLat,minLon,maxLat,maxLon");
        _output.WriteLine("  Example: 38.8,-77.1,38.9,-77.0");
        _output.Write("Bounds: ");

        var line = _input.ReadLine();
        if (string.IsNullOrEmpty(line)) return null;

        var bounds = line.Trim();
        var parts = bounds.Split(',');

        if (parts.Length != 4 || !parts.All(p => double.TryParse(p.Trim(), out _)))
        {
            _output.WriteLine("Invalid format. Please enter 4 comma-separated numbers.");
            return PromptBounds();
        }

        return bounds;
    }

    private string? PromptInstallation()
    {
        _output.Write("Enter installation name or search term: ");
        var line = _input.ReadLine();

        if (string.IsNullOrEmpty(line)) return null;

        var query = line.Trim();

        // Search for matching installations
        var matches = InstallationInputHandler.Search(query, 10).ToList();

        if (matches.Count == 0)
        {
            _output.WriteLine("No installations found matching that name.");
            var retry = PromptConfirm("Would you like to try again?");
            return retry ? PromptInstallation() : null;
        }

        if (matches.Count == 1)
        {
            _output.WriteLine($"Found: {matches[0].Name} ({matches[0].Branch}, {matches[0].State})");
            var confirm = PromptConfirm("Use this installation?");
            return confirm ? matches[0].Name : PromptInstallation();
        }

        _output.WriteLine();
        _output.WriteLine("Multiple installations found:");
        _output.WriteLine();

        for (int i = 0; i < matches.Count; i++)
        {
            _output.WriteLine($"  [{i + 1}] {matches[i].Name} ({matches[i].Branch}, {matches[i].State})");
        }

        _output.WriteLine();
        _output.Write($"Enter choice (1-{matches.Count}), or 0 to search again: ");

        var choiceLine = _input.ReadLine();
        if (string.IsNullOrEmpty(choiceLine)) return null;

        if (int.TryParse(choiceLine.Trim(), out int choice))
        {
            if (choice == 0) return PromptInstallation();
            if (choice >= 1 && choice <= matches.Count)
            {
                return matches[choice - 1].Name;
            }
        }

        _output.WriteLine("Invalid choice.");
        return PromptInstallation();
    }

    private int? PromptInt(string prompt, int min, int max)
    {
        _output.Write($"{prompt} ({min}-{max}): ");
        var line = _input.ReadLine();

        if (string.IsNullOrEmpty(line)) return null;

        if (int.TryParse(line.Trim(), out int value) && value >= min && value <= max)
        {
            return value;
        }

        _output.WriteLine($"Please enter a number between {min} and {max}.");
        return PromptInt(prompt, min, max);
    }

    private string? PromptString(string prompt, string defaultValue)
    {
        _output.Write($"{prompt}: ");
        var line = _input.ReadLine();

        if (string.IsNullOrEmpty(line))
        {
            return defaultValue;
        }

        return line.Trim();
    }

    private bool PromptConfirm(string prompt)
    {
        _output.Write($"{prompt} (Y/n): ");
        var line = _input.ReadLine();

        if (string.IsNullOrEmpty(line)) return true;

        var answer = line.Trim().ToLowerInvariant();
        return answer != "n" && answer != "no";
    }
}
