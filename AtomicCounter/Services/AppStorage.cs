using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Stripe;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public static class AppStorage
    {
        public const string CountQueueName = "increment-items";
        public const string ResetEventsQueueName = "reset-events";
        public const string RecreateEventsQueueName = "recreate-events";
        public const string PriceChangeEventsQueueName = "price-change-events";
        public const string InvoicRequestEventsQueueName = "invoice-request-events";
        public const string ProfilesKey = "profiles";
        public const string StripesKey = "stripes";
        public const string CountersKey = "counters";

        private static readonly SHA256 Hasher = SHA256.Create();

        public static async Task DeleteCounterAsync(string counter, ILogger logger)
        {
            var counterClient = new CountStorage(counter, logger);
            var table = counterClient.GetCounterTable();
            await table.DeleteIfExistsAsync().ConfigureAwait(false);
        }

        public static async Task RecreateCounterAsync(string counter, ILogger logger)
        {
            var counterClient = new CountStorage(counter, logger);
            var table = counterClient.GetCounterTable();
            await table.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public static async Task SendDeleteEventAsync(string counter)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(ResetEventsQueueName);
            var message = new CloudQueueMessage(counter);
            await queue.AddMessageAsync(message).ConfigureAwait(false);
        }

        public static async Task SendRecreateEventAsync(string counter)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(RecreateEventsQueueName);
            var message = new CloudQueueMessage(counter);
            await queue.AddMessageAsync(message, null, TimeSpan.FromMinutes(1), null, null).ConfigureAwait(false);
        }

        public static async Task SendInvoiceRequestEventAsync(string counter, DateTimeOffset min, DateTimeOffset max)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(InvoicRequestEventsQueueName);

            var invoiceEvent = new InvoiceRequestEvent()
            {
                Counter = counter,
                Min = min,
                Max = max
            };

            var message = new CloudQueueMessage(invoiceEvent.ToJson());
            await queue.AddMessageAsync(message).ConfigureAwait(false);
        }

        public static async Task<int> RetryPoisonIncrementEventsAsync(ILogger log, CancellationToken token)
        {
            var queueClient = Storage.CreateCloudQueueClient();
            var poison = queueClient.GetQueueReference(CountQueueName + "-poison");
            var queue = queueClient.GetQueueReference(CountQueueName);

            log.LogInformation($"Transferring {poison.Name} to {queue.Name}.");

            var retval = 0;
            if (await poison.ExistsAsync().ConfigureAwait(false))
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

                var countSettingValue = !string.IsNullOrWhiteSpace(countSetting) ? int.Parse(countSetting, CultureInfo.InvariantCulture) : 32;

                log.LogInformation($"Grabbing maximum {countSettingValue} poinson items per batch.");
                while (canContinue())
                {
                    var messages = await poison.GetMessagesAsync(countSettingValue).ConfigureAwait(false);
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
                        await poison.DeleteMessageAsync(message).ConfigureAwait(false);
                        await queue.AddMessageAsync(message).ConfigureAwait(false);
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
                    var readKeys = new List<string> {
                        RandomString(),
                        counter.ReadKeys.First()
                    };
                    counter.ReadKeys.Clear();
                    readKeys.ForEach(x => counter.ReadKeys.Add(x));
                    keys = counter.ReadKeys.ToArray();
                    break;
                case KeyMode.Write:
                    var writeKeys = new List<string> {
                        RandomString(),
                        counter.WriteKeys.First()
                    };
                    counter.WriteKeys.Clear();
                    writeKeys.ForEach(x => counter.WriteKeys.Add(x));
                    keys = counter.WriteKeys.ToArray();
                    break;
                case KeyMode.Duplex:
                    throw new NotImplementedException();
                default:
                    return null;
            }

            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter.CounterName);

            await block.UploadTextAsync(counter.ToJson()).ConfigureAwait(false);

            return keys.Select(x => AuthorizationHelpers.CombineAndHash(counter.CounterName, x)).ToArray();
        }

        public static async Task<UserProfile> GetOrCreateUserProfileAsync(string sid, string token)
        {
            var tableClient = Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);

            var refEntity = new ProfileMappingEntity()
            {
                Sid = sid,
                Token = token
            };

            var op = TableOperation.Retrieve<ProfileMappingEntity>(refEntity.PartitionKey, refEntity.RowKey);
            var queryResult = await table.ExecuteAsync(op).ConfigureAwait(false);
            var resEntity = (ProfileMappingEntity)queryResult?.Result;

            if (resEntity == null)
            {
                // Create profile
                var profile = new UserProfile();

                await SaveUserProfileAsync(profile).ConfigureAwait(false);
                refEntity.ProfileId = profile.Id;

                await table.ExecuteAsync(TableOperation.Insert(refEntity)).ConfigureAwait(false);

                return profile;
            }
            else
            {
                var blob = GetProfileContainer();
                var block = blob.GetBlockBlobReference(resEntity.ProfileId.ToString());
                return (await block.DownloadTextAsync().ConfigureAwait(false)).FromJson<UserProfile>();
            }
        }

        public static async Task SaveUserProfileAsync(UserProfile profile)
        {
            var blob = GetProfileContainer();
            var block = blob.GetBlockBlobReference(profile.Id.ToString());
            await block.UploadTextAsync(profile.ToJson()).ConfigureAwait(false);
        }

        public static async Task<OAuthToken> GetOrCreateStripeInfo(string code, Func<Task<OAuthToken>> tokenFactory)
        {
            var blob = GetStripeContainer();

            var hashed = Base64UrlEncoder.Encode(Hasher.ComputeHash(Encoding.UTF8.GetBytes(code)));
            var block = blob.GetBlockBlobReference(hashed);

            if (await block.ExistsAsync().ConfigureAwait(false))
            {
                var data = await block.DownloadTextAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<OAuthToken>(data);
            }
            else
            {
                var data = await tokenFactory().ConfigureAwait(false);
                await block.UploadTextAsync(JsonConvert.SerializeObject(data)).ConfigureAwait(false);
                return data;
            }
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

            if (await block.ExistsAsync().ConfigureAwait(false))
            {
                var existing = (await block.DownloadTextAsync().ConfigureAwait(false)).FromJson<Counter>();
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
                        block.UploadTextAsync(newCounter.ToJson()),
                        table.CreateIfNotExistsAsync(),
                    };

                    Task.WaitAll(tasks);

                    profile.Counters.Add(newCounter.CounterName);
                    await SaveUserProfileAsync(profile).ConfigureAwait(false);
                }
                catch
                {
                    log.LogError($"Could not create counter {counter}");
                    var tasks = new[] {
                        block.DeleteIfExistsAsync(),
                        table.DeleteIfExistsAsync(),
                    };

                    await Task.Run(() => Task.WaitAll(tasks)).ConfigureAwait(false);

                    throw;
                }

                return newCounter;
            }
        }

        public static async Task SaveInvoiceAsync(string counter, DateTimeOffset min, DateTimeOffset max, IEnumerable<ChargeGroup> invoice)
        {
            var blobClient = Storage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference($"{counter}");
            var ranges = Uri.EscapeDataString(min.ToString("o", CultureInfo.InvariantCulture)) + "-" + Uri.EscapeDataString(max.ToString("o", CultureInfo.InvariantCulture)) + ".json";
            var blob = container.GetBlockBlobReference(ranges);
            await blob.UploadTextAsync(invoice.ToJson()).ConfigureAwait(false);
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
            var existing = await GetCounterMetadataAsync(counter).ConfigureAwait(false);

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

            if (await block.ExistsAsync().ConfigureAwait(false))
            {
                return (await block.DownloadTextAsync().ConfigureAwait(false)).FromJson<Counter>();
            }

            return null;
        }

        public static async Task SaveCounterMetadataAsync(Counter counter)
        {
            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter.CounterName);
            await block.UploadTextAsync(counter.ToJson()).ConfigureAwait(false);
        }

        private static CloudBlobContainer GetProfileContainer()
        {
            var blobClient = Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(ProfilesKey);
        }

        private static CloudBlobContainer GetStripeContainer()
        {
            var blobClient = Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(StripesKey);
        }

        public static CloudBlobContainer GetCounterMetadataContainer()
        {
            var blobClient = Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(CountersKey);
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

            var priceChangeQueue = queueClient.GetQueueReference(PriceChangeEventsQueueName);
            tasks.Add(priceChangeQueue.CreateIfNotExistsAsync());

            var tableClient = Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);
            tasks.Add(table.CreateIfNotExistsAsync());

            var blobClient = Storage.CreateCloudBlobClient();
            var counters = blobClient.GetContainerReference(CountersKey);
            var profiles = blobClient.GetContainerReference(ProfilesKey);
            var stripes = blobClient.GetContainerReference(StripesKey);
            tasks.Add(counters.CreateIfNotExistsAsync());
            tasks.Add(profiles.CreateIfNotExistsAsync());
            tasks.Add(stripes.CreateIfNotExistsAsync());

            Task.WaitAll(tasks.ToArray());
        }

        public static readonly CloudStorageAccount Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
    }
}
