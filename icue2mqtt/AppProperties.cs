namespace icue2mqtt
{
    public class AppProperties
    {
        public string logPath { get; set; }
        public string mqttCredentialsPwd { get; set; }
        public string mqttCredentialsUser { get; set; }
        public string mqttUrl { get; set; }
        public string mqttOverridePort { get; set; }

        /// <summary>
        /// URL of the Open CUE service used to switch iCUE profiles. Defaults to http://localhost:25555.
        /// </summary>
        public string openCueUrl { get; set; }

        /// <summary>
        /// When true (default), a color/brightness/on-off command sent to a light switches back to
        /// the "Manual" mode, deactivating the active iCUE profile.
        /// </summary>
        public bool autoManualMode { get; set; } = true;
    }
}
