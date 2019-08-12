using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class Increment
    {
        public static IAuthorizationProvider AuthProvider { get; set; } = new AuthorizationProvider();

        [FunctionName(nameof(Increment))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "counter/{counter}/increment")]HttpRequest req,
            string counter,
            ILogger log)
        {
            if (req == null)
            {
                throw new ArgumentNullException(nameof(req));
            }
            log.LogInformation($"Incrementing {counter}.");
            long count = GetCount(req);

            return await AuthProvider.AuthorizeAppAndExecute(
                req,
                KeyMode.Write,
                counter,
                async () =>
                {
                    var cs = new CountStorage(counter, log);
                    var countParam = req.Query["count"].FirstOrDefault();
                    long increment = 1;

                    if (countParam != null && !long.TryParse(countParam, out increment))
                    {
                        return new BadRequestResult();
                    }

                    var valueParam = req.Query["value"].FirstOrDefault();
                    _ = decimal.TryParse(valueParam, out decimal value);


                    await cs.SendIncrementEventAsync(increment, value);

                    return new AcceptedResult();
                });
        }

        private static long GetCount(HttpRequest req)
        {
            var countString = req.Query["count"].FirstOrDefault() ?? string.Empty;

            long count = 1;
            if (long.TryParse(countString, out var parsed))
            {
                count = parsed;
            }

            return count;
        }
    }
}
