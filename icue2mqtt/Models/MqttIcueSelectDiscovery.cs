using Newtonsoft.Json;
using System.Collections.Generic;

namespace icue2mqtt.Models
{
    /// <summary>
    /// MQTT discovery payload of the "iCUE Profile" select entity.
    /// </summary>
    internal class MqttIcueSelectDiscovery : JsonConvertableBase
    {
        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("unique_id")]
        public string UniqueId { get; private set; }

        [JsonProperty("state_topic")]
        public string StateTopic { get; private set; }

        [JsonProperty("command_topic")]
        public string CommandTopic { get; private set; }

        [JsonProperty("options")]
        public List<string> Options { get; private set; }

        [JsonProperty("icon")]
        public string Icon { get; private set; } = "mdi:led-strip-variant";

        internal MqttIcueSelectDiscovery(string name, string uniqueId, string stateTopic, string commandTopic, List<string> options)
        {
            this.Name = name;
            this.UniqueId = uniqueId;
            this.StateTopic = stateTopic;
            this.CommandTopic = commandTopic;
            this.Options = options;
        }
    }
}
