// Copyright (c) 2021 Jose Torres. All rights reserved. Licensed under the Apache License, Version 2.0. See LICENSE.md file in the project root for full license information.

namespace AudioDeviceSwitcher.Core.Application;

public static class DeviceSelectionResolver
{
    // The caller supplies devices from the command's playback/recording class only.
    public static Command Resolve(Command command, IReadOnlyList<AudioDevice> availableDevices)
    {
        var names = new Dictionary<string, string>();
        var ids = new List<string>();
        var reserved = command.Devices.Where(id => availableDevices.Any(d => d.Id == id)).ToHashSet();

        foreach (var id in command.Devices)
        {
            var device = availableDevices.FirstOrDefault(d => d.Id == id);
            command.DeviceNames.TryGetValue(id, out var name);

            if (device == null && !string.IsNullOrWhiteSpace(name))
            {
                var candidates = availableDevices.Where(d => string.Equals(d.Name, name, StringComparison.Ordinal)).ToArray();
                var savedMatches = command.Devices.Count(savedId =>
                    command.DeviceNames.TryGetValue(savedId, out var savedName) && savedName == name);

                // Never guess between identically named endpoints or steal another selection.
                if (candidates.Length == 1 && savedMatches == 1 && !reserved.Contains(candidates[0].Id))
                    device = candidates[0];
            }

            var resolvedId = device?.Id ?? id;
            ids.Add(resolvedId);
            if (device != null)
            {
                names[resolvedId] = device.Name;
                reserved.Add(resolvedId);
            }
            else if (name != null)
            {
                names[resolvedId] = name;
            }
        }

        if (ids.SequenceEqual(command.Devices) && names.Count == command.DeviceNames.Count
            && names.All(pair => command.DeviceNames.TryGetValue(pair.Key, out var name) && name == pair.Value))
            return command;

        return command with { Devices = ids.ToArray(), DeviceNames = names };
    }
}
