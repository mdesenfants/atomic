using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class GetCounters
    {
        public static IAuthorizationProvider AuthProvider { get; set; } = new AuthorizationProvider();

        [FunctionName(nameof(GetCounters))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "counters")]HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Getting counters for user profile.");

            return await AuthProvider.AuthorizeUserAndExecute(req, async profile =>
            {
                return await Task.FromResult(new OkObjectResult(profile.Counters)).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
