using AtomicCounter.Models;
using AtomicCounter.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public class AuthorizationProvider : IAuthorizationProvider
    {
        public async Task<T> AuthorizeAppAndExecute<T>(HttpRequestMessage req, KeyMode mode, string tenant, Func<Task<T>> action, Func<string, T> otherwise)
        {
            var key = req
                .GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Equals(q.Key, "key", StringComparison.OrdinalIgnoreCase))
                .Value;

            if (key == null)
            {
                return otherwise($"No API key provided.");
            }

            var existing = await AppStorage.GetTenantAsync(tenant);

            if (existing == null)
            {
                return otherwise($"No existing tenant named {tenant}.");
            }

            HashSet<string> target = null;
            switch (mode)
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

            if (!target.Any(x => AuthorizationHelpers.CombineAndHash(tenant, x) == key))
            {
                return otherwise($"No matching keys for {Enum.GetName(typeof(KeyMode), mode)}.");
            }

            return await action();
        }

        public async Task<T> AuthorizeUserAndExecute<T>(HttpRequestMessage req, Func<UserProfile, Task<T>> action, Func<string, T> otherwise)
        {
            if (!Thread.CurrentPrincipal.Identity.IsAuthenticated)
            {
                return otherwise("Provide a valid X-ZUMO-AUTH token.");
            }

            var authInfo = await req.GetAuthInfoAsync();
            var userName = $"{authInfo.ProviderName}|{authInfo.GetClaim(ClaimTypes.NameIdentifier).Value}";

            return await action(await AppStorage.GetOrCreateUserProfileAsync(userName));
        }
    }
}
