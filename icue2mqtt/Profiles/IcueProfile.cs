namespace icue2mqtt.Profiles
{
    /// <summary>
    /// An iCUE lighting profile, as declared in priorities.cfg.
    /// </summary>
    internal class IcueProfile
    {
        /// <summary>
        /// Gets the profile name, which is also the name shown in Home Assistant.
        /// </summary>
        internal string Name { get; private set; }

        /// <summary>
        /// Gets the priority of the profile. iCUE shows the active profile with the highest priority.
        /// </summary>
        internal int Priority { get; private set; }

        internal IcueProfile(string name, int priority)
        {
            Name = name;
            Priority = priority;
        }
    }
}
