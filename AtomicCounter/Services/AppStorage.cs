using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public static class AppStorage
    {
        public const string CountQueueName = "increment-items";
        private const string ProfilesKey = "profiles";
        private const string TenantsKey = "tenants";

        private static readonly Random Random = new Random();

        public static async Task SendIncrementEventAsync(string tenant, string app, string counter, long count = 1)
        {
            var queueClient = storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(CountQueueName);
            await queue.CreateIfNotExistsAsync();

            var message = new CloudQueueMessage(new IncrementEvent()
            {
                App = app,
                Tenant = tenant,
                Count = count,
                Counter = counter
            }.ToString());

            await queue.AddMessageAsync(message);
        }

        public static async Task<UserProfile> GetOrCreateUserProfileAsync(string sid)
        {
            var tableClient = storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);
            await table.CreateIfNotExistsAsync();

            var refEntity = new ProfileMappingEntity()
            {
                Sid = sid
            };

            var op = TableOperation.Retrieve<ProfileMappingEntity>(refEntity.PartitionKey, refEntity.RowKey);
            var queryResult = await table.ExecuteAsync(op);
            var resEntity = (ProfileMappingEntity)queryResult?.Result;

            if (resEntity == null)
            {
                // Create profile
                var profile = new UserProfile();

                await SaveUserProfileAsync(profile);
                refEntity.ProfileId = profile.Id;

                await table.ExecuteAsync(TableOperation.Insert(refEntity));

                return profile;
            }
            else
            {
                var blob = await GetProfileContainerAsync();
                var block = blob.GetBlockBlobReference(resEntity.ProfileId.ToString());
                return JsonConvert.DeserializeObject<UserProfile>(await block.DownloadTextAsync());
            }
        }

        public static async Task SaveUserProfileAsync(UserProfile profile)
        {
            var blob = await GetProfileContainerAsync();
            var block = blob.GetBlockBlobReference(profile.Id.ToString());
            block.UploadText(JsonConvert.SerializeObject(profile));
        }

        public static async Task<Tenant> GetOrCreateTenantAsync(UserProfile profile, string tenant)
        {
            var blob = await GetTenantContainerAsync();
            var block = blob.GetBlockBlobReference(tenant);

            if (await block.ExistsAsync())
            {
                var existing = JsonConvert.DeserializeObject<Tenant>(await block.DownloadTextAsync());

                return existing.Profiles.Contains(profile.Id) ? existing : null;
            }
            else
            {
                RandomString();

                var newTenant = new Tenant() { TenantName = tenant };
                newTenant.Profiles.Add(profile.Id);

                for (var i = 0; i < 2; i++)
                {
                    newTenant.ReadKeys.Add(RandomString());
                }

                for (var i = 0; i < 2; i++)
                {
                    newTenant.WriteKeys.Add(RandomString());
                }

                await block.UploadTextAsync(JsonConvert.SerializeObject(newTenant));

                return newTenant;
            }
        }

        private static string RandomString()
        {
            var builder = new StringBuilder(100);

            for (var i = 0; i < 256; i++)
            {
                builder.Append(Random.Next('a', 'z'));
            }

            return builder.ToString();
        }

        public static async Task<Tenant> GetTenantAsync(UserProfile profile, string tenant)
        {
            var existing = await GetTenantAsync(tenant);

            return existing?.Profiles?.Contains(profile.Id) ?? false ? existing : null;
        }

        public static async Task<Tenant> GetTenantAsync(string tenant)
        {
            var blob = await GetTenantContainerAsync();
            var block = blob.GetBlockBlobReference(tenant);

            if (await block.ExistsAsync())
            {
                return JsonConvert.DeserializeObject<Tenant>(await block.DownloadTextAsync());
            }

            return null;
        }

        private static async Task<CloudBlobContainer> GetProfileContainerAsync()
        {
            var blobClient = storage.CreateCloudBlobClient();
            var blob = blobClient.GetContainerReference(ProfilesKey);
            await blob.CreateIfNotExistsAsync();

            return blob;
        }

        private static async Task<CloudBlobContainer> GetTenantContainerAsync()
        {
            var blobClient = storage.CreateCloudBlobClient();
            var blob = blobClient.GetContainerReference(TenantsKey);
            await blob.CreateIfNotExistsAsync();

            return blob;
        }

        static AppStorage()
        {
            storage = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        }

        private static readonly CloudStorageAccount storage;
    }
}
