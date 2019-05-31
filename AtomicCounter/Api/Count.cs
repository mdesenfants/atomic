using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class Count
    {
        public static IAuthorizationProvider AuthProvider { get; set; } = new AuthorizationProvider();

        private enum Mode : int
        {
            Client = 1,
            Date = 2,
        }

        [FunctionName(nameof(Count))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "counter/{counter}/count")]HttpRequest req,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Getting a count for counter/{counter}.");

            return await AuthProvider.AuthorizeAppAndExecute(req, KeyMode.Read, counter, async () =>
            {
                var query = req.GetQueryParameterDictionary();

                var mode = 0;
                if (query.TryGetValue("client", out var client))
                {
                    mode |= (int)Mode.Client;
                }

                var minParam = req.Query["min"].FirstOrDefault();
                var maxParam = req.Query["max"].FirstOrDefault();

                var min = DateTimeOffset.MinValue;
                try
                {
                    if (minParam != null)
                    {
                        min = DateTimeOffset.ParseExact(minParam, "o", CultureInfo.InvariantCulture);
                        mode |= (int)Mode.Date;
                    }
                }
                catch
                {
                    return new BadRequestObjectResult("Min query parameter timestamp must conform to ISO 8601.");
                }

                var max = DateTimeOffset.MaxValue;
                try
                {
                    if (maxParam != null)
                    {
                        max = DateTimeOffset.ParseExact(maxParam, "o", CultureInfo.InvariantCulture);
                        mode |= (int)Mode.Date;
                    }
                }
                catch
                {
                    return new BadRequestObjectResult("Max query parameter timestamp must conform to ISO 8601.");
                }

                var storage = new CountStorage(counter, log);
                long result = 0;

                if (mode == ((int)Mode.Date | (int)Mode.Client))
                {
                    result = await storage.CountAsync(client, min, max).ConfigureAwait(false);
                }
                else if (mode == (int)Mode.Date)
                {
                    result = await storage.CountAsync(min, max).ConfigureAwait(false);
                }
                else if (mode == (int)Mode.Client)
                {
                    result = await storage.CountAsync(client).ConfigureAwait(false);
                }
                else
                {
                    result = await storage.CountAsync().ConfigureAwait(false);
                }

                return new OkObjectResult(result);
            }).ConfigureAwait(false);
        }
    }
}
