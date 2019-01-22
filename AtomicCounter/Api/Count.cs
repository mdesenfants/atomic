using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class Count
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName(nameof(Count))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "counter/{counter}/count")]HttpRequest req,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Getting a count for counter/{counter}.");

            return await AuthProvider.AuthorizeAppAndExecute(req, KeyMode.Read, counter, async () =>
            {
                var storage = new CountStorage(counter, log);
                return new OkObjectResult(await storage.CountAsync());
            });
        }
    }
}
