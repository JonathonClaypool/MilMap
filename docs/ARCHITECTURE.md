# MilMap Architecture

This document describes the high-level architecture of MilMap.

## Overview

MilMap is designed as a modular .NET application with clear separation between the command-line interface and core functionality. The architecture supports extensibility for new input methods, output formats, and rendering components.

```
┌─────────────────────────────────────────────────────────────────┐
│                         MilMap.CLI                               │
│  ┌─────────────────┐  ┌──────────────┐  ┌────────────────────┐  │
│  │ CommandLineParser│  │ ConfigLoader │  │ ConsoleProgress    │  │
│  └────────┬────────┘  └──────────────┘  │ Reporter           │  │
│           │                              └────────────────────┘  │
└───────────┼─────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────┐
│                        MilMap.Core                               │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                       Input Layer                         │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐  │   │
│  │  │MgrsInput    │  │LatLonInput   │  │InstallationInput│  │   │
│  │  │Handler      │  │Handler       │  │Handler          │  │   │
│  │  └─────────────┘  └──────────────┘  └─────────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                     Data Sources                          │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐  │   │
│  │  │OsmTileFetcher│ │OverpassClient│  │SrtmElevation    │  │   │
│  │  │             │  │              │  │Source           │  │   │
│  │  └─────────────┘  └──────────────┘  └─────────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    Rendering Pipeline                     │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐  │   │
│  │  │BaseMap      │  │ContourRenderer│ │MgrsGrid         │  │   │
│  │  │Renderer     │  │              │  │Renderer         │  │   │
│  │  └─────────────┘  └──────────────┘  └─────────────────┘  │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐  │   │
│  │  │ScaleBar     │  │MagneticDecl  │  │MilitarySymbology│  │   │
│  │  │Renderer     │  │Renderer      │  │Renderer         │  │   │
│  │  └─────────────┘  └──────────────┘  └─────────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                     Export Layer                          │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐  │   │
│  │  │PdfExporter  │  │ImageExporter │  │GeoTiffExporter  │  │   │
│  │  └─────────────┘  └──────────────┘  └─────────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Components

### MilMap.CLI

The command-line interface layer that handles user interaction.

| Component | Responsibility |
|-----------|----------------|
| `CommandLineParser` | Parses command-line arguments using System.CommandLine |
| `ConfigLoader` | Discovers and loads YAML/JSON configuration files |
| `MapOptions` | Data transfer object for map generation options |
| `ConsoleProgressReporter` | Displays progress to the console |

### MilMap.Core

The core library containing all map generation logic.

#### Input Layer (`Input/`)

Handles parsing and validation of different region specification methods.

| Component | Responsibility |
|-----------|----------------|
| `MgrsInputHandler` | Parses MGRS coordinates and grid squares |
| `LatLonInputHandler` | Parses latitude/longitude bounding boxes |
| `InstallationInputHandler` | Looks up military installations with fuzzy matching |

#### MGRS Subsystem (`Mgrs/`)

Implements the Military Grid Reference System.

| Component | Responsibility |
|-----------|----------------|
| `MgrsParser` | Parses MGRS strings into structured coordinates |
| `MgrsEncoder` | Converts lat/lon to MGRS |
| `MgrsBoundary` | Calculates grid zone and square boundaries |

#### Data Sources

##### Tiles (`Tiles/`)

| Component | Responsibility |
|-----------|----------------|
| `OsmTileFetcher` | Downloads OpenStreetMap raster tiles |
| `TileCache` | Caches tiles locally to reduce network requests |
| `ZoomLevelCalculator` | Determines appropriate zoom level for scale/DPI |

##### OSM Data (`Osm/`)

| Component | Responsibility |
|-----------|----------------|
| `OverpassClient` | Queries OpenStreetMap's Overpass API for vector data |

##### Elevation (`Elevation/`)

| Component | Responsibility |
|-----------|----------------|
| `SrtmElevationSource` | Provides elevation data from NASA SRTM dataset |
| `ElevationTile` | Represents a single elevation data tile |

#### Rendering Pipeline (`Rendering/`)

Composable renderers that each handle a specific map layer.

| Component | Responsibility |
|-----------|----------------|
| `BaseMapRenderer` | Renders the base OSM map tiles |
| `ContourRenderer` | Generates contour lines from elevation data |
| `MgrsGridRenderer` | Draws MGRS grid lines and labels |
| `ScaleBarRenderer` | Renders the map scale bar |
| `MapMarginRenderer` | Draws map margins with coordinates |
| `MagneticDeclinationRenderer` | Shows magnetic declination diagram |
| `MilitarySymbologyRenderer` | Renders military map symbols |

#### Export Layer (`Export/`)

| Component | Responsibility |
|-----------|----------------|
| `PdfExporter` | Exports maps as PDF documents |
| `ImageExporter` | Exports maps as PNG images |
| `GeoTiffExporter` | Exports georeferenced TIFF files |

### Progress Reporting (`Progress/`)

| Component | Responsibility |
|-----------|----------------|
| `ProgressReporter` | Abstract base for progress reporting |

## Data Flow

1. **Input Parsing**: User provides region via MGRS, lat/lon bounds, or installation name
2. **Region Resolution**: Input handlers convert to a normalized bounding box
3. **Tile Calculation**: Zoom level and required tiles are calculated based on scale and DPI
4. **Data Fetching**: OSM tiles and elevation data are fetched (with caching)
5. **Rendering**: Each renderer in the pipeline adds its layer to the canvas
6. **Export**: The final composite image is exported in the requested format

## Extension Points

### Adding New Input Methods

1. Create a new handler in `Input/` implementing the input interface
2. Register in `CommandLineParser` with appropriate options

### Adding New Renderers

1. Create a new renderer class in `Rendering/`
2. Add to the rendering pipeline configuration

### Adding New Export Formats

1. Create a new exporter in `Export/`
2. Add to the `OutputFormat` enum
3. Wire up in the export dispatcher

## Dependencies

- **System.CommandLine** - Command-line parsing
- **SkiaSharp** - 2D graphics rendering
- **YamlDotNet** - YAML configuration parsing
- **System.Text.Json** - JSON configuration parsing

## Threading Model

- Tile fetching uses parallel HTTP requests with configurable concurrency
- Rendering is single-threaded to simplify canvas composition
- Progress reporting is thread-safe

## Error Handling

- Input validation errors are reported with helpful suggestions
- Network errors trigger retry with exponential backoff
- Missing tiles are handled gracefully with placeholder rendering
