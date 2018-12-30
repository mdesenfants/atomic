using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class AddTenant
    {
        [FunctionName("AddTenant")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/{tenant}")]HttpRequestMessage req,
            string tenant,
            TraceWriter log)
        {
            return await req.AuthorizeUserAndExecute(
                async user => await Task.FromResult(req.CreateResponse(HttpStatusCode.OK, user)),
                async () => await Task.FromResult(req.CreateResponse(HttpStatusCode.Unauthorized))
            );
        }
    }
}
