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
    public static class AddTenant
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName("AddTenant")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenant/{tenant}")]HttpRequest req,
            string tenant,
            ILogger log)
        {
            log.LogInformation("Creating tenant {0}", tenant);

            return await AuthProvider.AuthorizeUserAndExecute(
                req,
                async user =>
                {
                    var existing = await AppStorage.GetOrCreateTenantAsync(user, tenant);

                    if (existing == null) return new NotFoundResult();

                    return new OkObjectResult(new TenantViewModel()
                    {
                        TenantName = existing.TenantName,
                        Origins = existing.Origins,
                        ReadKeys = existing.ReadKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.TenantName, x)),
                        WriteKeys = existing.WriteKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.TenantName, x))
                    });
                });
        }
    }
}
