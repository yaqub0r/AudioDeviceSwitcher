// Copyright (c) 2021 Jose Torres. All rights reserved. Licensed under the Apache License, Version 2.0. See LICENSE.md file in the project root for full license information.

namespace AudioDeviceSwitcher;

public sealed record AudioSwitcherState
{
    public static string Repository { get; } = "https://github.com/yaqub0r/AudioDeviceSwitcher";
    public static string Issues { get; } = "https://github.com/yaqub0r/AudioDeviceSwitcher/issues";
    public static string Title { get; } = "Audio Device Switcher";
    public static string ExecutionAlias { get; } = "AudioDeviceSwitcher";
    public List<Command> Commands { get; set; } = new();
    public bool RunAtStartup { get; set; } = true;
    public bool RunAtStartupMinimized { get; set; } = true;
    public bool RunInBackground { get; set; } = true;
    public bool ShowDisabledDevices { get; set; } = false;
    public bool SwitchCommunicationDevice { get; set; } = false;
    public bool DarkTheme { get; set; } = true;
}
