using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class StripeStorage : AppStorage
    {
        public override async Task CreateStorage()
        {
            var stripes = Blobs.GetContainerReference(StripesKey);
            await stripes.CreateIfNotExistsAsync();
        }
    }
}
