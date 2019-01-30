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
    public class CountStorage
    {
        private readonly string Counter;
        private readonly ILogger logger;
        private const string CountPartition = "counts";

        public CountStorage(string counter, ILogger logger)
        {
            Counter = counter;
            this.logger = logger;
        }

        public async Task SendIncrementEventAsync(long count = 1, string client = null)
        {
            var queueClient = AppStorage.Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(AppStorage.CountQueueName);

            var message = new CloudQueueMessage(new IncrementEvent()
            {
                Counter = Counter,
                Count = count,
                Client = client
            }.ToString());

            await queue.AddMessageAsync(message);
        }

        public static string Sanitize(string input)
        {
            return new string(input.Where(char.IsLetterOrDigit).ToArray());
        }

        public static string Tableize(string input)
        {
            return $"count{Sanitize(input)}";
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

        public async Task IncrementAsync(Guid id, string client, long count = 1)
        {
            var table = GetCounterTable();

            var insert = TableOperation.InsertOrReplace(new CountEntity()
            {
                PartitionKey = CountPartition,
                RowKey = id.ToString(),
                Count = count,
                Client = client
            });

            await table.ExecuteAsync(insert);
        }

        public async Task<long> CountAsync(Func<CountEntity, bool> conditions = null)
        {
            try
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
            catch
            {
                logger.LogWarning($"There was a problem counting {Counter}. Defaulting to 0.");
                return 0;
            }
        }

        public async Task<long> CountAsync(string client)
        {
            return await CountAsync(x => client.Equals(x.Client, StringComparison.OrdinalIgnoreCase));
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

        public async Task<long> CountAsync(string client, DateTimeOffset min, DateTimeOffset max)
        {
            return await CountAsync(x =>
                client.Equals(x.Client, StringComparison.OrdinalIgnoreCase) &&
                DateInRange(x.Timestamp, min, max));
        }

        public async Task<Dictionary<string, IEnumerable<ChargeGroup>>> GetInvoiceDataAsync(DateTimeOffset min, DateTimeOffset max)
        {
            try
            {
                var table = GetCounterTable();

                if (table == null)
                {
                    return new Dictionary<string, IEnumerable<ChargeGroup>>();
                }

                var meta = await AppStorage.GetCounterMetadataAsync(Counter);

                // Create a sorted lookup so we classify records to their bucket quickly
                var lookup = new SortedSet<DateTimeOffset>(
                    meta
                        .PriceChanges.Where(k => DateInRange(k.Effective, min, max)).Select(k => k.Effective.Value))
                    .Reverse();

                // Create price lookup
                var prices = meta.PriceChanges.ToDictionary(x => x.Effective, y => y.Amount);

                // Make buckets by price change effective date, starting with 0
                Dictionary<DateTimeOffset, long> getBuckets() => meta.PriceChanges.ToDictionary(x => x.Effective.Value, y => 0L);

                var query = new TableQuery<CountEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, CountPartition));

                var clients = new Dictionary<string, Dictionary<DateTimeOffset, long>>();

                TableContinuationToken token = null;
                do
                {
                    var resultSegment = await table.ExecuteQuerySegmentedAsync(query, token);
                    var filtered = resultSegment.Where(r => DateInRange(r.Timestamp, min, max) && !string.IsNullOrEmpty(r.Client));
                    foreach (var result in filtered)
                    {
                        if (!clients.TryGetValue(result.Client, out var changes))
                        {
                            changes = getBuckets();
                            clients[result.Client] = changes;
                        }

                        // Add count to bucket based on the first price change that is before the record timestamp
                        var bucket = lookup.FirstOrDefault(pc => pc <= result.Timestamp);
                        if (bucket > DateTimeOffset.MinValue)
                        {
                            changes[bucket] += result.Count;
                        }
                    }

                    token = resultSegment.ContinuationToken;
                } while (token != null);

                return clients.ToDictionary(c => c.Key, v => v.Value.Select(x => new ChargeGroup()
                {
                    Effective = x.Key,
                    Price = prices[x.Key],
                    Quantity = x.Value
                }).Where(y => y.Quantity != 0));
            }
            catch
            {
                logger.LogWarning($"There was a problem counting {Counter}. Defaulting to 0.");
                throw;
            }
        }
    }

    public class CountEntity : TableEntity
    {
        public long Count { get; set; }
        public string Client { get; set; }
    }
}
