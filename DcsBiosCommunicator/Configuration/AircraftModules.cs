using System;
using System.Collections.Generic;
using System.Linq;

namespace DcsBios.Communicator.Configuration;

public static class AircraftModules
{
    public static IReadOnlyList<string> Shared { get; } = Array.AsReadOnly(new[] { "CommonData", "MetadataStart", "MetadataEnd" });

    public static string[] Resolve(string aircraft, IEnumerable<AircraftBiosConfiguration> configurations) =>
        configurations.Where(c => !Shared.Contains(c.AircraftName) &&
                                  (c.AircraftName == aircraft || c.Aliases.Contains(aircraft)))
            .OrderByDescending(c => c.AircraftName == aircraft).ThenBy(c => c.AircraftName, StringComparer.Ordinal)
            .Select(c => c.AircraftName).ToArray();
}
