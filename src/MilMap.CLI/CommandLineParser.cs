using System.CommandLine;

namespace MilMap.CLI;

/// <summary>
/// Builds and configures the command-line argument parser for MilMap.
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Creates and configures the root command with all options.
    /// </summary>
    public static RootCommand CreateRootCommand()
    {
        var outputArg = new Argument<FileInfo>("output")
        {
            Description = "Output file path for the generated map"
        };

        var mgrsOption = new Option<string?>("-m", "--mgrs")
        {
            Description = "MGRS coordinate or grid reference (e.g., '18TXM', '18TXM12345678')"
        };

        var boundsOption = new Option<string?>("-b", "--bounds")
        {
            Description = "Lat/lon bounding box as 'minLat,minLon,maxLat,maxLon' (e.g., '38.8,-77.1,38.9,-77.0')"
        };

        var installationOption = new Option<string?>("-i", "--installation")
        {
            Description = "Military installation name to look up (e.g., 'Fort Liberty')"
        };

        var scaleOption = new Option<int>("-s", "--scale")
        {
            Description = "Map scale denominator (e.g., 25000 for 1:25000)",
            DefaultValueFactory = _ => 25000
        };
        scaleOption.Validators.Add(result =>
        {
            int value = result.GetValueOrDefault<int>();
            if (value <= 0)
            {
                result.AddError("Scale must be a positive integer");
            }
        });

        var dpiOption = new Option<int>("-d", "--dpi")
        {
            Description = "Output resolution in dots per inch",
            DefaultValueFactory = _ => 300
        };
        dpiOption.Validators.Add(result =>
        {
            int value = result.GetValueOrDefault<int>();
            if (value <= 0 || value > 1200)
            {
                result.AddError("DPI must be between 1 and 1200");
            }
        });

        var formatOption = new Option<OutputFormat>("-f", "--format")
        {
            Description = "Output format (pdf, png, geotiff)",
            DefaultValueFactory = _ => OutputFormat.Pdf
        };

        var cacheDirOption = new Option<DirectoryInfo?>("-c", "--cache-dir")
        {
            Description = "Directory for caching downloaded tiles"
        };

        var rootCommand = new RootCommand("MilMap - Generate military-style topographic maps from OpenStreetMap data");
        rootCommand.Arguments.Add(outputArg);
        rootCommand.Options.Add(mgrsOption);
        rootCommand.Options.Add(boundsOption);
        rootCommand.Options.Add(installationOption);
        rootCommand.Options.Add(scaleOption);
        rootCommand.Options.Add(dpiOption);
        rootCommand.Options.Add(formatOption);
        rootCommand.Options.Add(cacheDirOption);

        // Add examples to the description
        rootCommand.Description += @"

Examples:
  milmap output.pdf --mgrs 18TXM
    Generate a map for MGRS grid square 18TXM

  milmap output.pdf --mgrs 18TXM12345678 --scale 50000
    Generate a 1:50000 scale map centered on a precise MGRS coordinate

  milmap output.png --bounds 38.8,-77.1,38.9,-77.0 --format png --dpi 150
    Generate a PNG map for a lat/lon bounding box

  milmap output.pdf --installation ""Fort Liberty"" --scale 100000
    Generate a map for a military installation";

        // Add validation for mutually exclusive region options
        rootCommand.Validators.Add(result =>
        {
            int regionOptionsCount = 0;
            if (result.GetValue(mgrsOption) is not null) regionOptionsCount++;
            if (result.GetValue(boundsOption) is not null) regionOptionsCount++;
            if (result.GetValue(installationOption) is not null) regionOptionsCount++;

            if (regionOptionsCount == 0)
            {
                result.AddError("One of --mgrs, --bounds, or --installation is required");
            }
            else if (regionOptionsCount > 1)
            {
                result.AddError("Options --mgrs, --bounds, and --installation are mutually exclusive");
            }
        });

        rootCommand.SetAction(parseResult =>
        {
            var options = new MapOptions
            {
                OutputPath = parseResult.GetValue(outputArg)!.FullName,
                Mgrs = parseResult.GetValue(mgrsOption),
                Bounds = parseResult.GetValue(boundsOption),
                Installation = parseResult.GetValue(installationOption),
                Scale = parseResult.GetValue(scaleOption),
                Dpi = parseResult.GetValue(dpiOption),
                Format = parseResult.GetValue(formatOption),
                CacheDir = parseResult.GetValue(cacheDirOption)?.FullName
            };

            Console.WriteLine($"Generating map: {options.OutputPath}");
            Console.WriteLine($"  Region: {options.GetRegionInputType()} = {options.Mgrs ?? options.Bounds ?? options.Installation}");
            Console.WriteLine($"  Scale: 1:{options.Scale}");
            Console.WriteLine($"  DPI: {options.Dpi}");
            Console.WriteLine($"  Format: {options.Format}");
            if (options.CacheDir is not null)
                Console.WriteLine($"  Cache: {options.CacheDir}");

            // TODO: Implement actual map generation
            return 0;
        });

        return rootCommand;
    }

    /// <summary>
    /// Parses command-line arguments and executes the handler.
    /// </summary>
    public static int Invoke(string[] args)
    {
        var rootCommand = CreateRootCommand();
        return rootCommand.Parse(args).Invoke();
    }
}
