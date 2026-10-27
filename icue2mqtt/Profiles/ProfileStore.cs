using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace icue2mqtt.Profiles
{
    /// <summary>
    /// Reads the iCUE Game SDK profiles from the priorities.cfg file written next to the profile files.
    /// </summary>
    internal class ProfileStore
    {
        /// <summary>
        /// Sub directories of ProgramData holding the Game SDK effects, newest iCUE version first.
        /// </summary>
        private static readonly string[] GameSdkEffectsCandidates =
        {
            @"Corsair\CUE5\GameSdkEffects",
            @"Corsair\CUE4\GameSdkEffects"
        };

        private DateTime lastWriteTimeUtc;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileStore"/> class.
        /// </summary>
        /// <param name="gameSdkEffectsPath">Full path of the GameSdkEffects directory, or null to detect it.</param>
        /// <param name="profilesDirectoryName">Name of the profiles sub directory, "profiles" by default.</param>
        internal ProfileStore(string gameSdkEffectsPath, string profilesDirectoryName)
        {
            DirectoryName = string.IsNullOrWhiteSpace(profilesDirectoryName) ? "profiles" : profilesDirectoryName.Trim();
            string root = string.IsNullOrWhiteSpace(gameSdkEffectsPath) ? DetectGameSdkEffectsPath() : gameSdkEffectsPath.Trim();
            Directory = root == null ? null : Path.Combine(root, DirectoryName);
            PrioritiesFile = Directory == null ? null : Path.Combine(Directory, "priorities.cfg");
        }

        /// <summary>
        /// Gets the name of the profiles directory, which is also what the Game SDK calls the "game".
        /// </summary>
        internal string DirectoryName { get; private set; }

        /// <summary>
        /// Gets the full path of the profiles directory, null when no iCUE installation was found.
        /// </summary>
        internal string Directory { get; private set; }

        /// <summary>
        /// Gets the full path of the priorities.cfg file, null when no iCUE installation was found.
        /// </summary>
        internal string PrioritiesFile { get; private set; }

        /// <summary>
        /// Finds the GameSdkEffects directory of the installed iCUE version.
        /// </summary>
        /// <returns>The full path, or null when none exists.</returns>
        private static string DetectGameSdkEffectsPath()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            foreach (string candidate in GameSdkEffectsCandidates)
            {
                string path = Path.Combine(programData, candidate);
                if (System.IO.Directory.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a value indicating whether priorities.cfg changed since it was last read.
        /// </summary>
        internal bool HasChanged()
        {
            return GetLastWriteTimeUtc() != lastWriteTimeUtc;
        }

        private DateTime GetLastWriteTimeUtc()
        {
            if (PrioritiesFile == null)
            {
                return DateTime.MinValue;
            }
            try
            {
                return File.Exists(PrioritiesFile) ? File.GetLastWriteTimeUtc(PrioritiesFile) : DateTime.MinValue;
            }
            catch (IOException)
            {
                return lastWriteTimeUtc;
            }
            catch (UnauthorizedAccessException)
            {
                return lastWriteTimeUtc;
            }
        }

        /// <summary>
        /// Reads the profiles from priorities.cfg.
        /// </summary>
        /// <param name="problem">Set to a readable reason when the file could not be read.</param>
        /// <returns>The profiles, highest priority first; an empty list when the file is unusable.</returns>
        internal List<IcueProfile> Load(out string problem)
        {
            problem = null;
            List<IcueProfile> profiles = new List<IcueProfile>();
            if (PrioritiesFile == null)
            {
                problem = "no iCUE profiles directory found under ProgramData";
                return profiles;
            }

            DateTime stamp = GetLastWriteTimeUtc();
            string[] lines;
            try
            {
                lines = File.ReadAllLines(PrioritiesFile);
            }
            catch (FileNotFoundException)
            {
                problem = string.Format("{0} does not exist", PrioritiesFile);
                lastWriteTimeUtc = stamp;
                return profiles;
            }
            catch (DirectoryNotFoundException)
            {
                problem = string.Format("{0} does not exist", PrioritiesFile);
                lastWriteTimeUtc = stamp;
                return profiles;
            }
            catch (Exception ex)
            {
                problem = string.Format("{0} could not be read ({1})", PrioritiesFile, ex.Message);
                return profiles;
            }

            List<string> badLines = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    badLines.Add(rawLine);
                    continue;
                }
                // The names are trimmed: iCUE itself ignores the surrounding spaces, so a file written
                // with indentation would otherwise declare profiles that can never be activated.
                string name = line.Substring(0, separator).Trim();
                string priorityText = line.Substring(separator + 1).Trim();
                int priority;
                if (name.Length == 0 || !int.TryParse(priorityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out priority))
                {
                    badLines.Add(rawLine);
                    continue;
                }
                if (!seen.Add(name))
                {
                    continue;
                }
                profiles.Add(new IcueProfile(name, priority));
            }

            profiles.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            lastWriteTimeUtc = stamp;
            if (badLines.Count > 0)
            {
                problem = string.Format("{0} line(s) of {1} could not be read, for instance \"{2}\"",
                    badLines.Count, PrioritiesFile, badLines[0]);
            }
            return profiles;
        }
    }
}
