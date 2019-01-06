using System.Linq;
using System.Threading.Tasks;
using AtomicCounter.Models.ViewModels;
using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AtomicCounter.Api
{
    public static class GetTenant
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();
        [FunctionName("GetTenant")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/{tenant}")]HttpRequest req,
            string tenant,
            ILogger log)
        {
            log.LogInformation("Getting info for tenant {0}", tenant);

            return await AuthProvider.AuthorizeUserAndExecute(
                req,
                async user =>
                {
                    var existing = await AppStorage.GetTenantAsync(user, tenant);

                    return existing != null
                        ? new OkObjectResult(new TenantViewModel()
                        {
                            TenantName = existing.TenantName,
                            Origins = existing.Origins,
                            ReadKeys = existing.ReadKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.TenantName, x)),
                            WriteKeys = existing.WriteKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.TenantName, x))
                        })
                        : (IActionResult)new NotFoundResult();
                });
        }
    }
}
