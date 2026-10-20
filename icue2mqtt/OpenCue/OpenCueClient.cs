using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace icue2mqtt.OpenCue
{
    /// <summary>
    /// Minimal client for the REST API of the Open CUE service (https://github.com/Legion2/open-cue-service),
    /// which drives the iCUE Game SDK and therefore allows switching iCUE lighting profiles.
    /// </summary>
    internal class OpenCueClient : IDisposable
    {
        internal const string DefaultUrl = "http://localhost:25555";

        private readonly HttpClient http;

        /// <summary>
        /// Gets the base URL of the Open CUE service.
        /// </summary>
        internal string BaseUrl { get; }

        internal OpenCueClient(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = DefaultUrl;
            }
            BaseUrl = baseUrl.Trim().TrimEnd('/');
            http = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl + "/"),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        /// <summary>
        /// Lists the profiles known by Open CUE (those declared in priorities.cfg).
        /// </summary>
        internal async Task<List<OpenCueProfile>> GetProfilesAsync()
        {
            string json = await http.GetStringAsync("api/profiles").ConfigureAwait(false);
            List<OpenCueProfile> profiles = JsonConvert.DeserializeObject<List<OpenCueProfile>>(json);
            return profiles ?? new List<OpenCueProfile>();
        }

        /// <summary>
        /// Triggers (activates) a profile.
        /// </summary>
        /// <param name="name">The profile name exactly as returned by <see cref="GetProfilesAsync"/>.</param>
        internal async Task TriggerProfileAsync(string name)
        {
            string url = "api/profiles/" + Uri.EscapeDataString(name) + "/trigger";
            using (HttpResponseMessage response = await http.PostAsync(url, new StringContent(string.Empty)).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Deactivates every profile, so that iCUE falls back to its own lighting configuration.
        /// </summary>
        internal async Task DeactivateAllProfilesAsync()
        {
            using (HttpResponseMessage response = await http.PostAsync("api/sdk/deactivate-all-profiles", new StringContent(string.Empty)).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public void Dispose()
        {
            http.Dispose();
        }
    }
}
