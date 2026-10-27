using IcueHelper.GameSdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace icue2mqtt.Profiles
{
    /// <summary>
    /// Keeps track of who drives the lighting: this service ("Manual" mode), iCUE itself ("iCUE" mode)
    /// or one of the iCUE profiles activated through the Game SDK.
    /// The value is exposed to Home Assistant as a select entity.
    /// </summary>
    internal class ProfileModeManager : IDisposable
    {
        /// <summary>
        /// Lighting is driven by this service: no profile is active and the colours sent from
        /// Home Assistant are shown through the lighting SDK.
        /// </summary>
        internal const string ModeManual = "Manual";

        /// <summary>
        /// Lighting is driven by iCUE itself: no profile is active and this service steps back.
        /// </summary>
        internal const string ModeIcue = "iCUE";

        private readonly GameSdk sdk;
        private readonly ProfileStore store;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        private List<IcueProfile> profiles = new List<IcueProfile>();
        private string activeProfileName;
        private string lastProblem;
        private bool loaded;

        internal ProfileModeManager(ProfileStore store)
        {
            this.store = store;
            sdk = new GameSdk(store.DirectoryName);
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
            if (sdk.IsNativeLibraryAvailable)
            {
                foreach (IcueProfile profile in profiles)
                {
                    if (!options.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        options.Add(profile.Name);
                    }
                }
            }
            return options;
        }

        /// <summary>
        /// Reloads priorities.cfg when it changed and makes sure iCUE is still reachable,
        /// re-applying the selected profile when iCUE was restarted.
        /// </summary>
        /// <returns><c>true</c> when the options or the current mode changed.</returns>
        internal async Task<bool> RefreshAsync()
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                string before = Signature();
                ReloadProfilesIfNeeded();

                if (sdk.IsNativeLibraryAvailable)
                {
                    bool wasConnected = sdk.IsConnected;
                    bool connected = TryWithSdk(() => sdk.EnsureConnected());
                    if (connected && !wasConnected)
                    {
                        Logger.LogInformation(string.Format("Game SDK connected: {0}", sdk.ProtocolDetails));
                        // iCUE was (re)started: nothing is active any more, so put the selection back.
                        if (activeProfileName != null)
                        {
                            Apply(activeProfileName);
                        }
                    }
                }

                return before != Signature();
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
        /// <returns><c>true</c> when the requested option is now the current one.</returns>
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
                if (string.Equals(requested, ModeManual, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(requested, ModeIcue, StringComparison.OrdinalIgnoreCase))
                {
                    bool manual = string.Equals(requested, ModeManual, StringComparison.OrdinalIgnoreCase);
                    Apply(null);
                    Current = manual ? ModeManual : ModeIcue;
                    return true;
                }

                ReloadProfilesIfNeeded();
                IcueProfile profile = profiles.FirstOrDefault(
                    candidate => string.Equals(candidate.Name, requested, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                {
                    Logger.LogInformation(string.Format("Unknown iCUE profile '{0}'", requested));
                    return false;
                }
                if (!sdk.IsNativeLibraryAvailable)
                {
                    Logger.LogInformation(string.Format(
                        "Cannot activate '{0}': the Game SDK library is not installed", profile.Name));
                    return false;
                }
                if (!Apply(profile.Name))
                {
                    return false;
                }
                Current = profile.Name;
                return true;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Activates one profile, or clears every profile when <paramref name="profileName"/> is null.
        /// </summary>
        /// <returns><c>true</c> when the Game SDK accepted the change.</returns>
        private bool Apply(string profileName)
        {
            if (profileName == null)
            {
                if (activeProfileName != null || sdk.HasControl)
                {
                    TryWithSdk(() => { sdk.ClearAllStates(); return true; });
                }
                activeProfileName = null;
                return true;
            }

            if (!sdk.IsConnected && !TryWithSdk(() => sdk.EnsureConnected()))
            {
                return false;
            }
            if (activeProfileName != null && !string.Equals(activeProfileName, profileName, StringComparison.OrdinalIgnoreCase))
            {
                string previous = activeProfileName;
                TryWithSdk(() => { sdk.ClearState(previous); return true; });
            }
            if (!TryWithSdk(() => { sdk.SetState(profileName); return true; }))
            {
                return false;
            }
            activeProfileName = profileName;
            return true;
        }

        /// <summary>
        /// Runs a Game SDK call, logging each distinct problem once instead of flooding the log.
        /// </summary>
        private bool TryWithSdk(Func<bool> call)
        {
            try
            {
                bool result = call();
                if (result && lastProblem != null)
                {
                    lastProblem = null;
                }
                return result;
            }
            catch (CgSdkException ex)
            {
                if (lastProblem != ex.Message)
                {
                    lastProblem = ex.Message;
                    Logger.LogInformation(string.Format("Game SDK unavailable: {0}", ex.Message));
                }
                return false;
            }
        }

        /// <summary>
        /// Reads priorities.cfg again when the file changed, and drops a selection that no longer exists.
        /// </summary>
        private void ReloadProfilesIfNeeded()
        {
            if (loaded && !store.HasChanged())
            {
                return;
            }
            loaded = true;
            string problem;
            List<IcueProfile> fresh = store.Load(out problem);
            if (problem != null)
            {
                if (lastProblem != problem)
                {
                    lastProblem = problem;
                    Logger.LogInformation(string.Format("iCUE profiles: {0}", problem));
                }
            }
            else
            {
                Logger.LogInformation(string.Format("iCUE profiles read from {0}: {1}",
                    store.PrioritiesFile,
                    fresh.Count == 0
                        ? "none declared"
                        : string.Join(", ", fresh.Select(p => string.Format("{0} [{1}]", p.Name, p.Priority)))));
            }
            profiles = fresh;

            if (Current != ModeManual && Current != ModeIcue
                && !profiles.Any(p => string.Equals(p.Name, Current, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogInformation(string.Format("Profile '{0}' is gone, giving the lighting back to iCUE", Current));
                Apply(null);
                Current = ModeIcue;
            }
        }

        /// <summary>
        /// Builds a value that changes whenever Home Assistant has to be told something new.
        /// </summary>
        private string Signature()
        {
            return string.Join("|", GetOptions()) + "#" + Current;
        }

        /// <summary>
        /// Clears the profiles and releases the Game SDK.
        /// </summary>
        public void Dispose()
        {
            sdk.Dispose();
            gate.Dispose();
        }
    }
}
