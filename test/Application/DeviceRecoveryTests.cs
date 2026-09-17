namespace AudioDeviceSwitcher.Tests;

using System.Collections.Specialized;

public class DeviceRecoveryTests
{
    [Theory]
    [InlineData(AudioDeviceClass.Render)]
    [InlineData(AudioDeviceClass.Capture)]
    public async Task HotkeyRecoversChangedIdAndPersistsIt(AudioDeviceClass type)
    {
        var fixture = new Fixture(type);
        await fixture.Switcher.LoadAsync();
        Assert.Equal("Speakers", fixture.Storage.Load().Commands[0].DeviceNames["old"]);
        fixture.Available = new[] { new AudioDevice("new", "Speakers") };

        await fixture.Switcher.ExecuteCommandByHotkeyAsync(fixture.Hotkey);

        Assert.Equal("new", fixture.DefaultId);
        Assert.Equal(new[] { "new" }, fixture.Storage.Load().Commands[0].Devices);
        fixture.Audio.Verify(x => x.GetAllDevicesAsync(type), Times.AtLeastOnce());
        fixture.Audio.Verify(x => x.SetDefaultDevice("old", It.IsAny<AudioDeviceRoleType>()), Times.Never());
    }

    [Fact]
    public async Task AmbiguousRecoveryDoesNotSwitchToAnUnrelatedEndpoint()
    {
        var fixture = new Fixture();
        await fixture.Switcher.LoadAsync();
        fixture.Available = new[] { new AudioDevice("new1", "Speakers"), new AudioDevice("new2", "Speakers") };

        await fixture.Switcher.ExecuteCommandByHotkeyAsync(fixture.Hotkey);

        fixture.Audio.Verify(x => x.SetDefaultDevice(It.IsAny<string>(), It.IsAny<AudioDeviceRoleType>()), Times.Never());
        Assert.Equal(new[] { "old" }, fixture.Storage.Load().Commands[0].Devices);
    }

    [Fact]
    public async Task DeviceRemovalAndHotkeyEditDoNotEraseSelection()
    {
        var fixture = new Fixture();
        var selected = new List<object>();
        using var model = fixture.CreatePage();
        await model.InitializeAsync(AudioDeviceClass.Render, watch: false, selected);
        var device = AudioDeviceViewModel.Create(fixture.Audio.Object, fixture.Available[0], AudioDeviceClass.Render);
        model.Devices.Add(device);
        await model.LoadCommandAsync(model.SelectedCommand);
        // Simulate the ListView removing its selection when its source removes a row.
        model.FilteredDevices.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                selected.Remove(device);
                model.DeviceSelectionChanged();
            }
        };
        fixture.Available = Array.Empty<AudioDevice>();
        fixture.Events.Raise(new AudioDeviceRemoved("old"));
        model.SetHotkey(new Hotkey(KeyModifiers.Control, Key.A));
        model.SaveSelectedCommand();

        Assert.Equal(new[] { "old" }, fixture.Storage.Load().Commands[0].Devices);
        Assert.Equal("Speakers", fixture.Storage.Load().Commands[0].DeviceNames["old"]);
        Assert.True(model.CanExecute());
    }

    [Fact]
    public async Task ExplicitDeselectionOfPresentDeviceIsSaved()
    {
        var fixture = new Fixture();
        using var model = fixture.CreatePage();
        await model.InitializeAsync(AudioDeviceClass.Render, watch: false);
        model.Devices.Add(AudioDeviceViewModel.Create(fixture.Audio.Object, fixture.Available[0], AudioDeviceClass.Render));
        await model.LoadCommandAsync(model.SelectedCommand);
        model.SelectDevices(Array.Empty<AudioDeviceViewModel>());
        model.DeviceSelectionChanged();

        Assert.Empty(fixture.Storage.Load().Commands[0].Devices);
        Assert.Empty(fixture.Storage.Load().Commands[0].DeviceNames);
    }

    [Fact]
    public async Task PageRestoresRecoveredSelectionAfterBackgroundHotkey()
    {
        var fixture = new Fixture();
        using var model = fixture.CreatePage();
        await model.InitializeAsync(AudioDeviceClass.Render, watch: false);
        fixture.Available = new[] { new AudioDevice("new", "Speakers") };
        await fixture.Switcher.ExecuteCommandByHotkeyAsync(fixture.Hotkey);
        model.Devices.Add(AudioDeviceViewModel.Create(fixture.Audio.Object, fixture.Available[0], AudioDeviceClass.Render));
        await model.LoadCommandAsync(model.SelectedCommand);

        Assert.Equal(new[] { "new" }, model.SelectedCommand.DeviceIds);
        Assert.Equal(new[] { "new" }, model.ToModel(model.SelectedCommand).Devices);
    }

    private sealed class Fixture
    {
        public Fixture(AudioDeviceClass type = AudioDeviceClass.Render)
        {
            Storage.Save(new AudioSwitcherState
            {
                Commands = new() { new Command("Switch", type, Hotkey, new[] { "old" }) },
            });
            Audio.Setup(x => x.GetAllDevicesAsync(It.IsAny<AudioDeviceClass>()))
                .ReturnsAsync((AudioDeviceClass requested) => requested == type ? Available : Array.Empty<AudioDevice>());
            Audio.Setup(x => x.GetState(It.IsAny<string>()))
                .Returns((string id) => Available.Any(d => d.Id == id) ? AudioDeviceState.Active : (AudioDeviceState)0);
            Audio.Setup(x => x.IsActive(It.IsAny<string>())).Returns((string id) => Available.Any(d => d.Id == id));
            Audio.Setup(x => x.GetDefaultAudioId(type, AudioDeviceRoleType.Default)).Returns(() => DefaultId);
            Audio.Setup(x => x.SetDefaultDevice(It.IsAny<string>(), It.IsAny<AudioDeviceRoleType>()))
                .Returns((string id, AudioDeviceRoleType _) => { DefaultId = id; return true; });
            var startup = new FakeStartupTaskManager();
            startup.Tasks.Add(AudioSwitcher.StartupTaskId, new FakeStartupTask());
            Switcher = new AudioSwitcher(Audio.Object, startup, new NoIO(), Storage,
                new Mock<IHotkeyManager>().Object, new NoNotificationService());
        }

        public Hotkey Hotkey { get; } = new(KeyModifiers.Control | KeyModifiers.Menu, (Key)'D');
        public Mock<IAudioManager> Audio { get; } = new();
        public AudioDevice[] Available { get; set; } = new[] { new AudioDevice("old", "Speakers") };
        public string DefaultId { get; set; } = "unselected";
        public InMemoryStateStorage Storage { get; } = new();
        public AudioEvents Events { get; } = new();
        public AudioSwitcher Switcher { get; }

        public AudioPageViewModel CreatePage() => new(Switcher, Events, new AudioDeviceWatcher(Events), Audio.Object,
            new NoNotificationService(), new NoIO(), new SyncDispatcher(), new Mock<IClipboard>().Object, new Mock<IApp>().Object);
    }
}
