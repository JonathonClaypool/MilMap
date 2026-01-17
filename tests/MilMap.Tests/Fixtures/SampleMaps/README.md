# Sample Maps for Visual Regression Testing

This directory contains reference images used for visual regression testing of the MilMap rendering engine.

## Purpose

These sample maps serve as baselines to detect unintended changes to map rendering output. Each test compares the current rendering output against these reference images to catch visual regressions.

## Sample Maps

| File | Description |
|------|-------------|
| `mgrs_grid_dc.png` | MGRS grid overlay for Washington DC area |
| `mgrs_grid_london.png` | MGRS grid near UTM zone 30/31 boundary (Greenwich) |
| `mgrs_grid_sydney.png` | MGRS grid for southern hemisphere (Sydney) |
| `mgrs_grid_cross_zone.png` | MGRS grid spanning UTM zones 17/18 |
| `mgrs_grid_scale_100k.png` | MGRS grid at 1:100,000 scale |
| `scale_bar_25k.png` | Scale bar at 1:25,000 |
| `scale_bar_50k.png` | Scale bar at 1:50,000 |

## Updating Baselines

When rendering logic changes intentionally and you need to update the reference images:

```csharp
// In a test or scratch file:
using MilMap.Tests.Fixtures.SampleMaps;

SampleMapGenerator.GenerateAll("/path/to/tests/MilMap.Tests/Fixtures/SampleMaps");
```

Or run the test suite once to auto-generate missing images, then copy from the bin output directory.

## Test Tolerance

Visual regression tests allow up to 1% pixel difference to account for:
- Anti-aliasing variations
- Font rendering differences across platforms
- Floating-point precision differences

## Adding New Samples

1. Add a new `SampleMapDefinition` to `SampleMapGenerator.Samples`
2. Run tests to generate the baseline image
3. Commit the new PNG file
