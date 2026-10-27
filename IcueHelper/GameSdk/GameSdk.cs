using System;

namespace IcueHelper.GameSdk
{
    /// <summary>
    /// Wrapper around the Corsair Game SDK, which is the only way to activate an iCUE lighting profile.
    /// It is deliberately tolerant: when the native library is missing or iCUE is not running, the
    /// instance simply reports itself as unavailable instead of bringing the whole application down.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class GameSdk : IDisposable
    {
        private readonly object sync = new object();

        /// <summary>
        /// The "game" name expected by the SDK, which is in fact the name of the profiles sub directory.
        /// </summary>
        private readonly string gameName;

        private bool nativeLibraryMissing;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameSdk"/> class. No native call is made here,
        /// so creating the instance is always safe; call <see cref="Connect"/> to reach iCUE.
        /// </summary>
        /// <param name="gameName">The name of the profiles directory, for instance "profiles".</param>
        public GameSdk(string gameName)
        {
            this.gameName = string.IsNullOrWhiteSpace(gameName) ? "profiles" : gameName.Trim();
            ProtocolDetails = new CgSdkProtocolDetails();
        }

        /// <summary>
        /// Gets the name of the profiles directory passed to the SDK.
        /// </summary>
        public string GameName
        {
            get { return gameName; }
        }

        /// <summary>
        /// Gets the result of the last handshake.
        /// </summary>
        public CgSdkProtocolDetails ProtocolDetails { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the native library could be loaded.
        /// Once it is known to be missing, no further native call is attempted.
        /// </summary>
        public bool IsNativeLibraryAvailable
        {
            get { return !nativeLibraryMissing; }
        }

        /// <summary>
        /// Gets a value indicating whether iCUE answered the last handshake.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance currently holds the Game SDK control.
        /// The control is only taken while a profile has to be shown, so that it interferes as
        /// little as possible with the lighting SDK used for the manual mode.
        /// </summary>
        public bool HasControl { get; private set; }

        /// <summary>
        /// Performs the handshake with iCUE and declares the profiles directory.
        /// </summary>
        /// <returns><c>true</c> when iCUE answered and the profiles directory was accepted.</returns>
        /// <exception cref="CgSdkException">The native library is missing, or the SDK refused the call.</exception>
        public bool Connect()
        {
            lock (sync)
            {
                HasControl = false;
                IsConnected = false;
                ProtocolDetails = new CgSdkProtocolDetails(Call(CgSdkInterop.PerformProtocolHandshake));
                if (!ProtocolDetails.IsServerAvailable)
                {
                    return false;
                }
                if (ProtocolDetails.BreakingChanges)
                {
                    throw new CgSdkException(CgSdkError.IncompatibleProtocol);
                }
                // Taking the control once is what makes iCUE accept the profiles directory, exactly as
                // the Open CUE service does; it is released again as soon as no profile has to be shown.
                Check(Call(CgSdkInterop.RequestControl));
                HasControl = true;
                Check(Call(() => CgSdkInterop.SetGame(gameName)));
                IsConnected = true;
                return true;
            }
        }

        /// <summary>
        /// Checks that iCUE is still there, and reconnects when it was restarted.
        /// </summary>
        /// <returns><c>true</c> when the SDK is usable.</returns>
        public bool EnsureConnected()
        {
            lock (sync)
            {
                if (IsConnected)
                {
                    CgSdkProtocolDetails details = new CgSdkProtocolDetails(Call(CgSdkInterop.PerformProtocolHandshake));
                    ProtocolDetails = details;
                    if (details.IsServerAvailable)
                    {
                        return true;
                    }
                    // iCUE was closed or restarted: everything has to be set up again.
                    IsConnected = false;
                    HasControl = false;
                }
                return Connect();
            }
        }

        /// <summary>
        /// Activates a profile and keeps it active until it is cleared.
        /// </summary>
        /// <param name="profileName">The profile name, as declared in priorities.cfg.</param>
        public void SetState(string profileName)
        {
            lock (sync)
            {
                TakeControl();
                Check(Call(() => CgSdkInterop.SetState(profileName)));
            }
        }

        /// <summary>
        /// Deactivates a profile. A profile that was not active is not an error.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        public void ClearState(string profileName)
        {
            lock (sync)
            {
                if (!HasControl)
                {
                    return;
                }
                Check(Call(() => CgSdkInterop.ClearState(profileName)), CgSdkError.StateNotSet);
            }
        }

        /// <summary>
        /// Deactivates every profile and gives the Game SDK control back, so that iCUE or the
        /// lighting SDK can drive the devices again.
        /// </summary>
        public void ClearAllStates()
        {
            lock (sync)
            {
                if (!HasControl)
                {
                    return;
                }
                Check(Call(CgSdkInterop.ClearAllStates), CgSdkError.StateNotSet);
                ReleaseControl();
            }
        }

        /// <summary>
        /// Plays a profile once, as a one shot event.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        public void TriggerEvent(string profileName)
        {
            lock (sync)
            {
                TakeControl();
                Check(Call(() => CgSdkInterop.SetEvent(profileName)));
            }
        }

        /// <summary>
        /// Takes the Game SDK control if it is not held yet, and declares the profiles directory again.
        /// </summary>
        private void TakeControl()
        {
            if (!IsConnected)
            {
                throw new CgSdkException(CgSdkError.ServerNotFound);
            }
            if (HasControl)
            {
                return;
            }
            Check(Call(CgSdkInterop.RequestControl));
            HasControl = true;
            Check(Call(() => CgSdkInterop.SetGame(gameName)));
        }

        /// <summary>
        /// Gives the Game SDK control back. Failures are ignored: there is nothing useful to do about them.
        /// </summary>
        private void ReleaseControl()
        {
            if (!HasControl)
            {
                return;
            }
            try
            {
                Call(CgSdkInterop.ReleaseControl);
            }
            catch (CgSdkException)
            {
            }
            HasControl = false;
        }

        /// <summary>
        /// Runs a native call, turning a missing native library into a <see cref="CgSdkException"/>.
        /// </summary>
        private T Call<T>(Func<T> nativeCall)
        {
            if (nativeLibraryMissing)
            {
                throw new CgSdkException(
                    string.Format("{0} was not found next to the executable", CgSdkInterop.LibraryName), null, true);
            }
            try
            {
                return nativeCall();
            }
            catch (DllNotFoundException ex)
            {
                nativeLibraryMissing = true;
                throw new CgSdkException(
                    string.Format("{0} was not found next to the executable", CgSdkInterop.LibraryName), ex, true);
            }
            catch (EntryPointNotFoundException ex)
            {
                nativeLibraryMissing = true;
                throw new CgSdkException(
                    string.Format("{0} does not provide the expected Game SDK functions", CgSdkInterop.LibraryName), ex, true);
            }
            catch (BadImageFormatException ex)
            {
                nativeLibraryMissing = true;
                throw new CgSdkException(
                    string.Format("{0} could not be loaded, a 64 bit process is required", CgSdkInterop.LibraryName), ex, true);
            }
        }

        /// <summary>
        /// Turns a failed native call into an exception, unless the reported error is tolerated.
        /// </summary>
        private void Check(bool nativeResult, CgSdkError tolerated = CgSdkError.Success)
        {
            if (nativeResult)
            {
                return;
            }
            CgSdkError error = (CgSdkError)Call(CgSdkInterop.GetLastError);
            if (error == CgSdkError.Success || error == tolerated)
            {
                return;
            }
            if (error == CgSdkError.ServerNotFound)
            {
                IsConnected = false;
                HasControl = false;
            }
            throw new CgSdkException(error);
        }

        /// <summary>
        /// Clears the profiles and releases the control.
        /// </summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                try
                {
                    ClearAllStates();
                }
                catch (CgSdkException)
                {
                }
            }
        }
    }
}
