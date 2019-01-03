using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AtomicCounter.Models.ViewModels;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace AtomicCounter.Api
{
    public static class AddTenant
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName("AddTenant")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenant/{tenant}")]HttpRequestMessage req,
            string tenant,
            ILogger log)
        {
            log.LogInformation("Creating tenant {0}", tenant);

            return await AuthProvider.AuthorizeUserAndExecute(req,
                async user =>
                {
                    var existing = await AppStorage.GetOrCreateTenantAsync(user, tenant);

                    return existing != null
                        ? req.CreateResponse(HttpStatusCode.OK, new TenantViewModel()
                        {
                            TenantName = existing.TenantName,
                            Origins = existing.Origins,
                            ReadKeys = existing.ReadKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.TenantName, x)),
                            WriteKeys = existing.WriteKeys
                                .Select(x => AuthorizationHelpers.CombineAndHash(existing.TenantName, x))
                        })
                        : req.CreateResponse(HttpStatusCode.Unauthorized);
                },
                x => req.CreateResponse(HttpStatusCode.Unauthorized, x)
            );
        }
    }
}
