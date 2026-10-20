using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace icue2mqtt.OpenCue
{
    /// <summary>
    /// Keeps track of who drives the lighting: this service ("Manual" mode), iCUE itself ("iCUE" mode)
    /// or one of the iCUE profiles activated through Open CUE.
    /// The value is exposed to Home Assistant as a select entity.
    /// </summary>
    internal class ProfileModeManager
    {
        /// <summary>
        /// Lighting is driven by this service (colors and brightness sent from Home Assistant).
        /// All the Open CUE profiles are deactivated.
        /// </summary>
        internal const string ModeManual = "Manual";

        /// <summary>
        /// Lighting is driven by iCUE itself: no profile active and this service does not draw anything.
        /// </summary>
        internal const string ModeIcue = "iCUE";

        private readonly OpenCueClient client;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private volatile List<OpenCueProfile> profiles = new List<OpenCueProfile>();
        private bool unavailableLogged;

        internal ProfileModeManager(OpenCueClient client)
        {
            this.client = client;
        }

        /// <summary>
        /// Gets the current mode: <see cref="ModeManual"/>, <see cref="ModeIcue"/> or a profile name.
        /// </summary>
        internal string Current { get; private set; } = ModeManual;

        /// <summary>
        /// Gets a value indicating whether this service is the one drawing the lighting.
        /// </summary>
        internal bool IsManual
        {
            get { return Current == ModeManual; }
        }

        /// <summary>
        /// Gets the options offered to Home Assistant: the two modes, then the profiles.
        /// </summary>
        internal List<string> GetOptions()
        {
            List<string> options = new List<string> { ModeManual, ModeIcue };
            foreach (OpenCueProfile profile in profiles)
            {
                string name = profile.DisplayName;
                if (name.Length > 0 && !options.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    options.Add(name);
                }
            }
            return options;
        }

        /// <summary>
        /// Reloads the profile list from Open CUE and detects profiles activated from outside
        /// (open-cue-cli, another MQTT bridge...).
        /// </summary>
        /// <returns><c>true</c> if the options or the current mode changed.</returns>
        internal async Task<bool> RefreshAsync()
        {
            List<OpenCueProfile> fresh;
            try
            {
                fresh = await client.GetProfilesAsync().ConfigureAwait(false);
                if (unavailableLogged)
                {
                    Logger.LogInformation("Open CUE service is reachable again");
                    unavailableLogged = false;
                }
            }
            catch (Exception ex)
            {
                if (!unavailableLogged)
                {
                    Logger.LogInformation(string.Format("Open CUE service unreachable at {0} ({1}). Profile switching is unavailable until it is started.", client.BaseUrl, ex.Message));
                    unavailableLogged = true;
                }
                return false;
            }

            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                string before = Signature();
                profiles = fresh;

                if (Current == ModeManual || Current == ModeIcue)
                {
                    // A profile was activated behind our back: reflect it.
                    OpenCueProfile active = fresh
                        .Where(p => p.State && IsProfileName(p.DisplayName))
                        .OrderByDescending(p => p.Priority)
                        .FirstOrDefault();
                    if (active != null)
                    {
                        Current = active.DisplayName;
                    }
                }
                else if (!GetOptions().Contains(Current, StringComparer.OrdinalIgnoreCase))
                {
                    // The current profile no longer exists.
                    Current = ModeIcue;
                }

                string after = Signature();
                if (before != after)
                {
                    Logger.LogInformation(string.Format("iCUE profiles: {0} (current: {1})",
                        string.Join(", ", fresh.Select(p => string.Format("{0} [priority {1}, {2}]", p.DisplayName, p.Priority, p.State ? "active" : "inactive"))),
                        Current));
                }
                return before != after;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Selects a mode or a profile.
        /// </summary>
        /// <param name="option">One of the options returned by <see cref="GetOptions"/>.</param>
        /// <returns><c>true</c> if the requested option is now the current one.</returns>
        internal async Task<bool> SelectAsync(string option)
        {
            string requested = (option ?? string.Empty).Trim();
            if (requested.Length == 0)
            {
                return false;
            }

            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                bool manual = string.Equals(requested, ModeManual, StringComparison.OrdinalIgnoreCase);
                if (manual || string.Equals(requested, ModeIcue, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await client.DeactivateAllProfilesAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Open CUE not installed or not started: nothing we know of is active, so carry on.
                        Logger.LogInformation("Open CUE: could not deactivate the profiles (" + ex.Message + ")");
                    }
                    Current = manual ? ModeManual : ModeIcue;
                    return true;
                }

                OpenCueProfile profile = FindProfile(requested);
                if (profile == null)
                {
                    // The list may be outdated (profile added since the last refresh).
                    try
                    {
                        profiles = await client.GetProfilesAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInformation("Open CUE: could not list the profiles (" + ex.Message + ")");
                    }
                    profile = FindProfile(requested);
                }
                if (profile == null)
                {
                    Logger.LogInformation(string.Format("Unknown iCUE profile '{0}'", requested));
                    return false;
                }

                try
                {
                    await client.TriggerProfileAsync(profile.Name).ConfigureAwait(false);
                    Current = profile.DisplayName;
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(string.Format("Open CUE: could not activate profile '{0}'", profile.DisplayName), ex);
                    return false;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        private OpenCueProfile FindProfile(string displayName)
        {
            return profiles.FirstOrDefault(p => string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsProfileName(string name)
        {
            return name.Length > 0
                && !string.Equals(name, ModeManual, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, ModeIcue, StringComparison.OrdinalIgnoreCase);
        }

        private string Signature()
        {
            return string.Join("|", GetOptions()) + "#" + Current;
        }
    }
}
