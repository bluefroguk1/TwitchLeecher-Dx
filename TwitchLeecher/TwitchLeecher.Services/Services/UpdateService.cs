using System.Text.Json.Nodes;
using System;
using System.Globalization;
using System.Net;
using System.Text;
using TwitchLeecher.Core.Models;
using TwitchLeecher.Services.Interfaces;
using TwitchLeecher.Shared.Extensions;
using TwitchLeecher.Shared.Reflection;

namespace TwitchLeecher.Services.Services
{
    internal class UpdateService : IUpdateService
    {
        #region Constants

        private const string LatestReleaseUrl = "https://github.com/schneidermanuel/TwitchLeecher-Dx/releases/tag/v{0}";
        private const string ReleasesApiUrl = "https://api.github.com/repos/schneidermanuel/TwitchLeecher-Dx/releases";

        #endregion Constants

        #region Methods

        public UpdateInfo CheckForUpdate()
        {
            try
            {
                using (HttpRequestClient webClient = new HttpRequestClient())
                {
                    webClient.AddHeader("User-Agent", "TwitchLeecher-Dx");

                    string result = webClient.DownloadString(ReleasesApiUrl);

                    JsonNode releasesJson = JsonNodeExtensions.Parse(result);

                    foreach (JsonNode releaseJson in releasesJson.AsArray())
                    {
                        bool draft = releaseJson.Value<bool>("draft");
                        bool prerelease = releaseJson.Value<bool>("prerelease");

                        if (!draft && !prerelease)
                        {
                            string tagStr = releaseJson.Value<string>("tag_name");
                            string releasedStr = releaseJson.Value<string>("published_at");
                            string infoStr = releaseJson.Value<string>("body");

                            Version releaseVersion = Version.Parse(tagStr.Substring(1)).Pad();
                            Version localVersion = AssemblyUtil.Get.GetAssemblyVersion().Pad();

                            DateTime released = DateTime.Parse(releasedStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

                            if (releaseVersion > localVersion)
                            {
                                return new UpdateInfo(releaseVersion, released, string.Format(LatestReleaseUrl, releaseVersion.ToString(3)), infoStr);
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Update check should not distract the application
            }

            return null;
        }

        #endregion Methods
    }
}