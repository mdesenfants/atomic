using AtomicCounter.Models;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class ProfilesStorage : AppStorage
    {
        public const string ProfilesKey = "profiles";

        public override async Task CreateStorage()
        {
            var tableClient = AppStorage.Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);
            await table.CreateIfNotExistsAsync();

            var profiles = Blobs.GetContainerReference(ProfilesKey);
            await profiles.CreateIfNotExistsAsync();
        }

        private static CloudBlobContainer GetProfileContainer()
        {
            var blobClient = AppStorage.Storage.CreateCloudBlobClient();
            return blobClient.GetContainerReference(ProfilesKey);
        }

        public static async Task SaveUserProfileAsync(UserProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var blob = GetProfileContainer();
            var block = blob.GetBlockBlobReference(profile.Id.ToString());
            await block.UploadTextAsync(profile.ToJson());
        }

        public static async Task<UserProfile> GetOrCreateUserProfileAsync(string sid, string token)
        {
            var tableClient = AppStorage.Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(ProfilesKey);

            var refEntity = new ProfileMappingEntity()
            {
                Sid = sid,
                Token = token
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
                return (await block.DownloadTextAsync()).FromJson<UserProfile>();
            }
        }
    }
}
