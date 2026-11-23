using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using TwitchLeecher.Shared.Extensions;

namespace TwitchLeecher.Core.Models
{
    public class AuthInfo
    {
        #region Constants

        public const string AUTHENTICATION_EL = "Authentication";

        private const string AUTHENTICATION_ACCESSTOKEN_EL = "AccessToken";
        private const string AUTHENTICATION_ACCESSTOKENSUBONLY_EL = "AccessTokenSubOnly";
        private const string PROTECTED_ATTR = "Protected";
        private const string PROTECTED_DPAPI = "DPAPI";

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TwitchLeecher-Dx.AuthInfo");

        #endregion Constants

        #region Properties

        public string AccessToken { get; set; }

        public string AccessTokenSubOnly { get; set; }

        #endregion Properties

        #region Methods

        public XElement GetXml()
        {
            if (string.IsNullOrWhiteSpace(AccessToken) && string.IsNullOrWhiteSpace(AccessTokenSubOnly))
            {
                return null;
            }

            XElement authenticationEl = new XElement(AUTHENTICATION_EL);

            if (!string.IsNullOrWhiteSpace(AccessToken))
            {
                authenticationEl.Add(GetTokenXml(AUTHENTICATION_ACCESSTOKEN_EL, AccessToken));
            }

            if (!string.IsNullOrWhiteSpace(AccessTokenSubOnly))
            {
                authenticationEl.Add(GetTokenXml(AUTHENTICATION_ACCESSTOKENSUBONLY_EL, AccessTokenSubOnly));
            }

            return authenticationEl;
        }

        private static XElement GetTokenXml(string name, string token)
        {
            XElement tokenEl = new XElement(name);

            if (OperatingSystem.IsWindows())
            {
                tokenEl.SetAttributeValue(PROTECTED_ATTR, PROTECTED_DPAPI);
                token = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser));
            }

            // shortcut: on Linux the token is stored as plain text (runtime.xml is made owner-only), use libsecret if Linux users ask for it.
            tokenEl.SetValue(token);
            return tokenEl;
        }

        private static string ReadToken(XElement tokenEl)
        {
            string value = tokenEl.GetValueAsString();

            // No attribute: plain text from Linux or from an older version. It gets protected on the next save.
            if (tokenEl.Attribute(PROTECTED_ATTR) == null)
            {
                return value;
            }

            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Token was protected on Windows.");
            }

            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser));
        }

        #endregion Methods

        #region Static Methods

        public static AuthInfo GetFromXml(XElement authenticationEl)
        {
            AuthInfo runtimeAuthInfo = new AuthInfo();

            if (authenticationEl != null)
            {
                XElement accessTokenEl = authenticationEl.Element(AUTHENTICATION_ACCESSTOKEN_EL);
                XElement accessTokenSubOnlyEl = authenticationEl.Element(AUTHENTICATION_ACCESSTOKENSUBONLY_EL);

                try
                {
                    if (accessTokenEl != null)
                    {
                        runtimeAuthInfo.AccessToken = ReadToken(accessTokenEl);
                    }

                    if (accessTokenSubOnlyEl != null)
                    {
                        runtimeAuthInfo.AccessTokenSubOnly = ReadToken(accessTokenSubOnlyEl);
                    }
                }
                catch
                {
                    // Malformed XML, or encrypted for another Windows user: the user signs in again
                    return null;
                }
            }

            if (runtimeAuthInfo.AccessToken != null || runtimeAuthInfo.AccessTokenSubOnly != null)
            {
                return runtimeAuthInfo;
            }

            return null;
        }

        #endregion Static Methods
    }
}
