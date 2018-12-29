using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
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
            var user = (ClaimsPrincipal)Thread.CurrentPrincipal;

            if (!user.Identity.IsAuthenticated)
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            return req.CreateResponse(HttpStatusCode.OK, user?.Claims?.FirstOrDefault(x => x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value);
        }
    }
}
