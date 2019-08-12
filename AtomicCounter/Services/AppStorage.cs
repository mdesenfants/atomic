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
    public abstract class AppStorage
    {
        public const string CountQueueName = "increment-items";
        public const string ResetEventsQueueName = "reset-events";
        public const string RecreateEventsQueueName = "recreate-events";
        public const string InvoicRequestEventsQueueName = "invoice-request-events";
        public const string StripesKey = "stripes";
        public const string CountersKey = "counters";

        private static CloudBlobClient blobClient;
        private static CloudQueueClient queueClient;
        private static CloudTableClient tableClient;

        protected static CloudBlobClient Blobs
        {
            get
            {
                if (blobClient == null)
                {
                    blobClient = Storage.CreateCloudBlobClient();
                }

                return blobClient;
            }
        }

        protected static CloudQueueClient Queues
        {
            get
            {
                if (queueClient == null)
                {
                    queueClient = Storage.CreateCloudQueueClient();
                }

                return queueClient;
            }
        }

        protected static CloudTableClient Tables
        {
            get
            {
                if (tableClient == null)
                {
                    tableClient = Storage.CreateCloudTableClient();
                }

                return tableClient;
            }
        }

        private static readonly SHA256 Hasher = SHA256.Create();

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
            var queue = Queues.GetQueueReference(ResetEventsQueueName);
            var message = new CloudQueueMessage(counter);
            await queue.AddMessageAsync(message);
        }

        public static async Task SendRecreateEventAsync(string counter)
        {
            var queue = Queues.GetQueueReference(RecreateEventsQueueName);
            var message = new CloudQueueMessage(counter);
            await queue.AddMessageAsync(message, null, TimeSpan.FromMinutes(1), null, null);
        }

        public static async Task SendInvoiceRequestEventAsync(string counter, DateTimeOffset min, DateTimeOffset max)
        {
            var queue = Queues.GetQueueReference(InvoicRequestEventsQueueName);

            var invoiceEvent = new InvoiceRequestEvent()
            {
                Counter = counter,
                Min = min,
                Max = max
            };

            var message = new CloudQueueMessage(invoiceEvent.ToJson());
            await queue.AddMessageAsync(message);
        }

        public static async Task<int> RetryPoisonIncrementEventsAsync(ILogger log, CancellationToken token)
        {
            
            var poison = Queues.GetQueueReference(CountQueueName + "-poison");
            var queue = Queues.GetQueueReference(CountQueueName);

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

                var countSettingValue = !string.IsNullOrWhiteSpace(countSetting) ? int.Parse(countSetting, CultureInfo.InvariantCulture) : 32;

                log.LogInformation($"Grabbing maximum {countSettingValue} poinson items per batch.");
                while (canContinue())
                {
                    var messages = await poison.GetMessagesAsync(countSettingValue);
                    if (!messages.Any())
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
            name = name.ToCanonicalName();

            return name.Length <= 58 &&
                name.Length > 3 &&
                char.IsLetter(name[0]) &&
                name.All(char.IsLetterOrDigit);
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
            var block = blob.GetBlockBlobReference(counter.CanonicalName);

            await block.UploadTextAsync(counter.ToJson());

            return keys.Select(x => AuthorizationHelpers.CombineAndHash(counter.CanonicalName, x)).ToArray();
        }

        public static async Task<OAuthToken> GetOrCreateStripeInfo(string code, Func<Task<OAuthToken>> tokenFactory)
        {
            if (tokenFactory == null)
            {
                throw new ArgumentNullException(nameof(tokenFactory));
            }

            var blob = GetStripeContainer();

            var hashed = Base64UrlEncoder.Encode(Hasher.ComputeHash(Encoding.UTF8.GetBytes(code)));
            var block = blob.GetBlockBlobReference(hashed);

            if (await block.ExistsAsync())
            {
                var data = await block.DownloadTextAsync();
                return JsonConvert.DeserializeObject<OAuthToken>(data);
            }
            else
            {
                var data = await tokenFactory();
                await block.UploadTextAsync(JsonConvert.SerializeObject(data));
                return data;
            }
        }

        public static async Task SaveInvoiceAsync(string counter, DateTimeOffset min, DateTimeOffset max, IEnumerable<ChargeGroup> invoice)
        {
            var blobClient = Storage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference($"{counter}");
            var ranges = Uri.EscapeDataString(min.ToString("o", CultureInfo.InvariantCulture)) + "-" + Uri.EscapeDataString(max.ToString("o", CultureInfo.InvariantCulture)) + ".json";
            var blob = container.GetBlockBlobReference(ranges);
            await blob.UploadTextAsync(invoice.ToJson());
        }

        protected static string RandomString()
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
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var existing = await GetCounterMetadataAsync(counter.ToCanonicalName());

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
            var block = blob.GetBlockBlobReference(counter.ToCanonicalName());

            if (await block.ExistsAsync())
            {
                return (await block.DownloadTextAsync()).FromJson<Counter>();
            }

            return null;
        }

        public static async Task SaveCounterMetadataAsync(Counter counter)
        {
            if (counter == null)
            {
                throw new ArgumentNullException(nameof(counter));
            }

            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(counter.CanonicalName);
            await block.UploadTextAsync(counter.ToJson());
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

        public abstract Task CreateStorage();

        public static readonly CloudStorageAccount Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
    }
}
