using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace AtomicCounter.Models
{
    public class ProfileMappingEntity : TableEntity
    {
        public string Sid
        {
            get => $"{RowKey}|{PartitionKey}";
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
    }
}
