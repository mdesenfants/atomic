using AtomicCounter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
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
        public const string ResetEventsQueueName = "reset-events";
        public const string RecreateEventsQueueName = "recreate-events";
        public const string ProfilesKey = "profiles";
        public const string CountersKey = "counters";

        public static async Task DeleteCounterAsync(string counter, ILogger logger)
        {
            var counterClient = new CountStorage(counter, logger);
            var table = counterClient.GetCounterTable();
            await table.DeleteIfExistsAsync();
        }

        public static async Task RecreateCounterAsync(string counter, ILogger logger)
        {
            var counterClient = new CountStorage(counter, logger);
            var table = counterClient.GetCounterTable();
            await table.CreateIfNotExistsAsync();
        }

        public static async Task SendDeleteEventAsync(string counter)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(ResetEventsQueueName);
            var message = new CloudQueueMessage(counter);
            await queue.AddMessageAsync(message);
        }

        public static async Task SendRecreateEventAsync(string counter)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(RecreateEventsQueueName);
            var message = new CloudQueueMessage(counter);
            await queue.AddMessageAsync(message, null, TimeSpan.FromMinutes(1), null, null);
        }

        public static async Task<int> RetryPoisonIncrementEventsAsync(CancellationToken token, ILogger log)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var poison = queueClient.GetQueueReference(CountQueueName + "-poison");
            var queue = queueClient.GetQueueReference(CountQueueName);

            log.LogInformation($"Transferring {poison.Name} to {queue.Name}.");

            var retval = 0;
            if (await poison.ExistsAsync())
            {
                log.LogInformation("Found poison queue.");
                var countSetting = Environment.GetEnvironmentVariable("ResetCount");

                bool canContinue()
                {
                    if (token.IsCancellationRequested)
                    {
                        log.LogInformation("Cancellation requested.");
                        return false;
                    }

                    return true;
                }

                var countSettingValue = !string.IsNullOrWhiteSpace(countSetting) ? int.Parse(countSetting) : 32;

                log.LogInformation($"Grabbing maximum {countSettingValue} poinson items per batch.");
                while (canContinue())
                {
                    var messages = await poison.GetMessagesAsync(countSettingValue);
                    if (messages.Count() == 0)
                    {
                        break;
                    }

                    foreach (var message in messages)
                    {
                        if (message == null)
                        {
                            continue;
                        }

                        retval++;
                        await poison.DeleteMessageAsync(message);
                        await queue.AddMessageAsync(message);
                    }
                }
            }

            return retval;
        }

        public static bool CounterNameIsValid(string name)
        {
            return name.Length <= 58 &&
                name.Length > 3 &&
                name.All(c => char.IsDigit(c) || char.IsLower(c));
        }

        internal static async Task<string[]> RotateKeysAsync(Counter counter, KeyMode mode)
        {
            if (counter == null)
            {
                throw new InvalidOperationException($"Could not find counter {counter.CounterName}.");
            }

            string[] keys = null;
            switch (mode)
            {
                case KeyMode.Read:
                    counter.ReadKeys = new List<string> {
                        RandomString(),
                        counter.ReadKeys.First()
                    };
                    keys = counter.ReadKeys.ToArray();
                    break;
                case KeyMode.Write:
                    counter.WriteKeys = new List<string> {
                        RandomString(),
                        counter.WriteKeys.First()
                    };
                    keys = counter.WriteKeys.ToArray();
                    break;
                case KeyMode.Duplex:
                    throw new NotImplementedException();
                default:
                    return null;
            }

            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter.CounterName);

            await block.UploadTextAsync(JsonConvert.SerializeObject(counter));

            return keys.Select(x => AuthorizationHelpers.CombineAndHash(counter.CounterName, x)).ToArray();
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

        public static async Task<Counter> GetOrCreateCounterAsync(UserProfile profile, string counter, ILogger log)
        {
            if (!CounterNameIsValid(counter))
            {
                log.LogInformation($"Counter named '{counter}' was denied because it was invalid.");
                throw new InvalidOperationException();
            }

            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter);

            if (await block.ExistsAsync())
            {
                var existing = JsonConvert.DeserializeObject<Counter>(await block.DownloadTextAsync());
                return existing.Profiles.Contains(profile.Id) ? existing : null;
            }
            else
            {
                var newCounter = new Counter() { CounterName = counter };
                newCounter.Profiles.Add(profile.Id);

                for (var i = 0; i < 2; i++)
                {
                    newCounter.ReadKeys.Add(RandomString());
                }

                for (var i = 0; i < 2; i++)
                {
                    newCounter.WriteKeys.Add(RandomString());
                }

                var client = new CountStorage(counter, log);
                var table = client.GetCounterTable();
                try
                {
                    var tasks = new[] {
                        block.UploadTextAsync(JsonConvert.SerializeObject(newCounter)),
                        table.CreateIfNotExistsAsync(),
                    };

                    await Task.Run(() => Task.WaitAll(tasks));

                    profile.Counters.Add(newCounter.CounterName);
                    await SaveUserProfileAsync(profile);
                }
                catch
                {
                    log.LogError($"Could not create counter {counter}");
                    var tasks = new[] {
                        block.DeleteIfExistsAsync(),
                        table.DeleteIfExistsAsync(),
                    };

                    await Task.Run(() => Task.WaitAll(tasks));

                    throw;
                }

                return newCounter;
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

        public static async Task<Counter> GetCounterMetadataAsync(UserProfile profile, string counter)
        {
            var existing = await GetCounterMetadataAsync(counter);

            if (existing == null)
            {
                return null;
            }
            else if (!(existing?.Profiles?.Contains(profile.Id) ?? false))
            {
                throw new UnauthorizedAccessException($"The specified profile does not have access to counter {counter}.");
            }

            return existing?.Profiles?.Contains(profile.Id) ?? false ? existing : null;
        }

        public static async Task<Counter> GetCounterMetadataAsync(string counter)
        {
            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter);

            if (await block.ExistsAsync())
            {
                return JsonConvert.DeserializeObject<Counter>(await block.DownloadTextAsync());
            }

            return null;
        }

        private static CloudBlobContainer GetProfileContainer()
        {
            var blobClient = Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(ProfilesKey);
        }

        private static CloudBlobContainer GetCounterMetadataContainer()
        {
            var blobClient = Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(CountersKey);
        }

        static AppStorage()
        {
            Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        }

        public static void CreateAppStorage()
        {
            var tasks = new List<Task>();

            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(CountQueueName);
            tasks.Add(queue.CreateIfNotExistsAsync());

            var resetQueue = queueClient.GetQueueReference(ResetEventsQueueName);
            tasks.Add(resetQueue.CreateIfNotExistsAsync());

            var createQueue = queueClient.GetQueueReference(RecreateEventsQueueName);
            tasks.Add(createQueue.CreateIfNotExistsAsync());

            var tableClient = Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);
            tasks.Add(table.CreateIfNotExistsAsync());

            var blobClient = Storage.CreateCloudBlobClient();
            var blob = blobClient.GetContainerReference(CountersKey);
            tasks.Add(blob.CreateIfNotExistsAsync());

            Task.WaitAll(tasks.ToArray());
        }

        public static readonly CloudStorageAccount Storage;
    }
}
