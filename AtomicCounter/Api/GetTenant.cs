using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AtomicCounter.Models.ViewModels;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace AtomicCounter.Api
{
    public static class GetTenant
    {
        [FunctionName("GetTenant")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/{tenant}")]HttpRequestMessage req,
            string tenant,
            TraceWriter log)
        {
            return await req.AuthorizeUserAndExecute(
                async user =>
                {
                    var existing = await AppStorage.GetTenantAsync(user, tenant);

                    return existing != null
                        ? req.CreateResponse(HttpStatusCode.OK, new TenantViewModel()
                        {
                            TenantName = existing.TenantName,
                            Origins = existing.Origins,
                            ReadKeys = existing.ReadKeys
                                .Select(x => AuthorizationExtensions.CombineAndHash(existing.TenantName, x)),
                            WriteKeys = existing.WriteKeys
                                .Select(x => AuthorizationExtensions.CombineAndHash(existing.TenantName, x))
                        })
                        : req.CreateResponse(HttpStatusCode.Unauthorized);
                },
                req.CreateResponse(HttpStatusCode.Unauthorized)
            );
        }
    }
}
