using LocalBackupMaster.Models;

namespace LocalBackupMaster.Tests;

public class BackupProfileTests
{
    [Fact]
    public void GetSourceIds_RoundTripsSerializedValues()
    {
        var profile = new BackupProfile { Name = "Fotos" };

        profile.SetSourceIds([1, 2, 3]);

        Assert.Equal([1, 2, 3], profile.GetSourceIds());
        Assert.Equal("[1,2,3]", profile.SourceIdsJson);
    }

    [Fact]
    public void GetDestinationIds_RoundTripsSerializedValues()
    {
        var profile = new BackupProfile { Name = "Destino" };

        profile.SetDestinationIds([4, 8]);

        Assert.Equal([4, 8], profile.GetDestinationIds());
        Assert.Equal("[4,8]", profile.DestinationIdsJson);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[not-json]")]
    public void GetSourceIds_InvalidJson_ReturnsEmptyList(string json)
    {
        var profile = new BackupProfile
        {
            Name = "Inválido",
            SourceIdsJson = json
        };

        Assert.Empty(profile.GetSourceIds());
    }

    [Fact]
    public void GetDestinationIds_NullJson_ReturnsEmptyList()
    {
        var profile = new BackupProfile
        {
            Name = "Nulo",
            DestinationIdsJson = null!
        };

        Assert.Empty(profile.GetDestinationIds());
    }
}
