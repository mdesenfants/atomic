using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace AtomicCounter.Services
{
    public static class AppStorage
    {
        public const string CountQueueName = "increment-items";
        private const string ProfilesKey = "profiles";
        private const string TenantsKey = "tenants";

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

        public static async Task ResetIncrementEventsAsync()
        {
            var queueClient = storage.CreateCloudQueueClient();
            var poison = queueClient.GetQueueReference(CountQueueName + "-poison");
            var queue = queueClient.GetQueueReference(CountQueueName);

            if (await poison.ExistsAsync())
            {
                var countSetting = Environment.GetEnvironmentVariable("ResetCount");
                var messages = await poison.GetMessagesAsync(!string.IsNullOrWhiteSpace(countSetting) ? int.Parse(countSetting) : 32);

                foreach (var message in messages)
                {
                    if (message == null) continue;

                    await poison.DeleteMessageAsync(message);
                    await queue.AddMessageAsync(message);
                }
            }
        }

        internal static async Task<string[]> RotateKeysAsync(UserProfile user, string tenant, KeyMode mode)
        {
            var result = await GetTenantAsync(user, tenant);

            if (result == null)
            {
                throw new InvalidOperationException($"Could not find tenant {tenant}.");
            }

            string[] keys = null;
            switch (mode)
            {
                case KeyMode.Read:
                    result.ReadKeys = new List<string> {
                        RandomString(),
                        result.ReadKeys.First()
                    };
                    keys = result.ReadKeys.ToArray();
                    break;
                case KeyMode.Write:
                    result.WriteKeys = new List<string> {
                        RandomString(),
                        result.WriteKeys.First()
                    };
                    keys = result.WriteKeys.ToArray();
                    break;
                case KeyMode.Duplex:
                    throw new NotImplementedException();
                default:
                    return null;
            }

            var blob = await GetTenantContainerAsync();
            var block = blob.GetBlockBlobReference(tenant);

            await block.UploadTextAsync(JsonConvert.SerializeObject(result));

            return keys.Select(x => AuthorizationHelpers.CombineAndHash(tenant, x)).ToArray();
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
            await block.UploadTextAsync(JsonConvert.SerializeObject(profile));
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
            const int strlen = 256;
            var builder = new StringBuilder(strlen);

            using (var random = new RNGCryptoServiceProvider())
            {
                for (var i = 0; i < 256; i++)
                {
                    var byteArray = new byte[4];
                    random.GetBytes(byteArray);
                    var randomInt = BitConverter.ToUInt32(byteArray, 0) % 26 + 'a';
                    builder.Append((char)randomInt);
                }
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
            storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        }

        private static readonly CloudStorageAccount storage;
    }
}
