using MilMap.Core.Input;
using Xunit;

namespace MilMap.Tests.Input;

public class InstallationInputHandlerTests
{
    [Fact]
    public void GetAllInstallations_ReturnsNonEmptyList()
    {
        var installations = InstallationInputHandler.GetAllInstallations();

        Assert.NotNull(installations);
        Assert.True(installations.Count >= 50, $"Expected at least 50 installations, got {installations.Count}");
    }

    [Fact]
    public void Lookup_ByExactName_ReturnsInstallation()
    {
        var result = InstallationInputHandler.Lookup("Fort Liberty");

        Assert.NotNull(result);
        Assert.Equal("fort-liberty", result.InstallationId);
        Assert.Equal("Fort Liberty", result.InstallationName);
        Assert.Equal("Army", result.Branch);
    }

    [Fact]
    public void Lookup_ByAlias_ReturnsInstallation()
    {
        // Fort Cavazos was formerly Fort Hood
        var result = InstallationInputHandler.Lookup("Fort Hood");

        Assert.NotNull(result);
        Assert.Equal("fort-hood", result.InstallationId);
    }

    [Fact]
    public void Lookup_ById_ReturnsInstallation()
    {
        var result = InstallationInputHandler.Lookup("camp-pendleton");

        Assert.NotNull(result);
        Assert.Equal("Marine Corps Base Camp Pendleton", result.InstallationName);
        Assert.Equal("Marines", result.Branch);
    }

    [Fact]
    public void Lookup_CaseInsensitive_ReturnsInstallation()
    {
        var result = InstallationInputHandler.Lookup("FORT LIBERTY");

        Assert.NotNull(result);
        Assert.Equal("fort-liberty", result.InstallationId);
    }

    [Fact]
    public void Lookup_PartialMatch_ReturnsInstallation()
    {
        var result = InstallationInputHandler.Lookup("Camp Pend");

        Assert.NotNull(result);
        Assert.Contains("Camp Pendleton", result.InstallationName);
    }

    [Fact]
    public void Lookup_NotFound_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InstallationInputHandler.Lookup("Nonexistent Base"));
    }

    [Fact]
    public void Lookup_EmptyQuery_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InstallationInputHandler.Lookup(""));
    }

    [Fact]
    public void TryLookup_ValidQuery_ReturnsTrue()
    {
        var found = InstallationInputHandler.TryLookup("Fort Liberty", out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("fort-liberty", result!.InstallationId);
    }

    [Fact]
    public void TryLookup_InvalidQuery_ReturnsFalse()
    {
        var found = InstallationInputHandler.TryLookup("Nonexistent Base", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void Search_PartialQuery_ReturnsMatches()
    {
        var results = InstallationInputHandler.Search("Fort").ToList();

        Assert.NotEmpty(results);
        Assert.True(results.Count > 5, "Expected multiple Fort installations");
        Assert.All(results, r => Assert.True(
            r.Name.Contains("Fort", StringComparison.OrdinalIgnoreCase) ||
            r.Aliases.Any(a => a.Contains("Fort", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void Search_RespectsMaxResults()
    {
        var results = InstallationInputHandler.Search("Fort", maxResults: 3).ToList();

        Assert.True(results.Count <= 3);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var results = InstallationInputHandler.Search("").ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void GetByBranch_Army_ReturnsArmyInstallations()
    {
        var results = InstallationInputHandler.GetByBranch("Army").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Army", r.Branch));
    }

    [Fact]
    public void GetByBranch_Navy_ReturnsNavyInstallations()
    {
        var results = InstallationInputHandler.GetByBranch("Navy").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Navy", r.Branch));
    }

    [Fact]
    public void GetByBranch_Marines_ReturnsMarinesInstallations()
    {
        var results = InstallationInputHandler.GetByBranch("Marines").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Marines", r.Branch));
    }

    [Fact]
    public void GetByBranch_AirForce_ReturnsAirForceInstallations()
    {
        var results = InstallationInputHandler.GetByBranch("Air Force").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Air Force", r.Branch));
    }

    [Fact]
    public void GetByState_CA_ReturnsCaliforniaInstallations()
    {
        var results = InstallationInputHandler.GetByState("CA").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("CA", r.State));
    }

    [Fact]
    public void GetByType_Fort_ReturnsFortInstallations()
    {
        var results = InstallationInputHandler.GetByType("fort").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("fort", r.Type));
    }

    [Fact]
    public void IsInstallation_ValidName_ReturnsTrue()
    {
        Assert.True(InstallationInputHandler.IsInstallation("Fort Liberty"));
    }

    [Fact]
    public void IsInstallation_InvalidName_ReturnsFalse()
    {
        Assert.False(InstallationInputHandler.IsInstallation("Nonexistent Base"));
    }

    [Fact]
    public void IsInstallation_EmptyString_ReturnsFalse()
    {
        Assert.False(InstallationInputHandler.IsInstallation(""));
    }

    [Fact]
    public void Lookup_BoundingBoxIsValid()
    {
        var result = InstallationInputHandler.Lookup("Fort Liberty");

        Assert.True(result.BoundingBox.MinLat < result.BoundingBox.MaxLat);
        Assert.True(result.BoundingBox.MinLon < result.BoundingBox.MaxLon);
        Assert.True(result.BoundingBox.MinLat >= -90 && result.BoundingBox.MaxLat <= 90);
        Assert.True(result.BoundingBox.MinLon >= -180 && result.BoundingBox.MaxLon <= 180);
    }

    [Fact]
    public void Lookup_CenterIsWithinBoundingBox()
    {
        var result = InstallationInputHandler.Lookup("Fort Liberty");

        Assert.True(result.CenterLat >= result.BoundingBox.MinLat);
        Assert.True(result.CenterLat <= result.BoundingBox.MaxLat);
        Assert.True(result.CenterLon >= result.BoundingBox.MinLon);
        Assert.True(result.CenterLon <= result.BoundingBox.MaxLon);
    }

    [Theory]
    [InlineData("Edwards AFB", "edwards-afb", "Air Force")]
    [InlineData("29 Palms", "twentynine-palms", "Marines")]
    [InlineData("JBLM", "fort-lewis", "Joint")]
    [InlineData("Pearl Harbor", "jb-pearl-harbor-hickam", "Joint")]
    [InlineData("Vandenberg", "vandenberg-sfb", "Space Force")]
    public void Lookup_KnownInstallations_ReturnsExpectedResults(string query, string expectedId, string expectedBranch)
    {
        var result = InstallationInputHandler.Lookup(query);

        Assert.Equal(expectedId, result.InstallationId);
        Assert.Equal(expectedBranch, result.Branch);
    }

    [Fact]
    public void AllInstallations_HaveRequiredFields()
    {
        var installations = InstallationInputHandler.GetAllInstallations();

        foreach (var installation in installations)
        {
            Assert.False(string.IsNullOrWhiteSpace(installation.Id), $"Installation missing ID");
            Assert.False(string.IsNullOrWhiteSpace(installation.Name), $"Installation {installation.Id} missing Name");
            Assert.False(string.IsNullOrWhiteSpace(installation.Type), $"Installation {installation.Id} missing Type");
            Assert.False(string.IsNullOrWhiteSpace(installation.Branch), $"Installation {installation.Id} missing Branch");
            Assert.False(string.IsNullOrWhiteSpace(installation.State), $"Installation {installation.Id} missing State");
            Assert.NotNull(installation.BoundingBox);
        }
    }

    [Fact]
    public void AllInstallations_HaveValidBoundingBoxes()
    {
        var installations = InstallationInputHandler.GetAllInstallations();

        foreach (var installation in installations)
        {
            var bb = installation.BoundingBox;
            Assert.True(bb.MinLat < bb.MaxLat, $"Installation {installation.Id} has invalid lat bounds");
            Assert.True(bb.MinLon < bb.MaxLon, $"Installation {installation.Id} has invalid lon bounds");
            Assert.True(bb.MinLat >= -90 && bb.MaxLat <= 90, $"Installation {installation.Id} has out of range lat");
            Assert.True(bb.MinLon >= -180 && bb.MaxLon <= 180, $"Installation {installation.Id} has out of range lon");
        }
    }
}
