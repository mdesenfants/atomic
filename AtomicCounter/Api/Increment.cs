using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace AtomicCounter.Api
{
    public static class Increment
    {
        [FunctionName("Increment")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenant/{tenant}/app/{app}/counter/{counter}/increment")]HttpRequestMessage req,
            TraceWriter log,
            string tenant,
            string app,
            string counter)
        {
            log.Info($"Incrementing {tenant}/{app}/{counter}");
            var count = GetCount(req);

            try
            {
                return await req.AuthorizeAndExecute(async () =>
                {
                    await AppStorage.SendIncrementEventAsync(tenant, app, counter, count);
                    return req.CreateResponse(HttpStatusCode.Accepted);
                });
            }
            catch (InvalidOperationException e) when (e.Message == AuthorizationExtensions.UnauthorizedMessage)
            {
                log.Warning(e.Message);
                return req.CreateResponse(HttpStatusCode.Unauthorized, "Provide a valid token to the key query parameter.");
            }
        }

        private static long GetCount(HttpRequestMessage req)
        {
            var countString = req
                .GetQueryNameValuePairs()
                .FirstOrDefault(p => p.Key.Equals("count", StringComparison.OrdinalIgnoreCase))
                .Value ?? string.Empty;

            long count = 1;
            if (long.TryParse(countString, out var parsed))
            {
                count = parsed;
            }

            return count;
        }
    }
}
