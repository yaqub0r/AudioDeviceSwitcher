namespace AudioDeviceSwitcher.Tests;

using System.Text.Json;
using AudioDeviceSwitcher.Core.Application;

public class DeviceSelectionResolverTests
{
    [Fact]
    public void LegacySettingsLearnNamesAndSurviveTwoIdChanges()
    {
        var command = JsonSerializer.Deserialize<Command>("{\"Name\":\"Speakers\",\"Devices\":[\"old\"]}")!;
        command = DeviceSelectionResolver.Resolve(command, new[] { new AudioDevice("old", "Speakers (USB)") });
        command = JsonSerializer.Deserialize<Command>(JsonSerializer.Serialize(command))!;
        command = DeviceSelectionResolver.Resolve(command, new[] { new AudioDevice("new", "Speakers (USB)") });
        command = JsonSerializer.Deserialize<Command>(JsonSerializer.Serialize(command))!;
        command = DeviceSelectionResolver.Resolve(command, new[] { new AudioDevice("newer", "Speakers (USB)") });

        Assert.Equal(new[] { "newer" }, command.Devices);
        Assert.Equal("Speakers (USB)", command.DeviceNames["newer"]);
        Assert.Single(command.DeviceNames);
    }

    [Fact]
    public void ExactIdWinsAndUpdatesRenamedDevice()
    {
        var command = Saved("old", "Speakers");
        var resolved = DeviceSelectionResolver.Resolve(command, new[]
        {
            new AudioDevice("other", "Speakers"), new AudioDevice("old", "Renamed speakers"),
        });

        Assert.Equal(command.Devices, resolved.Devices);
        Assert.Equal("Renamed speakers", resolved.DeviceNames["old"]);
    }

    [Fact]
    public void MissingDeviceIsRetainedUntilItReturns()
    {
        var command = Saved("old", "Speakers");
        Assert.Same(command, DeviceSelectionResolver.Resolve(command, Array.Empty<AudioDevice>()));
        Assert.Equal(new[] { "new" }, DeviceSelectionResolver.Resolve(command, new[] { new AudioDevice("new", "Speakers") }).Devices);
    }

    [Fact]
    public void DuplicateAvailableNamesAreNotGuessed()
    {
        var command = Saved("old", "Speakers");
        Assert.Same(command, DeviceSelectionResolver.Resolve(command, new[]
        {
            new AudioDevice("one", "Speakers"), new AudioDevice("two", "Speakers"),
        }));
    }

    [Fact]
    public void TwoSavedDevicesCannotCollapseOntoOneReplacement()
    {
        var command = Saved("old", "Speakers") with
        {
            Devices = new[] { "old", "other" },
            DeviceNames = new() { ["old"] = "Speakers", ["other"] = "Speakers" },
        };
        Assert.Same(command, DeviceSelectionResolver.Resolve(command, new[] { new AudioDevice("new", "Speakers") }));
    }

    [Fact]
    public void ReplacementCannotStealAnotherSavedId()
    {
        var command = Saved("old", "Speakers") with { Devices = new[] { "old", "present" } };
        var resolved = DeviceSelectionResolver.Resolve(command, new[] { new AudioDevice("present", "Speakers") });
        Assert.Equal(command.Devices, resolved.Devices);
    }

    [Fact]
    public void OrphanedLegacyIdIsNotMatchedToAnArbitraryDevice()
    {
        var command = new Command("Legacy", AudioDeviceClass.Render, devices: new[] { "old" });
        Assert.Same(command, DeviceSelectionResolver.Resolve(command, new[] { new AudioDevice("new", "Speakers") }));
    }

    [Fact]
    public void RecoveryPreservesCycleOrder()
    {
        var command = Saved("old", "Speakers") with { Devices = new[] { "headphones", "old" } };
        var resolved = DeviceSelectionResolver.Resolve(command, new[]
        {
            new AudioDevice("new", "Speakers"), new AudioDevice("headphones", "Headphones"),
        });
        Assert.Equal(new[] { "headphones", "new" }, resolved.Devices);
    }

    private static Command Saved(string id, string name) => new("Switch", AudioDeviceClass.Render, devices: new[] { id })
    {
        DeviceNames = new() { [id] = name },
    };
}
