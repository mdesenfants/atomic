using AtomicCounter.Models;
using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter
{
    public class AuthorizationProvider : IAuthorizationProvider
    {
        public async Task<IActionResult> AuthorizeAppAndExecute(HttpRequest req, KeyMode mode, string counter, Func<Task<IActionResult>> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (req == null)
            {
                throw new ArgumentNullException(nameof(req));
            }

            try
            {
                var key = req.Query["key"].FirstOrDefault();

                if (key == null)
                {
                    return new UnauthorizedResult();
                }

                var existing = await AppStorage.GetCounterMetadataAsync(counter).ConfigureAwait(false);

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

                return await action().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return new BadRequestResult();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
            {
                return new UnauthorizedResult();
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        public async Task<IActionResult> AuthorizeUserAndExecute(HttpRequest req, Func<UserProfile, Task<IActionResult>> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            try
            {
                var authInfo = await (req?.GetAuthInfoAsync()).ConfigureAwait(false);
                var userName = $"stripe|{authInfo.StripeUserId}";


                UserProfile userProfile = await ProfilesStorage.GetOrCreateUserProfileAsync(userName, authInfo.StripeUserId).ConfigureAwait(false);
                return await action(userProfile).ConfigureAwait(false);
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
                var canonicalName = counter.ToCanonicalName();
                if (!AppStorage.CounterNameIsValid(canonicalName))
                {
                    return new BadRequestResult();
                }

                return await AuthorizeUserAndExecute(req, async profile =>
                {
                    // Throw unauthorized exception here when necessary
                    var meta = await AppStorage.GetCounterMetadataAsync(profile, counter).ConfigureAwait(false);

                    if (meta == null)
                    {
                        return new NotFoundResult();
                    }

                    // Continue with action
                    return await action(profile, meta).ConfigureAwait(false);
                }).ConfigureAwait(false);
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
