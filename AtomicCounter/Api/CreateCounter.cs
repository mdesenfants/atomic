using AtomicCounter.Models.ViewModels;
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
    public static class CreateCounter
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName(nameof(CreateCounter))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "counter/{counter}")]HttpRequest req,
            string counter,
            ILogger log)
        {
            log.LogInformation("Creating counter {0}", counter);

            return await AuthProvider.AuthorizeUserAndExecute(
                req,
                async user =>
                {
                    var existing = await AppStorage.GetOrCreateCounterAsync(user, counter, log);

                    if (existing == null) return new ConflictResult();

                    return new OkObjectResult(new CounterViewModel()
                    {
                        CounterName = existing.CounterName,
                        Origins = existing.Origins,
                        ReadKeys = existing.ReadKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.CounterName, x)),
                        WriteKeys = existing.WriteKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.CounterName, x))
                    });
                });
        }
    }
}
