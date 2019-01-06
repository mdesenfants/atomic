using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class Count
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName("Count")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant/{tenant}/app/{app}/counter/{counter}/count")]HttpRequest req,
            string tenant,
            string app,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Getting a count for tenant/{tenant}/app/{app}/counter/{counter}.");

            return await AuthProvider.AuthorizeAppAndExecute(req, KeyMode.Read, tenant, async () =>
            {
                var storage = new CounterStorage(tenant, app, counter);
                try
                {
                    return req.CreateResponse(HttpStatusCode.OK, await storage.CountAsync());
                }
                catch (InvalidOperationException ioe)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound, ioe.Message);
                }
            });
        }
    }
}
