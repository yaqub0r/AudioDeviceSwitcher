namespace AudioDeviceSwitcher.Tests;

using AudioDeviceSwitcher.Migration;

public class LegacyDeviceMigrationTests
{
    [Fact]
    public void ExactHistoryRecoversOrphanWithoutGuessingItsName()
    {
        var original = Saved("old");
        var recovered = LegacyDeviceMigration.Recover(original, new[] { new AudioDevice("new", "Soundbar") },
            new Dictionary<string, string[]> { ["new"] = new[] { "old" } });
        Assert.Equal(new[] { "new" }, recovered.Devices);
        Assert.Equal("Soundbar", recovered.DeviceNames["new"]);
        Assert.Equal(original.Hotkey, recovered.Hotkey);
        Assert.Equal(new[] { "old" }, original.Devices);
        Assert.Empty(original.DeviceNames);
    }

    [Fact]
    public void AmbiguousHistoryAndUnknownIdsRemainSaved()
    {
        var original = Saved("old");
        var recovered = LegacyDeviceMigration.Recover(original,
            new[] { new AudioDevice("a", "Soundbar"), new AudioDevice("b", "Soundbar") },
            new Dictionary<string, string[]> { ["a"] = new[] { "old" }, ["b"] = new[] { "old" } });
        Assert.Equal(original.Devices, recovered.Devices);
        Assert.Empty(recovered.DeviceNames);
        Assert.Equal(original.Devices, LegacyDeviceMigration.Recover(original, Array.Empty<AudioDevice>(), new Dictionary<string, string[]>()).Devices);
    }

    [Fact]
    public void ExistingIdTakesPriorityOverHistory()
    {
        var original = Saved("old");
        var recovered = LegacyDeviceMigration.Recover(original,
            new[] { new AudioDevice("old", "Original"), new AudioDevice("new", "Other") },
            new Dictionary<string, string[]> { ["new"] = new[] { "old" } });
        Assert.Equal(original.Devices, recovered.Devices);
        Assert.Equal("Original", recovered.DeviceNames["old"]);
    }

    [Fact]
    public void MultipleHistoricalIdsForOneEndpointDoNotDuplicateCycleEntries()
    {
        var original = Saved("first") with { Devices = new[] { "first", "second" } };
        var recovered = LegacyDeviceMigration.Recover(original, new[] { new AudioDevice("new", "Soundbar") },
            new Dictionary<string, string[]> { ["new"] = new[] { "first", "second" } });
        Assert.Equal(new[] { "new" }, recovered.Devices);
    }

    private static Command Saved(string id) => new("Default", AudioDeviceClass.Render,
        new Hotkey(KeyModifiers.Control | KeyModifiers.Menu, (Key)'D'), new[] { id });
}
