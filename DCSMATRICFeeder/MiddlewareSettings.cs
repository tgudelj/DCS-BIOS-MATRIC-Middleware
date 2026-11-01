using System.ComponentModel;

using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace EXM.DBMM {
    /// <summary>
    /// Represents DCS-BIOS MATRIC middleware configuration
    /// </summary>
    internal class MiddlewareSettings {
        public MiddlewareSettings() {
            ListenAddress = "239.255.50.10";
            ListenPort = 5010;
            ImportPort = 7778;
            DCSBIOSJsonPath = "";
            AircraftVariables = new Dictionary<string, List<string>>();
            UpdateFrequency = 10;
        }

        /// <summary>
        /// IP address to listen on for DCS-BIOS, default is multicast address 239.255.50.10
        /// </summary>
        public string ListenAddress {  get; set; }

        /// <summary>
        /// Port to listen on for DCS-BIOS, default 5010
        /// </summary>
        public int ListenPort { get; set; }

        /// <summary>
        /// Port for DCS-BIOS import messages, default 7778
        /// </summary>
        public int ImportPort { get; set; }

        /// <summary>
        /// Path to DCS-BIOS doc/json directory, default is "%userprofile%/Saved Games/DCS.openbeta/Scripts/DCS-BIOS/doc/json/"
        /// </summary>
        public string DCSBIOSJsonPath {  get; set; }

        /// <summary>
        /// Chosen variables list per aircraft
        /// key - aircraft name
        /// value - list of variables to forward to MATRIC
        /// </summary>
        public Dictionary<string, List<string>> AircraftVariables { get; set; }

        /// <summary>
        /// Desired updates per second
        /// </summary>
        public int UpdateFrequency { get; set; }

        internal void Validate() {
            if (!IPAddress.TryParse(ListenAddress, out var address) || address.AddressFamily != AddressFamily.InterNetwork ||
                address.GetAddressBytes()[0] is < 224 or > 239)
                throw new ArgumentException("Enter an IPv4 multicast listener address (224.0.0.0 to 239.255.255.255).");
            if (ListenPort is < 1 or > 65535 || ImportPort is < 1 or > 65535)
                throw new ArgumentException("Ports must be between 1 and 65535.");
            if (UpdateFrequency is < 1 or > 25) throw new ArgumentException("Update frequency must be between 1 and 25.");
            ValidateSelections(AircraftVariables);
        }

        internal static Dictionary<string, List<string>> ParseSelections(string json) {
            if (string.IsNullOrWhiteSpace(json)) return new();
            var selections = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
                ?? throw new JsonSerializationException("Variable selections must be a JSON object.");
            ValidateSelections(selections);
            return selections.ToDictionary(p => p.Key, p => p.Value.Distinct(StringComparer.Ordinal).ToList());
        }

        private static void ValidateSelections(Dictionary<string, List<string>> selections) {
            if (selections == null || selections.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value == null || p.Value.Any(string.IsNullOrWhiteSpace)))
                throw new JsonSerializationException("Each module must have a list of nonempty variable names.");
        }
    }
}
