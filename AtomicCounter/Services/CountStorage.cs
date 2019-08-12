using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class CountStorage : AppStorage
    {
        private readonly string Counter;
        private readonly ILogger logger;
        private const string CountPartition = "counts";

        public CountStorage(string counter, ILogger logger)
        {
            Counter = counter.ToCanonicalName();
            this.logger = logger;
        }

        public async Task SendIncrementEventAsync(long count = 1, decimal value = 0)
        {
            var queueClient = AppStorage.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(AppStorage.CountQueueName);

            var message = new CloudQueueMessage(new IncrementEvent()
            {
                Counter = Counter,
                Count = count,
                Value = value
            }.ToJson());

            await queue.AddMessageAsync(message);
        }

        public static string Tableize(string input)
        {
            return $"count{input.ToCanonicalName()}";
        }

        internal CloudTable GetCounterTable()
        {
            var tableName = Tableize(Counter);
            try
            {
                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var tableClient = storageAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(tableName);
                return table;
            }
            catch
            {
                logger.LogError($"Could not get table {tableName}");
                throw;
            }
        }

        public async Task IncrementAsync(Guid id, long count = 1, decimal value = 0)
        {
            var table = GetCounterTable();

            var insert = TableOperation.InsertOrReplace(new CountEntity()
            {
                PartitionKey = CountPartition,
                RowKey = id.ToString(),
                Count = count,
                Value = value
            });

            await table.ExecuteAsync(insert);
        }

        public async Task<long> CountAsync(Func<CountEntity, bool> conditions = null)
        {
            var table = GetCounterTable();

            if (table == null)
            {
                return 0;
            }

            var query = new TableQuery<CountEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, CountPartition));

            // Print the fields for each customer.
            long sum = 0;
            TableContinuationToken token = null;
            do
            {
                var resultSegment = await table.ExecuteQuerySegmentedAsync(query, token);
                token = resultSegment.ContinuationToken;

                if (conditions != null)
                {
                    sum += resultSegment.Results.Where(conditions).Sum(x => x.Count);
                }
                else
                {
                    sum += resultSegment.Results.Sum(x => x.Count);
                }
            } while (token != null);

            return sum;
        }

        public async Task<long> CountAsync(DateTimeOffset min, DateTimeOffset max)
        {
            return await CountAsync(x => DateInRange(x.Timestamp, min, max));
        }

        private static bool DateInRange(DateTimeOffset? timestamp, DateTimeOffset min, DateTimeOffset max)
        {
            if (timestamp == null) return false;

            return timestamp >= min && timestamp < max;
        }

        public async Task<IEnumerable<ChargeGroup>> GetInvoiceDataAsync(DateTimeOffset min, DateTimeOffset max)
        {
            try
            {
                var table = GetCounterTable();

                if (table == null)
                {
                    return new List<ChargeGroup>();
                }

                var meta = await AppStorage.GetCounterMetadataAsync(Counter);

                var query = new TableQuery<CountEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, CountPartition));

                var counters = new Dictionary<string, Dictionary<decimal, long>>();

                TableContinuationToken token = null;

                var groups = new Dictionary<decimal, long>();

                do
                {
                    var resultSegment = await table.ExecuteQuerySegmentedAsync(query, token);
                    var filtered = resultSegment.Where(r => DateInRange(r.Timestamp, min, max));

                    foreach (var item in filtered)
                    {
                        groups[item.Value] = groups.ContainsKey(item.Value) ? groups[item.Value] + item.Count : item.Count;
                    }

                    token = resultSegment.ContinuationToken;
                } while (token != null);

                return groups.Select(x => new ChargeGroup()
                {
                    Price = x.Key,
                    Quantity = x.Value
                });
            }
            catch
            {
                logger.LogWarning($"There was a problem counting {Counter} when getting invoice data.");
                throw;
            }
        }

        public static async Task<Counter> GetOrCreateCounterAsync(UserProfile profile, string counter, ILogger log)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var canonicalName = counter.ToCanonicalName();
            if (!CounterNameIsValid(canonicalName))
            {
                log.LogInformation($"Counter name '{counter}' was denied because its canonical form ('{canonicalName}') is invalid.");
                throw new InvalidOperationException();
            }

            var blob = GetCounterMetadataContainer();
            var block = blob.GetBlockBlobReference(canonicalName);

            if (await block.ExistsAsync())
            {
                var existing = (await block.DownloadTextAsync()).FromJson<Counter>();
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
                    await ProfilesStorage.SaveUserProfileAsync(profile);
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

        public override async Task CreateStorage()
        {
            var counters = Blobs.GetContainerReference(CountersKey);
            await counters.CreateIfNotExistsAsync();
        }
    }

    public class CountEntity : TableEntity
    {
        public long Count { get; set; }
        public decimal Value { get; set; }
    }
}
