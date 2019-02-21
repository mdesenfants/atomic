using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Stripe;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public enum KeyMode
    {
        Read = 0,
        Write = 1,
        Duplex = 2
    }

    public static class AuthorizationHelpers
    {
        static AuthorizationHelpers()
        {
            StripeConfiguration.SetApiKey(Environment.GetEnvironmentVariable("StripeKey"));
        }

        public static string CombineAndHash(string a, string b)
        {
            HashAlgorithm sha = new SHA256Managed();
            var result = sha.ComputeHash(Encoding.UTF8.GetBytes(a + b));
            return Base64UrlEncoder.Encode(result);
        }

        public static async Task<OAuthToken> GetAuthInfoAsync(this HttpRequest request)
        {
            var value = GetAuthToken(request);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new UnauthorizedAccessException();
            }

            var first = value.IndexOf(' ') + 1;
            var length = value.Length - first - 1;
            var code = value.Substring(first, length);

            if (code.Length == 0)
            {
                throw new InvalidOperationException();
            }

            var tokenService = new OAuthTokenService();

            try
            {
                return await tokenService.CreateAsync(new OAuthTokenCreateOptions()
                {
                    ClientSecret = Environment.GetEnvironmentVariable("StripeSecKey"),
                    Code = code,
                    GrantType = "authorization_code"
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static readonly HttpClient _httpClient = new HttpClient();

        private static string GetAuthToken(this HttpRequest req)
        {
            var header = req.Headers["Authorization"];
            return header.FirstOrDefault();
        }
    }

    public class AuthInfo
    {
        [JsonProperty(PropertyName = "access_token")]
        public string AccessToken { get; set; }

        [JsonProperty(PropertyName = "livemode")]
        public string LiveMode { get; set; }

        [JsonProperty(PropertyName = "refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty(PropertyName = "token_type")]
        public string TokenType { get; set; }

        [JsonProperty(PropertyName = "stripe_publishable_key")]
        public string PublishableKey { get; set; }

        [JsonProperty(PropertyName = "stripe_user_id")]
        public string UserId { get; set; }

        [JsonProperty(PropertyName = "scope")]
        public string Scope { get; set; }
    }
}
