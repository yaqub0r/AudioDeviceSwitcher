// Copyright (c) 2021 Jose Torres. All rights reserved. Licensed under the Apache License, Version 2.0. See LICENSE.md file in the project root for full license information.

namespace AudioDeviceSwitcher;

using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        var version = Package.Current.Id.Version;
        Version = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        InitializeComponent();
    }

    public string? Version { get; }
}
