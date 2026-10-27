// Bindings for the Corsair Game SDK (CgSDK.x64_2015.dll).
// Derived from the Open CUE Service by Legion2 (https://github.com/Legion2/open-cue-service),
// distributed under the Apache License 2.0. See NOTICE at the root of this repository.
//
// The native library itself is supplied by Corsair Components, Inc. under different terms and is
// therefore NOT distributed with this project: it has to be placed next to the executable.
using System.Runtime.InteropServices;

namespace IcueHelper.GameSdk
{
    /// <summary>
    /// Raw entry points of the Corsair Game SDK.
    /// Every call throws <see cref="System.DllNotFoundException"/> when the native library is missing,
    /// so they are only ever called through <see cref="GameSdk"/>, which handles that case.
    /// </summary>
    internal static class CgSdkInterop
    {
        /// <summary>
        /// Name of the native library, looked up next to the executable.
        /// </summary>
        internal const string LibraryName = "CgSDK.x64_2015.dll";

        // The string parameters are const char*, so the default (ANSI) marshalling is the right one.
        [DllImport(LibraryName, EntryPoint = "CgSdkPerformProtocolHandshake", CallingConvention = CallingConvention.Cdecl)]
        internal static extern CgSdkProtocolDetailsNative PerformProtocolHandshake();

        [DllImport(LibraryName, EntryPoint = "CgSdkGetLastError", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetLastError();

        [DllImport(LibraryName, EntryPoint = "CgSdkRequestControl", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool RequestControl();

        [DllImport(LibraryName, EntryPoint = "CgSdkReleaseControl", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool ReleaseControl();

        [DllImport(LibraryName, EntryPoint = "CgSdkSetGame", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SetGame(string gameName);

        [DllImport(LibraryName, EntryPoint = "CgSdkSetState", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SetState(string stateName);

        [DllImport(LibraryName, EntryPoint = "CgSdkClearState", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool ClearState(string stateName);

        [DllImport(LibraryName, EntryPoint = "CgSdkClearAllStates", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool ClearAllStates();

        [DllImport(LibraryName, EntryPoint = "CgSdkSetEvent", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SetEvent(string eventName);

        [DllImport(LibraryName, EntryPoint = "CgSdkClearAllEvents", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool ClearAllEvents();
    }
}
