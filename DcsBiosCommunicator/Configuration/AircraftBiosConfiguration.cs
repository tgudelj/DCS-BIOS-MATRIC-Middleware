using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DcsBios.Communicator.Configuration
{
    /// <summary>
    /// A nested dictionary class containing all bios commands and their properties for a specific aircraft
    /// </summary>
    public class AircraftBiosConfiguration : Dictionary<string, BiosCategory>
    {
        public string AircraftName { get; private set; } = null!;

        public HashSet<string> Aliases { get; private set; } = null!;

        /// <summary>
        /// Parses all json files within the provided directories into <see cref="AircraftBiosConfiguration"/>s (basically
        /// just nested dictionaries).
        /// </summary>
        /// <remarks>
        /// This assumes that the directory contains a single aliases file <paramref name="configDirectory"/>. Skips all unparseable files and logs accordingly.
        /// </remarks>
        /// <param name="configDirectory">Name of the file containing the aliases, e.g. <code>AircraftAliases.json</code></param>
        /// <param name="logger">Logger</param>
        /// <param name="configLocations">Directories containing json configuration files outlining all available DCS-BIOS inputs/outputs</param>
        /// <returns>Parsed aircraft configurations</returns>
        public static async Task<AircraftBiosConfiguration[]> AllConfigurations(string? configDirectory = null, ILogger<AircraftBiosConfiguration>? logger = null,
            params string[] configLocations)
        {
            var allFiles = configLocations.SelectMany(GetAllJsonFilesBelowDirectory).ToList();
            var aliasTasks = allFiles.Where(f => f.Name == configDirectory).Select(f => GetAliases(f, logger));
            var aliasResults = await Task.WhenAll(aliasTasks);

            var aliases = new Dictionary<string, HashSet<string>>();

            foreach (var result in aliasResults)
            {
                foreach (var (key, value) in result)
                {
                    if (!aliases.TryGetValue(key, out var names)) aliases[key] = names = new();
                    names.UnionWith(value);
                }
            }

            var configTasks = allFiles
                .Where(f => f.Name != configDirectory)
                .Select(f => BuildFromConfiguration(f, aliases, logger));

            var configurations = (await Task.WhenAll(configTasks)).Where(c => c != null).Select(c => c!).ToArray();
            foreach (var duplicate in configurations.GroupBy(c => c.AircraftName).Where(g => g.Count() > 1))
                logger?.LogWarning("Duplicate module {Module}; using the first configuration", duplicate.Key);
            return configurations.DistinctBy(c => c.AircraftName).ToArray();
        }

        /// <summary>
        /// Creates a configuration from a single json file
        /// </summary>
        /// <param name="configFile">File to create a configuration from</param>
        /// <param name="allAliases">Potential naming aliases to consider: BIOS module -> [DCS code alias]</param>
        /// <param name="logger">Logger</param>
        /// <returns></returns>
        public static async Task<AircraftBiosConfiguration?> BuildFromConfiguration(FileSystemInfo configFile, Dictionary<string, HashSet<string>>? allAliases = null, ILogger<AircraftBiosConfiguration>? logger = null)
        {
            try
            {
                var fileData = await File.ReadAllTextAsync(configFile.FullName);
                var dcsConfiguration = JsonConvert.DeserializeObject<AircraftBiosConfiguration>(fileData,
                    new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver
                        {
                            NamingStrategy = new SnakeCaseNamingStrategy(),
                        }
                    });

                if (dcsConfiguration is null)
                {
                    throw new JsonSerializationException();
                }

                foreach (var category in dcsConfiguration.Values)
                {
                    if (category is null) throw new JsonSerializationException("Category is null.");
                    foreach (var control in category.Values)
                        if (control is null || string.IsNullOrWhiteSpace(control.Identifier) || control.Outputs is null || control.Outputs.Any(o => o is null))
                            throw new JsonSerializationException("Control requires an identifier and an outputs list without null entries.");
                }

                dcsConfiguration.AircraftName = Path.GetFileNameWithoutExtension(configFile.FullName);
                dcsConfiguration.Aliases =
                    allAliases != null && allAliases.TryGetValue(dcsConfiguration.AircraftName, out var aircraftAliases)
                        ? aircraftAliases
                        : new HashSet<string>();

                return dcsConfiguration;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                logger?.LogWarning(ex, "Cannot load config file {ConfigFile}; skipping", configFile.FullName);
                return null;
            }
        }

        /// <summary>
        /// Returns all supported aliases.
        /// </summary>
        /// <param name="aliasFile">File where aliases are saved</param>
        /// <param name="logger"></param>
        /// <returns>BIOS module -> [DCS code]</returns>
        private static async Task<Dictionary<string, HashSet<string>>> GetAliases(FileSystemInfo aliasFile, ILogger<AircraftBiosConfiguration>? logger = null)
        {
            try
            {
                var fileData = await File.ReadAllTextAsync(aliasFile.FullName);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(fileData) ??
                           new Dictionary<string, HashSet<string>>();

                if (dict.Any(kvp => kvp.Value is null || kvp.Value.Any(string.IsNullOrWhiteSpace)))
                    throw new JsonSerializationException("Alias lists must contain module names.");
                var r = dict.SelectMany(kvp => kvp.Value.Select(module => (module, kvp.Key))).GroupBy(t => t.module)
                    .ToDictionary(g => g.Key, g2 => g2.Select(t => t.Key).ToHashSet());
                return r;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                logger?.LogWarning(ex, "Cannot load alias file {AliasFile}; skipping", aliasFile.FullName);
                return new Dictionary<string, HashSet<string>>();
            }
        }

        private static IEnumerable<FileInfo> GetAllJsonFilesBelowDirectory(string directory)
        {
            var finalPath = FullOrRelativePath(Environment.ExpandEnvironmentVariables(directory));
            return Directory.Exists(finalPath)
                ? new DirectoryInfo(finalPath).EnumerateFiles("*.json", SearchOption.AllDirectories)
                : Enumerable.Empty<FileInfo>();
        }

        private static string FullOrRelativePath(string path)
        {
            return Path.IsPathFullyQualified(path) ? path : Path.Combine(Environment.CurrentDirectory, path);
        }
    }
}
