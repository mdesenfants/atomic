using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class CreateCounter
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName(nameof(CreateCounter))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenant/{tenant}/app/{app}/counter/{counter}")]HttpRequest req,
            string tenant,
            string app,
            string counter,
            ILogger log)
        {
            log.LogInformation("Creating counter {0}/{1}/{2}", tenant, app, counter);

            return await AuthProvider.AuthorizeUserAndExecute(
                req,
                async user =>
                {
                    await AppStorage.CreateCounterAsync(user, tenant, app, counter, log);
                    return new CreatedAtRouteResult(req.Path, 0);
                });
        }
    }
}
