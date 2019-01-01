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
    public static class AddTenant
    {
        [FunctionName("AddTenant")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenant/{tenant}")]HttpRequestMessage req,
            string tenant,
            TraceWriter log)
        {
            return await req.AuthorizeUserAndExecute(
                async user =>
                {
                    var existing = await AppStorage.GetOrCreateTenantAsync(user, tenant);

                    return existing != null
                        ? req.CreateResponse(HttpStatusCode.OK, new TenantViewModel()
                        {
                            TenantName = existing.TenantName,
                            Origins = existing.Origins,
                            ReadKeys = null,
                            WriteKeys = null
                        })
                        : req.CreateResponse(HttpStatusCode.Unauthorized);
                },
                x => req.CreateResponse(HttpStatusCode.Unauthorized, x)
            );
        }
    }
}
