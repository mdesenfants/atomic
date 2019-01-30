using AtomicCounter.Models;
using AtomicCounter.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class GetCounter
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName(nameof(GetCounter))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "counter/{counter}")]HttpRequest req,
            string counter,
            ILogger log)
        {
            log.LogInformation("Getting info for counter {0}", counter);

            return await AuthProvider.AuthorizeUserAndExecute(req, counter, GetViewModel);
        }

        private static async Task<IActionResult> GetViewModel(UserProfile profile, Counter existing)
        {
            return await Task.FromResult(new OkObjectResult(new CounterViewModel()
            {
                CounterName = existing.CounterName,
                Origins = existing.Origins,
                ReadKeys = existing.ReadKeys
                    .Select(x => AuthorizationHelpers.CombineAndHash(existing.CounterName, x)),
                WriteKeys = existing.WriteKeys
                    .Select(x => AuthorizationHelpers.CombineAndHash(existing.CounterName, x)),
                PriceChanges = existing.PriceChanges
            }));
        }
    }
}
