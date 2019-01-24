using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class Increment
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName(nameof(Increment))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "counter/{counter}/increment")]HttpRequest req,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Incrementing {counter}.");
            var count = GetCount(req);

            return await AuthProvider.AuthorizeAppAndExecute(
                req,
                KeyMode.Write,
                counter,
                async () =>
                {
                    var cs = new CountStorage(counter, log);
                    var client = req.Query["client"].FirstOrDefault() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(client))
                    {
                        await cs.SendIncrementEventAsync(count, client);
                    }
                    else
                    {
                        await cs.SendIncrementEventAsync(count);
                    }

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
