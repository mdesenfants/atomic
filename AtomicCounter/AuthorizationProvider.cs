using AtomicCounter.Models;
using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public class AuthorizationProvider : IAuthorizationProvider
    {
        public async Task<IActionResult> AuthorizeAppAndExecute(HttpRequest req, KeyMode mode, string tenant, Func<Task<IActionResult>> action)
        {
            var key = req.Query["key"].FirstOrDefault();

            if (key == null)
            {
                return new UnauthorizedResult();
            }

            var existing = await AppStorage.GetTenantAsync(tenant);

            if (existing == null)
            {
                return new NotFoundObjectResult($"No existing tenant named {tenant}.");
            }

            IEnumerable<string> target = null;
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
                return new UnauthorizedResult();
            }

            return await action();
        }

        public async Task<IActionResult> AuthorizeUserAndExecute(HttpRequest req, Func<UserProfile, Task<IActionResult>> action)
        {
            if (!Thread.CurrentPrincipal.Identity.IsAuthenticated)
            {
                return new UnauthorizedResult();
            }

            var authInfo = await req.GetAuthInfoAsync();
            var userName = $"{authInfo.ProviderName}|{authInfo.GetClaim(ClaimTypes.NameIdentifier).Value}";

            return await action(await AppStorage.GetOrCreateUserProfileAsync(userName));
        }
    }
}
