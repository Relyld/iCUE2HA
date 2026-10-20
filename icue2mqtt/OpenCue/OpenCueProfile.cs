using Newtonsoft.Json;
using System;

namespace icue2mqtt.OpenCue
{
    /// <summary>
    /// A lighting profile as exposed by the Open CUE service (GET /api/profiles).
    /// </summary>
    internal class OpenCueProfile
    {
        /// <summary>
        /// Gets or sets the profile name exactly as known by Open CUE (it may contain leading/trailing
        /// whitespace when priorities.cfg is badly formatted, so always use it as-is when calling the API).
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("state")]
        public bool State { get; set; }

        /// <summary>
        /// Gets the name without surrounding whitespace, which is the name shown in Home Assistant.
        /// </summary>
        [JsonIgnore]
        internal string DisplayName
        {
            get { return (Name ?? string.Empty).Trim(); }
        }
    }
}
