using AtomicCounter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class AppStorage
    {
        public const string CountQueueName = "increment-items";
        private const string ProfilesKey = "profiles";
        private const string TenantsKey = "tenants";

        public static async Task ResetAsync(UserProfile profile, string tenant, string app, string counter, ILogger logger)
        {
            try
            {
                var info = GetTenantAsync(profile, tenant);

                if (info == null)
                {
                    throw new UnauthorizedAccessException();
                }

                var counterClient = new CounterStorage(tenant, app, counter, logger);
                var table = counterClient.GetCounterTable();
                await table.DeleteAsync();
                await table.CreateAsync();
            }
            catch
            {
                logger.LogWarning($"Cannot reset counter {tenant}/{app}/{counter}.");
            }
        }

        public static async Task<int> RetryPoisonIncrementEventsAsync(CancellationToken token)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var poison = queueClient.GetQueueReference(CountQueueName + "-poison");
            var queue = queueClient.GetQueueReference(CountQueueName);

            var retval = 0;
            if (await poison.ExistsAsync())
            {
                var countSetting = Environment.GetEnvironmentVariable("ResetCount");

                async Task<bool> canContinue()
                {
                    if (token.IsCancellationRequested) return false;

                    await poison.FetchAttributesAsync();
                    return queue.ApproximateMessageCount.HasValue && queue.ApproximateMessageCount > 0;
                }

                var countSettingValue = !string.IsNullOrWhiteSpace(countSetting) ? int.Parse(countSetting) : 32;

                while (await canContinue())
                {
                    var maxBatchSize = Math.Min(queue.ApproximateMessageCount.Value, countSettingValue);

                    var messages = await poison.GetMessagesAsync(maxBatchSize);

                    foreach (var message in messages)
                    {
                        if (message == null) continue;

                        retval++;
                        await poison.DeleteMessageAsync(message);
                        await queue.AddMessageAsync(message);
                    }
                }
            }

            return retval;
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

            var blob = GetTenantContainer();
            var block = blob.GetBlockBlobReference(tenant);

            await block.UploadTextAsync(JsonConvert.SerializeObject(result));

            return keys.Select(x => AuthorizationHelpers.CombineAndHash(tenant, x)).ToArray();
        }

        public static async Task<UserProfile> GetOrCreateUserProfileAsync(string sid)
        {
            var tableClient = Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);

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
                var blob = GetProfileContainer();
                var block = blob.GetBlockBlobReference(resEntity.ProfileId.ToString());
                return JsonConvert.DeserializeObject<UserProfile>(await block.DownloadTextAsync());
            }
        }

        public static async Task SaveUserProfileAsync(UserProfile profile)
        {
            var blob = GetProfileContainer();
            var block = blob.GetBlockBlobReference(profile.Id.ToString());
            await block.UploadTextAsync(JsonConvert.SerializeObject(profile));
        }

        public static async Task<Tenant> GetOrCreateTenantAsync(UserProfile profile, string tenant)
        {
            var blob = GetTenantContainer();
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
            if (existing == null)
            {
                return null;
            }
            else if (!(existing?.Profiles?.Contains(profile.Id) ?? false))
            {
                throw new UnauthorizedAccessException($"The specified profile does not have access to tenant {tenant}.");
            }

            return existing?.Profiles?.Contains(profile.Id) ?? false ? existing : null;
        }

        public static async Task<Tenant> GetTenantAsync(string tenant)
        {
            var blob = GetTenantContainer();
            var block = blob.GetBlockBlobReference(tenant);

            if (await block.ExistsAsync())
            {
                return JsonConvert.DeserializeObject<Tenant>(await block.DownloadTextAsync());
            }

            return null;
        }

        private static CloudBlobContainer GetProfileContainer()
        {
            var blobClient = Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(ProfilesKey);
        }

        private static CloudBlobContainer GetTenantContainer()
        {
            var blobClient = Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(TenantsKey);
        }

        private static async Task UpdateTenantAsync(Tenant t)
        {
            var blob = GetTenantContainer();
            var block = blob.GetBlockBlobReference(t.TenantName);
            await block.UploadTextAsync(JsonConvert.SerializeObject(t));
        }

        public static async Task AddCounterToTenant(UserProfile user, string tenant, string app, string counter)
        {
            var ten = await GetTenantAsync(user, tenant);

            if (ten == null) throw new UnauthorizedAccessException();

            ten.Counters.Add(new Counter()
            {
                App = app,
                Name = counter
            });

            await UpdateTenantAsync(ten);
        }

        static AppStorage()
        {
            Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(CountQueueName);
            var countQueueTask = queue.CreateIfNotExistsAsync();

            var tableClient = Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);
            var profilesTask = table.CreateIfNotExistsAsync();


            var blobClient = Storage.CreateCloudBlobClient();
            var blob = blobClient.GetContainerReference(TenantsKey);
            var tenantsTask = blob.CreateIfNotExistsAsync();

            Task.WaitAll(new[] { countQueueTask, profilesTask, tenantsTask });
        }

        public static readonly CloudStorageAccount Storage;
    }
}
