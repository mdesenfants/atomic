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

                var minParam = req.Query["min"].FirstOrDefault();
                var maxParam = req.Query["max"].FirstOrDefault();

                var min = DateTimeOffset.MinValue;
                try
                {
                    if (minParam != null)
                    {
                        min = DateTimeOffset.ParseExact(minParam, "o", CultureInfo.InvariantCulture);
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
                    }
                }
                catch
                {
                    return new BadRequestObjectResult("Max query parameter timestamp must conform to ISO 8601.");
                }

                var storage = new CountStorage(counter, log);
                long result = 0;

                if (min != DateTimeOffset.MinValue || max != DateTimeOffset.MaxValue)
                {
                    result = await storage.CountAsync(min, max).ConfigureAwait(false);
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
