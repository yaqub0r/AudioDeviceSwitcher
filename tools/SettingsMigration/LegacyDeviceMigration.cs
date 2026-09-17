namespace AudioDeviceSwitcher.Migration;

using AudioDeviceSwitcher.Core.Application;

public static class LegacyDeviceMigration
{
    public static Command Recover(Command command, AudioDevice[] available, IReadOnlyDictionary<string, string[]> previousIds)
    {
        var ids = new List<string>();
        var names = new Dictionary<string, string>(command.DeviceNames);
        foreach (var oldId in command.Devices)
        {
            var id = oldId;
            if (!available.Any(device => device.Id == oldId))
            {
                var candidates = available.Where(device => previousIds.TryGetValue(device.Id, out var history)
                    && history.Contains(oldId, StringComparer.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length == 1)
                {
                    id = candidates[0].Id;
                    names.Remove(oldId);
                    names[id] = candidates[0].Name;
                }
            }

            if (!ids.Contains(id))
                ids.Add(id);
        }

        return DeviceSelectionResolver.Resolve(command with { Devices = ids.ToArray(), DeviceNames = names }, available);
    }
}
