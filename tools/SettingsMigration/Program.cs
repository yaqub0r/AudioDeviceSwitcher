using System.Text.Json;
using AudioDeviceSwitcher;
using AudioDeviceSwitcher.Migration;
using Microsoft.Win32;
using Windows.Management.Core;
using Windows.Management.Deployment;

const string legacyFamily = "16084JoseTorres.AudioDeviceSwitcher_dbg56r8e2ee38";
const string forkName = "yaqub0r.AudioDeviceSwitcher";
const string publisher = "CN=yaqub0r.AudioDeviceSwitcher";
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
try
{
    if (args.Length != 2 || (args[0] != "prepare" && args[0] != "import" && args[0] != "verify" && args[0] != "verify-source" && args[0] != "backup" && args[0] != "verify-backup"))
        throw new ArgumentException("Usage: SettingsMigration prepare|backup <new-backup-directory> | import|verify <prepared-settings.json> | verify-source <original-settings.json> | verify-backup <backup-directory>");

    if (args[0] == "backup" || args[0] == "verify-backup")
    {
        var folder = Path.GetFullPath(args[1]);
        var installed = new PackageManager().FindPackagesForUser(string.Empty).ToArray();
        var fork = installed.Single(p => p.Id.Name == forkName && p.Id.Publisher == publisher);
        var current = ApplicationDataManager.CreateForPackageFamily(fork.Id.FamilyName).LocalSettings.Values["settings"] as string
            ?? throw new InvalidOperationException("The fork has no saved settings.");
        var state = ReadState(current);
        if (args[0] == "verify-backup")
        {
            if (File.ReadAllText(Path.Combine(folder, "fork-settings.json")) != current)
                throw new InvalidOperationException("Fork settings differ from the pre-upgrade backup.");
            Console.WriteLine($"Verified all settings unchanged, including {state.Commands.Count} commands and their saved device names.");
            return 0;
        }
        if (Directory.Exists(folder))
            throw new IOException("Use a new backup directory; existing backups are never overwritten.");
        var legacy = installed.SingleOrDefault(p => p.Id.FamilyName == legacyFamily);
        var legacySettings = legacy == null ? null : ApplicationDataManager.CreateForPackageFamily(legacyFamily).LocalSettings.Values["settings"] as string;
        Directory.CreateDirectory(folder);
        WriteNew(Path.Combine(folder, "fork-settings.json"), current);
        if (legacySettings != null)
            WriteNew(Path.Combine(folder, "store-settings.json"), legacySettings);
        WriteNew(Path.Combine(folder, "packages.json"), JsonSerializer.Serialize(new
        {
            Fork = fork.Id.FullName, Store = legacy?.Id.FullName,
            StoreSettingsBackedUp = legacySettings != null,
        }, jsonOptions));
        Console.WriteLine($"Backed up {state.Commands.Count} fork commands and {(legacySettings == null ? "no" : "the")} Store settings to {folder}");
        return 0;
    }

    if (args[0] == "verify-source")
    {
        var current = ApplicationDataManager.CreateForPackageFamily(legacyFamily).LocalSettings.Values["settings"] as string;
        if (current != File.ReadAllText(args[1]))
            throw new InvalidOperationException("Store settings changed after the backup. Prepare a new migration before installing.");
        Console.WriteLine("Store settings still match the untouched backup.");
        return 0;
    }

    if (args[0] == "prepare")
    {
        var folder = Path.GetFullPath(args[1]);
        if (Directory.Exists(folder))
            throw new IOException("Use a new backup directory; existing backups are never overwritten.");
        var data = ApplicationDataManager.CreateForPackageFamily(legacyFamily);
        var original = data.LocalSettings.Values["settings"] as string;
        if (string.IsNullOrWhiteSpace(original))
            throw new InvalidOperationException("The Store app has no saved settings to migrate.");
        var state = ReadState(original);
        Directory.CreateDirectory(folder);
        WriteNew(Path.Combine(folder, "original-settings.json"), original);
        IAudioManager manager = new AudioManager();
        var migrated = new List<Command>();
        var report = new List<object>();
        foreach (var command in state.Commands)
        {
            var available = await manager.GetAllDevicesAsync(command.DeviceClass);
            var history = ReadHistory(command.DeviceClass, available);
            var recovered = LegacyDeviceMigration.Recover(command, available, history);
            migrated.Add(recovered);
            report.Add(new
            {
                command.Name, command.Hotkey, command.DeviceClass,
                OriginalIds = command.Devices,
                Devices = recovered.Devices.Select(id => new
                {
                    Id = id, Name = recovered.DeviceNames.GetValueOrDefault(id),
                    Present = available.Any(device => device.Id == id),
                    Active = available.Any(device => device.Id == id) && manager.IsActive(id),
                }).ToArray(),
            });
        }

        WriteNew(Path.Combine(folder, "prepared-settings.json"), JsonSerializer.Serialize(state with { Commands = migrated }, jsonOptions));
        WriteNew(Path.Combine(folder, "migration-report.json"), JsonSerializer.Serialize(report, jsonOptions));
        Console.WriteLine($"Backed up and prepared {migrated.Count} commands in {folder}");
        Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
        return 0;
    }

    var prepared = File.ReadAllText(args[1]);
    var expected = ReadState(prepared);
    var package = new PackageManager().FindPackagesForUser(string.Empty)
        .Single(p => p.Id.Name == forkName && p.Id.Publisher == publisher);
    var destination = ApplicationDataManager.CreateForPackageFamily(package.Id.FamilyName).LocalSettings;
    var existing = destination.Values["settings"] as string;
    if (args[0] == "import")
    {
        if (!string.IsNullOrWhiteSpace(existing) && existing != prepared)
            throw new InvalidOperationException("The personal app already has settings. Refusing to overwrite them.");
        destination.Values["settings"] = prepared;
        Console.WriteLine($"Imported {expected.Commands.Count} commands into {package.Id.FamilyName}");
    }
    else
    {
        var actual = ReadState(existing ?? throw new InvalidOperationException("No migrated settings found."));
        foreach (var command in expected.Commands)
        {
            var found = actual.Commands.Single(c => c.Name == command.Name);
            if (found.Hotkey != command.Hotkey || found.DeviceClass != command.DeviceClass
                || !found.Devices.SequenceEqual(command.Devices))
                throw new InvalidOperationException($"Migration verification failed for {command.Name}.");
        }

        if (actual.Commands.Count != expected.Commands.Count)
            throw new InvalidOperationException("Command counts differ.");
        if (actual.RunAtStartup != expected.RunAtStartup || actual.RunAtStartupMinimized != expected.RunAtStartupMinimized
            || actual.RunInBackground != expected.RunInBackground || actual.ShowDisabledDevices != expected.ShowDisabledDevices
            || actual.SwitchCommunicationDevice != expected.SwitchCommunicationDevice || actual.DarkTheme != expected.DarkTheme)
            throw new InvalidOperationException("Application preferences differ.");
        Console.WriteLine($"Verified all {expected.Commands.Count} migrated commands and hotkeys.");
    }

    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

AudioSwitcherState ReadState(string content)
{
    var state = JsonSerializer.Deserialize<AudioSwitcherState>(content)
        ?? throw new InvalidDataException("Settings are empty.");
    if (state.Commands == null || state.Commands.Count == 0 || state.Commands.Select(c => c.Name).Distinct().Count() != state.Commands.Count
        || state.Commands.Any(c => string.IsNullOrWhiteSpace(c.Name) || c.Devices == null || c.DeviceNames == null
            || !Enum.IsDefined(c.DeviceClass) || !Hotkey.Validate(c.Hotkey.Modifiers, c.Hotkey.Key)))
        throw new InvalidDataException("Settings contain invalid commands.");
    return state;
}

static void WriteNew(string path, string content)
{
    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
    using var writer = new StreamWriter(stream);
    writer.Write(content);
}

static Dictionary<string, string[]> ReadHistory(AudioDeviceClass type, AudioDevice[] available)
{
    var result = new Dictionary<string, string[]>();
    var category = type == AudioDeviceClass.Render ? "Render" : "Capture";
    foreach (var device in available)
    {
        var parts = device.Id.Split('#');
        if (parts.Length != 4 || !parts[2].Contains("}.{"))
            continue;
        var endpointGuid = parts[2][(parts[2].IndexOf("}.{") + 2)..];
        using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\{category}\{endpointGuid}\Properties");
        // Optional, undocumented Windows endpoint history observed on this machine.
        // Read only: absent or ambiguous history never warrants guessing a replacement.
        if (key?.GetValue("{4b416b7d-8501-40c1-acfd-97aa9bdc17c8},1") is string[] history)
            result[device.Id] = history.Select(old => $"{parts[0]}#{parts[1]}#{old}#{parts[3]}").ToArray();
    }

    return result;
}
