// Error codes of the Corsair Game SDK, as listed by the Open CUE Service (Apache License 2.0).
using System;

namespace IcueHelper.GameSdk
{
    /// <summary>
    /// Error codes returned by CgSdkGetLastError.
    /// </summary>
    public enum CgSdkError
    {
        Unknown = -1,
        Success = 0,
        ServerNotFound = 1,
        NoControl = 2,
        ProtocolHandshakeMissing = 3,
        IncompatibleProtocol = 4,
        InvalidArguments = 5,
        ProfilesConfigurationProblem = 7,
        StateNotSet = 8
    }

    /// <summary>
    /// Thrown when a Game SDK call fails.
    /// </summary>
    public class CgSdkException : Exception
    {
        /// <summary>
        /// Gets the error reported by the SDK.
        /// </summary>
        public CgSdkError Error { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the native library itself could not be loaded,
        /// which means the Game SDK is simply not installed.
        /// </summary>
        public bool IsNativeLibraryMissing { get; private set; }

        internal CgSdkException(CgSdkError error)
            : base(Describe(error))
        {
            Error = error;
        }

        internal CgSdkException(string message, Exception innerException, bool nativeLibraryMissing)
            : base(message, innerException)
        {
            Error = CgSdkError.Unknown;
            IsNativeLibraryMissing = nativeLibraryMissing;
        }

        /// <summary>
        /// Gets a readable description of an error code.
        /// </summary>
        public static string Describe(CgSdkError error)
        {
            switch (error)
            {
                case CgSdkError.Success:
                    return "success";
                case CgSdkError.ServerNotFound:
                    return "iCUE is not running, or the Game SDK is disabled in the iCUE settings";
                case CgSdkError.NoControl:
                    return "another application took exclusive control of the Game SDK";
                case CgSdkError.ProtocolHandshakeMissing:
                    return "the handshake with iCUE was not performed";
                case CgSdkError.IncompatibleProtocol:
                    return "this Game SDK version is not compatible with this iCUE version";
                case CgSdkError.InvalidArguments:
                    return "the profiles directory does not exist or is not configured correctly";
                case CgSdkError.ProfilesConfigurationProblem:
                    return "problem with the profiles or with priorities.cfg";
                case CgSdkError.StateNotSet:
                    return "the profile was not active";
                default:
                    return "unknown Game SDK error";
            }
        }
    }
}
