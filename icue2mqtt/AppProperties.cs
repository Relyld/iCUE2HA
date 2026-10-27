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
        /// Name of the Game SDK profiles directory, which is also the name the SDK calls the "game".
        /// Defaults to "profiles".
        /// </summary>
        public string profilesDirectory { get; set; }

        /// <summary>
        /// Full path of the GameSdkEffects directory, for instance
        /// C:\ProgramData\Corsair\CUE5\GameSdkEffects. Leave empty to detect the installed iCUE.
        /// </summary>
        public string gameSdkEffectsPath { get; set; }

        /// <summary>
        /// When true (default), a color/brightness/on-off command sent to a light switches back to
        /// the "Manual" mode, deactivating the active iCUE profile.
        /// </summary>
        public bool autoManualMode { get; set; } = true;
    }
}
