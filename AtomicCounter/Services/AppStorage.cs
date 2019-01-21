﻿using AtomicCounter.Models;
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
        public const string ProfilesKey = "profiles";
        public const string CountersKey = "counters";

        public static async Task ResetAsync(UserProfile profile, string counter, ILogger logger)
        {
            try
            {
                var info = GetCounterMetadataAsync(profile, counter);

                var counterClient = new CountStorage(counter, logger);
                var table = counterClient.GetCounterTable();
                await table.DeleteAsync();
                await table.CreateAsync();
            }
            catch
            {
                logger.LogWarning($"Cannot reset counter {counter}.");
                throw;
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
                    if (token.IsCancellationRequested)
                    {
                        return false;
                    }

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

        internal static async Task<string[]> RotateKeysAsync(UserProfile user, string counter, KeyMode mode)
        {
            var result = await GetCounterMetadataAsync(user, counter);

            if (result == null)
            {
                throw new InvalidOperationException($"Could not find counter {counter}.");
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

            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter);

            await block.UploadTextAsync(JsonConvert.SerializeObject(result));

            return keys.Select(x => AuthorizationHelpers.CombineAndHash(counter, x)).ToArray();
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
                var locks = client.GetCounterLockQueue();
                try
                {
                    var tasks = new[] {
                        block.UploadTextAsync(JsonConvert.SerializeObject(newCounter)),
                        table.CreateIfNotExistsAsync(),
                        locks.CreateIfNotExistsAsync()
                    };

                    await Task.Run(() => Task.WaitAll(tasks));
                }
                catch
                {
                    log.LogError($"Could not create counter {counter}");
                    var tasks = new[] {
                        block.DeleteIfExistsAsync(),
                        table.DeleteIfExistsAsync(),
                        locks.DeleteIfExistsAsync()
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

        private static async Task UpdateCounterMetadataAsync(Counter counter)
        {
            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter.CounterName);
            await block.UploadTextAsync(JsonConvert.SerializeObject(counter));
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
            var blob = blobClient.GetContainerReference(CountersKey);
            var countersTask = blob.CreateIfNotExistsAsync();

            Task.WaitAll(new[] { countQueueTask, profilesTask, countersTask });
        }

        public static readonly CloudStorageAccount Storage;
    }
}
