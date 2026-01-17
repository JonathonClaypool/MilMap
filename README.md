# MilMap

Generate military-style topographic maps from OpenStreetMap data.

## Overview

MilMap is a .NET command-line tool that creates printable military maps with MGRS (Military Grid Reference System) overlays. It addresses the common problem of military maps being either too zoomed out or outdated by allowing users to generate custom maps for any area at configurable scales.

## Features

- **MGRS Grid Overlays** - Full support for Military Grid Reference System coordinates
- **Multiple Input Methods** - Specify regions by MGRS coordinates, lat/lon bounding boxes, or military installation names
- **Configurable Scale** - Generate maps at 1:10,000, 1:25,000, 1:50,000, or any custom scale
- **Multiple Output Formats** - Export as PDF, PNG, or GeoTIFF
- **Military Installation Database** - Built-in lookup for U.S. military installations with fuzzy search
- **Offline Tile Caching** - Cache downloaded OSM tiles for offline use
- **Contour Lines** - Elevation contours from SRTM data
- **Military Symbology** - Standard military map symbols and annotations

## Installation

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later

### Build from Source

```bash
git clone https://github.com/your-org/MilMap.git
cd MilMap
dotnet build
```

### Install as Global Tool

```bash
dotnet pack -c Release
dotnet tool install --global --add-source ./src/MilMap.CLI/bin/Release MilMap.CLI
```

## Usage

### Basic Commands

```bash
# Generate a map for an MGRS grid square
milmap output.pdf --mgrs 18TXM

# Generate a map centered on a precise MGRS coordinate
milmap output.pdf --mgrs 18TXM12345678 --scale 50000

# Generate a map for a lat/lon bounding box
milmap output.png --bounds 38.8,-77.1,38.9,-77.0 --format png --dpi 150

# Generate a map for a military installation
milmap output.pdf --installation "Fort Liberty" --scale 100000
```

### Installation Lookup

```bash
# Search for installations
milmap installations search "Fort"

# List all Army installations
milmap installations list --branch Army

# Show details for a specific installation
milmap installations show "Fort Liberty"
```

### Configuration

```bash
# Show current configuration
milmap config show

# Create a new config file
milmap config init

# Show path to discovered config
milmap config path
```

### Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--mgrs` | `-m` | MGRS coordinate or grid reference | - |
| `--bounds` | `-b` | Lat/lon bounding box (minLat,minLon,maxLat,maxLon) | - |
| `--installation` | `-i` | Military installation name | - |
| `--scale` | `-s` | Map scale denominator (e.g., 25000 for 1:25000) | 25000 |
| `--dpi` | `-d` | Output resolution in DPI | 300 |
| `--format` | `-f` | Output format (pdf, png, geotiff) | pdf |
| `--cache-dir` | `-c` | Directory for caching tiles | - |
| `--config` | - | Path to configuration file | - |

### Configuration File

MilMap searches for configuration files in these locations:
1. Current directory and parent directories
2. Home directory (`~/`)
3. `~/.config/milmap/`

Supported formats: YAML (`.yaml`, `.yml`) and JSON (`.json`)

Example `milmap.yaml`:

```yaml
scale: 25000
dpi: 300
format: pdf
cacheDir: ~/.cache/milmap
```

## Development

### Project Structure

```
MilMap/
├── src/
│   ├── MilMap.CLI/          # Command-line interface
│   └── MilMap.Core/         # Core library
│       ├── Elevation/       # Elevation data (SRTM)
│       ├── Export/          # Output format exporters
│       ├── Input/           # Region input handlers
│       ├── Mgrs/            # MGRS coordinate system
│       ├── Osm/             # OpenStreetMap data client
│       ├── Progress/        # Progress reporting
│       ├── Rendering/       # Map rendering components
│       └── Tiles/           # Tile fetching and caching
├── tests/
│   └── MilMap.Tests/        # Unit tests
└── docs/                    # Documentation
```

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

## License

See [LICENSE](LICENSE) file for details.
