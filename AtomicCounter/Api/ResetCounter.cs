using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class ResetCounter
    {
        public static IAuthorizationProvider AuthProvider { get; set; } = new AuthorizationProvider();

        [FunctionName(nameof(ResetCounter))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "counter/{counter}/reset")]HttpRequest req,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Resetting {counter}.");

            return await AuthProvider.AuthorizeUserAndExecute(
                req,
                counter,
                async (profile, meta) =>
                {
                    // Send reset event
                    await AppStorage.SendDeleteEventAsync(counter);
                    return new AcceptedResult();
                });
        }
    }
}
