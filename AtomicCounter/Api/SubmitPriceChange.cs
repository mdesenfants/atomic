using AtomicCounter.Models.Events;
using AtomicCounter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace AtomicCounter.Api
{
    public static class SubmitPriceChange
    {
        public static IAuthorizationProvider AuthProvider = new AuthorizationProvider();

        [FunctionName(nameof(SubmitPriceChange))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "counter/{counter}/price")]HttpRequest req,
            string counter,
            ILogger log)
        {
            log.LogInformation($"Changing price for {counter}.");

            return await AuthProvider.AuthorizeUserAndExecute(
                req,
                counter,
                async (profile, client) =>
                {
                    using (var str = new StreamReader(req.Body))
                    {
                        var body = await str.ReadToEndAsync();
                        var change = body.FromJson<PriceChangeEvent>();

                        var vc = new ValidationContext(change);
                        var errors = new List<ValidationResult>();
                        if (!Validator.TryValidateObject(change, vc, errors, true))
                        {
                            return new BadRequestObjectResult(errors);
                        }

                        await AppStorage.SendPriceChangeEventAsync(change);

                        return new AcceptedResult();
                    }
                });
        }
    }
}
