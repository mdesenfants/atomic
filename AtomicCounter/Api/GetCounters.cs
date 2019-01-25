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
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName(nameof(GetCounters))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "counters")]HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Getting counters for user profile.");

            return await AuthProvider.AuthorizeUserAndExecute(req, async profile =>
            {
                return await Task.FromResult(new OkObjectResult(profile.Counters));
            });
        }
    }
}
