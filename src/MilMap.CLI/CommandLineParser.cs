using System.CommandLine;
using MilMap.Core;
using MilMap.Core.Input;
using MilMap.Core.Mgrs;

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
            Description = "Military installation name to look up (e.g., 'Fort Liberty', 'JBLM')"
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

        var configOption = new Option<FileInfo?>("--config")
        {
            Description = "Path to configuration file (YAML or JSON)"
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
        rootCommand.Options.Add(configOption);

        // Add subcommands
        rootCommand.Subcommands.Add(CreateInstallationsCommand());
        rootCommand.Subcommands.Add(CreateConfigCommand());
        rootCommand.Subcommands.Add(CreateInteractiveCommand());

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
    Generate a map for a military installation

  milmap installations search ""Fort""
    Search for installations matching 'Fort'

  milmap installations list --branch Army
    List all Army installations

  milmap config show
    Show current configuration";

        // Add validation for mutually exclusive region options
        rootCommand.Validators.Add(result =>
        {
            // Load config to check for defaults
            var configPath = result.GetValue(configOption)?.FullName;
            MilMapConfig? config = null;
            if (configPath != null)
            {
                if (!ConfigLoader.TryLoadFromFile(configPath, out config))
                {
                    result.AddError($"Could not load configuration file: {configPath}");
                    return;
                }
            }
            else
            {
                config = ConfigLoader.Discover();
            }

            int regionOptionsCount = 0;
            if (result.GetValue(mgrsOption) is not null) regionOptionsCount++;
            if (result.GetValue(boundsOption) is not null) regionOptionsCount++;
            if (result.GetValue(installationOption) is not null) regionOptionsCount++;
            
            // Also check config for defaults
            if (config != null)
            {
                if (regionOptionsCount == 0 && config.Mgrs is not null) regionOptionsCount++;
                if (regionOptionsCount == 0 && config.Bounds is not null) regionOptionsCount++;
                if (regionOptionsCount == 0 && config.Installation is not null) regionOptionsCount++;
            }

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
            // Load configuration
            var configPath = parseResult.GetValue(configOption)?.FullName;
            MilMapConfig config;
            if (configPath != null)
            {
                config = ConfigLoader.LoadFromFile(configPath);
            }
            else
            {
                config = ConfigLoader.DiscoverOrDefault();
            }

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

            // Apply config defaults
            options = config.ApplyTo(options);

            // Resolve bounding box from input
            BoundingBox bounds;
            string regionDescription;

            if (options.Installation is not null)
            {
                var lookupResult = ResolveInstallation(options.Installation);
                if (lookupResult is null)
                    return 1;
                
                bounds = lookupResult.BoundingBox;
                regionDescription = $"{lookupResult.InstallationName} ({lookupResult.Branch})";
            }
            else if (options.Mgrs is not null)
            {
                var mgrsResult = MgrsInputHandler.Parse(options.Mgrs);
                bounds = mgrsResult.BoundingBox;
                regionDescription = $"MGRS {options.Mgrs}";
            }
            else if (options.Bounds is not null)
            {
                var boundsResult = LatLonInputHandler.Parse(options.Bounds);
                bounds = boundsResult.BoundingBox;
                regionDescription = $"Bounds {options.Bounds}";
            }
            else
            {
                Console.Error.WriteLine("Error: No region specified");
                return 1;
            }

            Console.WriteLine($"Generating map: {options.OutputPath}");
            Console.WriteLine($"  Region: {regionDescription}");
            Console.WriteLine($"  Bounds: {bounds.MinLat:F4},{bounds.MinLon:F4} to {bounds.MaxLat:F4},{bounds.MaxLon:F4}");
            Console.WriteLine($"  Scale: 1:{options.Scale}");
            Console.WriteLine($"  DPI: {options.Dpi}");
            Console.WriteLine($"  Format: {options.Format}");
            
            if (options.CacheDir is not null)
                Console.WriteLine($"  Cache: {options.CacheDir}");

            Console.WriteLine();

            // Generate the map
            var generatorOptions = new MapGeneratorOptions
            {
                Bounds = bounds,
                Scale = options.Scale,
                Dpi = options.Dpi,
                OutputPath = options.OutputPath,
                Format = options.Format switch
                {
                    OutputFormat.Pdf => MapOutputFormat.Pdf,
                    OutputFormat.Png => MapOutputFormat.Png,
                    OutputFormat.GeoTiff => MapOutputFormat.GeoTiff,
                    _ => MapOutputFormat.Pdf
                },
                Title = regionDescription,
                CacheDirectory = options.CacheDir
            };

            var progress = ConsoleProgress.ForSteps();
            
            using var generator = new MapGenerator(options.CacheDir);
            var result = generator.GenerateAsync(generatorOptions, progress).GetAwaiter().GetResult();

            if (result.Success)
            {
                progress.Complete($"Map saved to {result.OutputPath}");
                Console.WriteLine($"  Tiles: {result.TilesDownloaded} downloaded, {result.TilesFromCache} from cache");
                Console.WriteLine($"  Duration: {result.Duration.TotalSeconds:F1}s");
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.ErrorMessage}");
                return 1;
            }
        });

        return rootCommand;
    }

    /// <summary>
    /// Creates the installations subcommand for searching and listing installations.
    /// </summary>
    private static Command CreateInstallationsCommand()
    {
        var installationsCommand = new Command("installations", "Search and list military installations");

        // Search subcommand
        var searchArg = new Argument<string>("query")
        {
            Description = "Search query (name, alias, or partial match)"
        };
        var maxResultsOption = new Option<int>("--max", "-n")
        {
            Description = "Maximum number of results",
            DefaultValueFactory = _ => 10
        };
        
        var searchCommand = new Command("search", "Search for installations by name or alias");
        searchCommand.Arguments.Add(searchArg);
        searchCommand.Options.Add(maxResultsOption);
        searchCommand.SetAction(parseResult =>
        {
            var query = parseResult.GetValue(searchArg)!;
            var maxResults = parseResult.GetValue(maxResultsOption);
            
            var results = InstallationInputHandler.Search(query, maxResults).ToList();
            
            if (results.Count == 0)
            {
                Console.WriteLine($"No installations found matching '{query}'");
                return 1;
            }
            
            Console.WriteLine($"Found {results.Count} installation(s) matching '{query}':");
            Console.WriteLine();
            
            foreach (var installation in results)
            {
                PrintInstallation(installation);
            }
            
            return 0;
        });

        // List subcommand
        var branchOption = new Option<string?>("--branch")
        {
            Description = "Filter by branch (Army, Navy, Air Force, Marines, Space Force, Coast Guard, Joint)"
        };
        var stateOption = new Option<string?>("--state")
        {
            Description = "Filter by state (two-letter code, e.g., CA, TX, NC)"
        };
        var typeOption = new Option<string?>("--type")
        {
            Description = "Filter by type (fort, camp, base, air_force_base, naval_station, etc.)"
        };
        
        var listCommand = new Command("list", "List installations with optional filters");
        listCommand.Options.Add(branchOption);
        listCommand.Options.Add(stateOption);
        listCommand.Options.Add(typeOption);
        listCommand.SetAction(parseResult =>
        {
            var branch = parseResult.GetValue(branchOption);
            var state = parseResult.GetValue(stateOption);
            var type = parseResult.GetValue(typeOption);
            
            IEnumerable<Installation> results = InstallationInputHandler.GetAllInstallations();
            
            if (branch is not null)
                results = results.Where(i => i.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase));
            if (state is not null)
                results = results.Where(i => i.State.Equals(state, StringComparison.OrdinalIgnoreCase));
            if (type is not null)
                results = results.Where(i => i.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            
            var list = results.OrderBy(i => i.Name).ToList();
            
            if (list.Count == 0)
            {
                Console.WriteLine("No installations found matching the filters");
                return 1;
            }
            
            Console.WriteLine($"Found {list.Count} installation(s):");
            Console.WriteLine();
            
            foreach (var installation in list)
            {
                PrintInstallation(installation);
            }
            
            return 0;
        });

        // Show subcommand
        var showArg = new Argument<string>("name")
        {
            Description = "Installation name, alias, or ID"
        };
        
        var showCommand = new Command("show", "Show details for a specific installation");
        showCommand.Arguments.Add(showArg);
        showCommand.SetAction(parseResult =>
        {
            var query = parseResult.GetValue(showArg)!;
            
            if (!InstallationInputHandler.TryLookup(query, out var result))
            {
                Console.WriteLine($"Installation not found: {query}");
                
                // Suggest similar matches
                var suggestions = InstallationInputHandler.Search(query, 5).ToList();
                if (suggestions.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Did you mean:");
                    foreach (var s in suggestions)
                    {
                        Console.WriteLine($"  - {s.Name}");
                    }
                }
                return 1;
            }

            var installation = InstallationInputHandler.GetAllInstallations()
                .First(i => i.Id == result!.InstallationId);
            
            Console.WriteLine($"Name:     {installation.Name}");
            Console.WriteLine($"ID:       {installation.Id}");
            Console.WriteLine($"Branch:   {installation.Branch}");
            Console.WriteLine($"Type:     {installation.Type}");
            Console.WriteLine($"State:    {installation.State}");
            if (installation.Aliases.Count > 0)
                Console.WriteLine($"Aliases:  {string.Join(", ", installation.Aliases)}");
            Console.WriteLine($"Bounds:   {installation.BoundingBox.MinLat:F4},{installation.BoundingBox.MinLon:F4} to {installation.BoundingBox.MaxLat:F4},{installation.BoundingBox.MaxLon:F4}");
            Console.WriteLine($"Center:   {result!.CenterLat:F4},{result.CenterLon:F4}");
            
            return 0;
        });

        installationsCommand.Subcommands.Add(searchCommand);
        installationsCommand.Subcommands.Add(listCommand);
        installationsCommand.Subcommands.Add(showCommand);

        return installationsCommand;
    }

    private static void PrintInstallation(Installation installation)
    {
        Console.WriteLine($"  {installation.Name}");
        Console.WriteLine($"    ID: {installation.Id}  Branch: {installation.Branch}  State: {installation.State}");
        if (installation.Aliases.Count > 0)
            Console.WriteLine($"    Aliases: {string.Join(", ", installation.Aliases)}");
        Console.WriteLine();
    }

    /// <summary>
    /// Resolves an installation query with fuzzy matching and user feedback.
    /// </summary>
    private static InstallationInputResult? ResolveInstallation(string query)
    {
        // Try exact/fuzzy lookup first
        if (InstallationInputHandler.TryLookup(query, out var result))
        {
            return result;
        }
        
        // Check for multiple possible matches
        var matches = InstallationInputHandler.Search(query, 5).ToList();
        
        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"Error: Installation not found: '{query}'");
            Console.Error.WriteLine("Use 'milmap installations search <query>' to find installations.");
            return null;
        }
        
        if (matches.Count == 1)
        {
            // Single match found, use it
            return InstallationInputHandler.Lookup(matches[0].Id);
        }
        
        // Multiple matches - show options
        Console.Error.WriteLine($"Error: Ambiguous installation name: '{query}'");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Multiple matches found:");
        foreach (var match in matches)
        {
            Console.Error.WriteLine($"  - {match.Name} ({match.Branch}, {match.State})");
        }
        Console.Error.WriteLine();
        Console.Error.WriteLine("Please be more specific or use the installation ID.");
        return null;
    }

    /// <summary>
    /// Creates the config subcommand for managing configuration files.
    /// </summary>
    private static Command CreateConfigCommand()
    {
        var configCommand = new Command("config", "Manage configuration files");

        // Show subcommand
        var showCommand = new Command("show", "Show current configuration");
        showCommand.SetAction(_ =>
        {
            var configPath = ConfigLoader.GetDiscoveredPath();
            if (configPath == null)
            {
                Console.WriteLine("No configuration file found.");
                Console.WriteLine();
                Console.WriteLine("Searched locations:");
                Console.WriteLine("  - Current directory and parent directories");
                Console.WriteLine("  - Home directory (~/)");
                Console.WriteLine("  - ~/.config/milmap/");
                Console.WriteLine();
                Console.WriteLine("Create a config file with: milmap config init");
                return 0;
            }

            Console.WriteLine($"Configuration file: {configPath}");
            Console.WriteLine();
            
            var config = ConfigLoader.LoadFromFile(configPath);
            
            if (config.Scale.HasValue)
                Console.WriteLine($"  scale: {config.Scale}");
            if (config.Dpi.HasValue)
                Console.WriteLine($"  dpi: {config.Dpi}");
            if (!string.IsNullOrEmpty(config.Format))
                Console.WriteLine($"  format: {config.Format}");
            if (!string.IsNullOrEmpty(config.CacheDir))
                Console.WriteLine($"  cacheDir: {config.CacheDir}");
            if (!string.IsNullOrEmpty(config.Mgrs))
                Console.WriteLine($"  mgrs: {config.Mgrs}");
            if (!string.IsNullOrEmpty(config.Bounds))
                Console.WriteLine($"  bounds: {config.Bounds}");
            if (!string.IsNullOrEmpty(config.Installation))
                Console.WriteLine($"  installation: {config.Installation}");
            if (!string.IsNullOrEmpty(config.OutputPath))
                Console.WriteLine($"  outputPath: {config.OutputPath}");

            return 0;
        });

        // Init subcommand
        var formatOption = new Option<string>("--format", "-f")
        {
            Description = "Config file format (yaml or json)",
            DefaultValueFactory = _ => "yaml"
        };
        var outputOption = new Option<string>("--output", "-o")
        {
            Description = "Output file path",
            DefaultValueFactory = _ => "milmap.yaml"
        };
        
        var initCommand = new Command("init", "Create a new configuration file");
        initCommand.Options.Add(formatOption);
        initCommand.Options.Add(outputOption);
        initCommand.SetAction(parseResult =>
        {
            var format = parseResult.GetValue(formatOption)!;
            var output = parseResult.GetValue(outputOption)!;
            
            if (File.Exists(output))
            {
                Console.Error.WriteLine($"Error: File already exists: {output}");
                return 1;
            }

            var content = ConfigLoader.GenerateSample(format);
            File.WriteAllText(output, content);
            Console.WriteLine($"Created configuration file: {output}");
            return 0;
        });

        // Path subcommand
        var pathCommand = new Command("path", "Show path to discovered configuration file");
        pathCommand.SetAction(_ =>
        {
            var configPath = ConfigLoader.GetDiscoveredPath();
            if (configPath == null)
            {
                Console.Error.WriteLine("No configuration file found.");
                return 1;
            }
            Console.WriteLine(configPath);
            return 0;
        });

        configCommand.Subcommands.Add(showCommand);
        configCommand.Subcommands.Add(initCommand);
        configCommand.Subcommands.Add(pathCommand);

        return configCommand;
    }

    /// <summary>
    /// Creates the interactive subcommand for guided map generation.
    /// </summary>
    private static Command CreateInteractiveCommand()
    {
        var interactiveCommand = new Command("interactive", "Launch interactive wizard for guided map generation");
        interactiveCommand.Aliases.Add("wizard");
        interactiveCommand.Aliases.Add("i");

        interactiveCommand.SetAction(_ =>
        {
            var wizard = new InteractiveWizard();
            var options = wizard.Run();

            if (options == null)
            {
                return 1;
            }

            // Resolve bounding box from input
            BoundingBox bounds;
            string regionDescription;

            if (options.Installation is not null)
            {
                if (!InstallationInputHandler.TryLookup(options.Installation, out var lookupResult) || lookupResult is null)
                {
                    Console.Error.WriteLine($"Error: Installation not found: {options.Installation}");
                    return 1;
                }
                bounds = lookupResult.BoundingBox;
                regionDescription = $"{lookupResult.InstallationName} ({lookupResult.Branch})";
            }
            else if (options.Mgrs is not null)
            {
                var mgrsResult = MgrsInputHandler.Parse(options.Mgrs);
                bounds = mgrsResult.BoundingBox;
                regionDescription = $"MGRS {options.Mgrs}";
            }
            else if (options.Bounds is not null)
            {
                var boundsResult = LatLonInputHandler.Parse(options.Bounds);
                bounds = boundsResult.BoundingBox;
                regionDescription = $"Bounds {options.Bounds}";
            }
            else
            {
                Console.Error.WriteLine("Error: No region specified");
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine($"Generating map: {options.OutputPath}");
            Console.WriteLine($"  Region: {regionDescription}");
            Console.WriteLine($"  Scale: 1:{options.Scale}");
            Console.WriteLine($"  DPI: {options.Dpi}");
            Console.WriteLine($"  Format: {options.Format}");
            Console.WriteLine();

            var generatorOptions = new MapGeneratorOptions
            {
                Bounds = bounds,
                Scale = options.Scale,
                Dpi = options.Dpi,
                OutputPath = options.OutputPath,
                Format = options.Format switch
                {
                    OutputFormat.Pdf => MapOutputFormat.Pdf,
                    OutputFormat.Png => MapOutputFormat.Png,
                    OutputFormat.GeoTiff => MapOutputFormat.GeoTiff,
                    _ => MapOutputFormat.Pdf
                },
                Title = regionDescription,
                CacheDirectory = options.CacheDir
            };

            var progress = ConsoleProgress.ForSteps();
            
            using var generator = new MapGenerator(options.CacheDir);
            var result = generator.GenerateAsync(generatorOptions, progress).GetAwaiter().GetResult();

            if (result.Success)
            {
                progress.Complete($"Map saved to {result.OutputPath}");
                Console.WriteLine($"  Tiles: {result.TilesDownloaded} downloaded, {result.TilesFromCache} from cache");
                Console.WriteLine($"  Duration: {result.Duration.TotalSeconds:F1}s");
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.ErrorMessage}");
                return 1;
            }
        });

        return interactiveCommand;
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
