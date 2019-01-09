using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AtomicCounter.Api
{
    public static class ResetCounter
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName("ResetCounter")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tenant/{tenant}/app/{app}/counter/{counter}")]HttpRequest req,
            string tenant,
            string app,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Resetting {tenant}/{app}/{counter}.");

            return await AuthProvider.AuthorizeUserAndExecute(req, async profile =>
            {
                var storage = new CounterStorage(tenant, app, counter);
                await storage.ResetAsync(profile);
                return new AcceptedResult();
            });
        }
    }
}
