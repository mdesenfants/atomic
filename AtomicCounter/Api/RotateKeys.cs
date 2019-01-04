using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AtomicCounter.Api
{
    public static class RotateKeys
    {
        public static IAuthorizationProvider Authorization = new AuthorizationProvider();

        [FunctionName("RotateKeys")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "{tenant}/keys/{readorwrite}/rotate")]HttpRequestMessage req,
            string tenant,
            string readorwrite,
            ILogger log)
        {
            log.LogInformation($"Start rotating {readorwrite} keys for {tenant}.");

            return await Authorization.AuthorizeUserAndExecute(req, async (user) =>
            {
                try
                {
                    var result = await AppStorage.RotateKeysAsync(user, tenant, (KeyMode)Enum.Parse(typeof(KeyMode), readorwrite, true));

                    if (result == null)
                    {
                        return req.CreateResponse(HttpStatusCode.NotFound);
                    }

                    return req.CreateResponse(HttpStatusCode.OK, result);

                }
                catch (Exception)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
            });
        }
    }
}
