// The protocol details structure is the one of the Corsair SDKs, as described by RGB.NET
// (https://github.com/DarthAffe/RGB.NET) and used by the Open CUE Service (Apache License 2.0).
using System;
using System.Runtime.InteropServices;

namespace IcueHelper.GameSdk
{
    /// <summary>
    /// Native layout of the structure returned by CgSdkPerformProtocolHandshake.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CgSdkProtocolDetailsNative
    {
        internal IntPtr sdkVersion;
        internal IntPtr serverVersion;
        internal int sdkProtocolVersion;
        internal int serverProtocolVersion;
        internal byte breakingChanges;
    }

    /// <summary>
    /// Result of the handshake with iCUE.
    /// </summary>
    public class CgSdkProtocolDetails
    {
        /// <summary>
        /// Gets the version of the SDK, always set even when iCUE is not running.
        /// </summary>
        public string SdkVersion { get; private set; }

        /// <summary>
        /// Gets the version of iCUE, or null when iCUE was not found.
        /// </summary>
        public string ServerVersion { get; private set; }

        /// <summary>
        /// Gets the protocol version implemented by the SDK.
        /// </summary>
        public int SdkProtocolVersion { get; private set; }

        /// <summary>
        /// Gets the protocol version implemented by iCUE, 0 when iCUE was not found.
        /// </summary>
        public int ServerProtocolVersion { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the SDK and iCUE protocols are incompatible.
        /// </summary>
        public bool BreakingChanges { get; private set; }

        /// <summary>
        /// Gets a value indicating whether iCUE answered the handshake.
        /// </summary>
        public bool IsServerAvailable
        {
            get { return ServerProtocolVersion > 0; }
        }

        internal CgSdkProtocolDetails(CgSdkProtocolDetailsNative native)
        {
            SdkVersion = native.sdkVersion == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(native.sdkVersion);
            ServerVersion = native.serverVersion == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(native.serverVersion);
            SdkProtocolVersion = native.sdkProtocolVersion;
            ServerProtocolVersion = native.serverProtocolVersion;
            BreakingChanges = native.breakingChanges != 0;
        }

        /// <summary>
        /// Initializes details describing an SDK that could not be reached at all.
        /// </summary>
        internal CgSdkProtocolDetails()
        {
        }

        public override string ToString()
        {
            return string.Format("SDK {0}, iCUE {1} (protocol {2}/{3})",
                SdkVersion ?? "?", ServerVersion ?? "not found", SdkProtocolVersion, ServerProtocolVersion);
        }
    }
}
