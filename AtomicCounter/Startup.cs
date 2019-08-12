using AtomicCounter.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(AtomicCounter.Startup))]
namespace AtomicCounter
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Services.AddHttpClient();
            builder.Services.AddLogging();

            builder.Services.AddSingleton<CountQueueStorage>();
            builder.Services.AddSingleton<CountStorage>();
            builder.Services.AddSingleton<ProfilesStorage>();
            builder.Services.AddSingleton<RecreateEventsQueueStorage>();
            builder.Services.AddSingleton<ResetEventsQueueStorage>();
            builder.Services.AddSingleton<StripeStorage>();
        }
    }
}
