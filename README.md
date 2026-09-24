# Audio Device Switcher 

[![CI](https://github.com/yaqub0r/AudioDeviceSwitcher/actions/workflows/main.yaml/badge.svg)](https://github.com/yaqub0r/AudioDeviceSwitcher/actions)

This is the **yaqub0r-maintained fork** of [José Torres's Audio Device Switcher](https://github.com/josetr/AudioDeviceSwitcher). It retains the familiar app name and upstream attribution, with its own package identity and support at [this repository's issue tracker](https://github.com/yaqub0r/AudioDeviceSwitcher/issues). It is independent of the original Microsoft Store listing, which does not include these changes.

## Device selection recovery

Commands retain their selected device IDs and remember each device's full name. If Windows replaces an ID, the app recovers it when exactly one device in the same playback/recording category has the saved full name. Exact IDs take priority. Ambiguous names are not guessed, and missing devices remain saved for when they return. Recovery runs on startup and before executing a saved command, including background hotkeys.

Existing settings remain readable. Names are learned automatically for saved IDs that Windows still recognizes. During migration from the Store app, the migration utility can also recover an old ID from an exact, unique entry in Windows' retained endpoint history. When neither a name nor endpoint history is available, the original selection is preserved without guessing. To discard an unavailable selection permanently, recreate that command. A present device can still be deselected normally.

## Development and validation

The existing app targets .NET 6 and Windows App SDK 1.1.5. These old dependencies need a separate modernization pass before a maintained release; this fix does not upgrade them. Building the WinUI app requires Visual Studio MSBuild with Windows application build tools, in addition to the .NET SDK. `dotnet build` alone may lack the Appx/PRI tasks.

Run the tests that do not change real audio devices:

```powershell
dotnet test test/AudioDeviceSwitcher.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName!~AudioPageViewModelTests"
```

The excluded `AudioPageViewModelTests` are hardware integration tests: they switch real default devices and change device visibility. The new `DeviceRecoveryTests` use mocks and cover the page's persistence behavior without those side effects. CI runs the safe suite and builds an unsigned x64 MSIX plus a migration utility; it does not install drivers, sign packages, or publish to the Microsoft Store.

## Installation

The package is named **Audio Device Switcher**, with identity `yaqub0r.AudioDeviceSwitcher`, publisher `CN=yaqub0r.AudioDeviceSwitcher`, and command alias `AudioDeviceSwitcher.exe`. Version 1.1.1 upgrades the earlier “(Personal)” build in place; its identity and settings store remain unchanged. The old `AudioDeviceSwitcherPersonal.exe` alias is retained for existing scripts. After migration and verification, uninstall the original Store app so shortcuts and hotkeys reach only this fork.

See [INSTALL.md](INSTALL.md) for signing, migration, installation preview, and rollback. CI artifacts are unsigned development builds. The signing key stays on the owner's PC and is never uploaded. Certificate trust is an explicit step; the installer never changes trust stores.


Audio Device Switcher is a Windows 10 app that makes it easy to quickly switch your default playback device as well as your recording device using hotkeys.

![AudioDeviceSwitcher](AudioDeviceSwitcher.png)

## Usage

### Active mode (application must stay open all the time)

1. Select one or more devices
2. Set up a hotkey
3. Go to settings and make sure "Run at startup & Run in the background" are enabled

When you press the hotkey, the app will cycle through the selected devices.

Clicking in the three dots menu will allow you to manage your commands (create, rename, delete).

### Offline mode (application can be closed)

1. Select one or more devices
2. Copy the generated command at the bottom
3. Open your favorite software that allows you to bind keys to commands such as a modern keyboard gaming software and paste it there
4. Go to settings and disable "Run at startup & Run in the background"

## License

This repository is licensed with the Apache, Version 2.0 license.
