using AtomicCounter.Models;
using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public class AuthorizationProvider : IAuthorizationProvider
    {
        public async Task<IActionResult> AuthorizeAppAndExecute(HttpRequest req, KeyMode mode, string counter, Func<Task<IActionResult>> action)
        {
            try
            {
                var key = req.Query["key"].FirstOrDefault();

                if (key == null)
                {
                    return new UnauthorizedResult();
                }

                var existing = await AppStorage.GetCounterMetadataAsync(counter);

                if (existing == null)
                {
                    return new NotFoundObjectResult($"No existing counter named {counter}.");
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

                if (!target.Any(x => AuthorizationHelpers.CombineAndHash(counter, x) == key))
                {
                    return new UnauthorizedResult();
                }

                return await action();
            }
            catch (InvalidOperationException)
            {
                return new BadRequestResult();
            }
            catch
            {
                return new UnauthorizedResult();
            }
        }

        public async Task<IActionResult> AuthorizeUserAndExecute(HttpRequest req, Func<UserProfile, Task<IActionResult>> action)
        {
            try
            {
                var authInfo = await req?.GetAuthInfoAsync();
                var userName = $"stripe|{authInfo.UserId}";

                return await action(await AppStorage.GetOrCreateUserProfileAsync(userName, authInfo.AccessToken));
            }
            catch (InvalidOperationException)
            {
                return new BadRequestResult();
            }
            catch (UnauthorizedAccessException)
            {
                return new UnauthorizedResult();
            }
        }

        public async Task<IActionResult> AuthorizeUserAndExecute(HttpRequest req, string counter, Func<UserProfile, Counter, Task<IActionResult>> action)
        {
            try
            {
                if (!AppStorage.CounterNameIsValid(counter))
                {
                    return new BadRequestResult();
                }

                return await AuthorizeUserAndExecute(req, async profile =>
                {
                    // Throw unauthorized exception here when necessary
                    var meta = await AppStorage.GetCounterMetadataAsync(profile, counter);

                    if (meta == null)
                    {
                        return new NotFoundResult();
                    }

                    // Continue with action
                    return await action(profile, meta);
                });
            }
            catch (InvalidOperationException)
            {
                return new BadRequestResult();
            }
            catch (UnauthorizedAccessException)
            {
                return new UnauthorizedResult();
            }
        }
    }
}
