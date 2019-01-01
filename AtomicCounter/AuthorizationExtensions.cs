using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AtomicCounter.Models;
using AtomicCounter.Services;
using Newtonsoft.Json;

namespace AtomicCounter
{
    public enum KeyMode
    {
        Read = 0,
        Write = 1,
        Duplex = 2
    }

    internal static class AuthorizationExtensions
    {
        internal static async Task<T> AuthorizeAppAndExecute<T>(this HttpRequestMessage req, KeyMode mode, string tenant, Func<Task<T>> action, T otherwise)
        {
            var key = req
                .GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Equals(q.Key, "key", StringComparison.OrdinalIgnoreCase))
                .Value;

            if (key == null)
            {
                return otherwise;
            }

            var existing = await AppStorage.GetTenantAsync(tenant);

            if (existing == null)
            {
                return otherwise;
            }

            HashSet<string> target = null;
            switch(mode)
            {
                case KeyMode.Read:
                    target = existing.ReadKeys;
                    break;
                case KeyMode.Write:
                    target = existing.WriteKeys;
                    break;
                case KeyMode.Duplex:
                    throw new NotImplementedException();
            }

            if (!target.Any(x => CombineAndHash(tenant, x) == key))
            {
                return otherwise;
            }

            return await action();
        }

        public static string CombineAndHash(string a, string b)
        {
            HashAlgorithm sha = new SHA1CryptoServiceProvider();
            byte[] result = sha.ComputeHash(Encoding.UTF8.GetBytes(a + b));
            return Convert.ToBase64String(result);
        }

        internal static async Task<T> AuthorizeUserAndExecute<T>(this HttpRequestMessage req, Func<UserProfile, Task<T>> action, T otherwise)
        {
            if (!Thread.CurrentPrincipal.Identity.IsAuthenticated)
            {
                return otherwise;
            }

            var authInfo = await req.GetAuthInfoAsync();
            var userName = $"{authInfo.ProviderName}|{authInfo.GetClaim(ClaimTypes.NameIdentifier).Value}";

            return await action(await AppStorage.GetOrCreateUserProfileAsync(userName));
        }

        private static HttpClient _httpClient = new HttpClient(); // cache and reuse to avoid repeated creation on Function calls

        /// <summary>
        /// Find a claim of the specified type
        /// </summary>
        /// <param name="authInfo"></param>
        /// <param name="claimType"></param>
        /// <returns></returns>
        public static AuthUserClaim GetClaim(this AuthInfo authInfo, string claimType)
        {
            return authInfo.UserClaims.FirstOrDefault(c => c.Type == claimType);
        }

        /// <summary>
        /// Get the EasyAuth properties for the currently authenticated user
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<AuthInfo> GetAuthInfoAsync(this HttpRequestMessage request)
        {
            var zumoAuthToken = request.GetZumoAuthToken();
            if (string.IsNullOrEmpty(zumoAuthToken))
            {
                return null;
            }

            var authMeRequest = new HttpRequestMessage(HttpMethod.Get, GetEasyAuthEndpoint())
            {
                Headers =
                    {
                        { "X-ZUMO-AUTH", zumoAuthToken }
                    }
            };

            var response = await _httpClient.SendAsync(authMeRequest);
            var authInfoArray = await response.Content.ReadAsAsync<AuthInfo[]>();
            return authInfoArray.Length >= 1 ? authInfoArray[0] : null; // The .auth/me content is a single item array if it is populated
        }

        private static string GetEasyAuthEndpoint()
        {
            // Get the hostname from environment variables so that we don't need config - thank you App Service!
            var hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            // Build up the .auth/me url
            var requestUri = $"https://{hostname}/.auth/me";
            return requestUri;
        }

        private static string GetZumoAuthToken(this HttpRequestMessage req)
        {
            return req.Headers.GetValues("X-ZUMO-AUTH").FirstOrDefault();
        }
    }

    public class AuthInfo // structure based on sample here: https://cgillum.tech/2016/03/07/app-service-token-store/
    {
        [JsonProperty("access_token", NullValueHandling = NullValueHandling.Ignore)]
        public string AccessToken { get; set; }
        [JsonProperty("provider_name", NullValueHandling = NullValueHandling.Ignore)]
        public string ProviderName { get; set; }
        [JsonProperty("user_id", NullValueHandling = NullValueHandling.Ignore)]
        public string UserId { get; set; }
        [JsonProperty("user_claims", NullValueHandling = NullValueHandling.Ignore)]
        public AuthUserClaim[] UserClaims { get; set; }
        [JsonProperty("access_token_secret", NullValueHandling = NullValueHandling.Ignore)]
        public string AccessTokenSecret { get; set; }
        [JsonProperty("authentication_token", NullValueHandling = NullValueHandling.Ignore)]
        public string AuthenticationToken { get; set; }
        [JsonProperty("expires_on", NullValueHandling = NullValueHandling.Ignore)]
        public string ExpiresOn { get; set; }
        [JsonProperty("id_token", NullValueHandling = NullValueHandling.Ignore)]
        public string IdToken { get; set; }
        [JsonProperty("refresh_token", NullValueHandling = NullValueHandling.Ignore)]
        public string RefreshToken { get; set; }
    }

    public class AuthUserClaim
    {
        [JsonProperty("typ")]
        public string Type { get; set; }
        [JsonProperty("val")]
        public string Value { get; set; }
    }
}
