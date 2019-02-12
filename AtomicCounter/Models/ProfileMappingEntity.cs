using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace AtomicCounter.Models
{
    public class ProfileMappingEntity : TableEntity
    {
        public string Sid
        {
            get => $"{PartitionKey}|{RowKey}";
            set
            {
                var pipe = value.IndexOf('|');
                var provider = value.Substring(0, pipe);
                var id = value.Substring(pipe + 1, value.Length - pipe - 1);

                // Stable id for provider acts as partition key
                PartitionKey = provider;

                // Grab the provider and set it to the row key
                RowKey = id;
            }
        }

        public Guid ProfileId { get; set; }

        public string Token { get; set; }
    }
}
