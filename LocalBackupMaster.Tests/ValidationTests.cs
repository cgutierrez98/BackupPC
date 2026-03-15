using LocalBackupMaster.Models;
using LocalBackupMaster.Services.Validation;
using Xunit;
using System.Collections.Generic;
using System.IO;

namespace LocalBackupMaster.Tests;

public class ValidationTests
{
    private readonly BackupValidator _validator;

    public ValidationTests()
    {
        _validator = new BackupValidator();
    }

    [Fact]
    public void Validate_NoSources_ReturnsFalse()
    {
        // Arrange
        var sources = new List<BackupSource>();
        var destinations = new List<BackupDestination> { new() { Uuid = "test", BackupPath = "C:\\" } };

        // Act
        var (isValid, message) = _validator.Validate(sources, destinations);

        // Assert
        Assert.False(isValid);
        Assert.Contains("origen", message);
    }

    [Fact]
    public void Validate_NoDestinations_ReturnsFalse()
    {
        // Arrange
        var sources = new List<BackupSource> { new() { Path = "C:\\Data" } };
        var destinations = new List<BackupDestination>();

        // Act
        var (isValid, message) = _validator.Validate(sources, destinations);

        // Assert
        Assert.False(isValid);
        Assert.Contains("destino", message);
    }

    [Fact]
    public void Validate_DestinationPathNotExists_ReturnsFalse()
    {
        // Arrange
        var sources = new List<BackupSource> { new() { Path = "C:\\Data" } };
        var destinations = new List<BackupDestination> { new() { Uuid = "test", BackupPath = "Z:\\NonExistentPath" } };

        // Act
        var (isValid, message) = _validator.Validate(sources, destinations);

        // Assert
        Assert.False(isValid);
        Assert.Contains("no está conectada", message);
    }
}
