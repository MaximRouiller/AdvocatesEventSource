using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.IO;
using System.Threading.Tasks;

namespace AdvocatesEventSource.Infrastructure
{
    public class AzureStorageHelper
    {
        private readonly string defaultContainerName;
        private readonly string connectionString;

        public AzureStorageHelper(string connectionString, string defaultContainerName = "advocates-events")
        {
            this.connectionString = connectionString;
            this.defaultContainerName = defaultContainerName;
        }
        public async Task<string> ReadFileContent(string filename)
        {
            var containerClient = new BlobContainerClient(connectionString, defaultContainerName);
            if (!await containerClient.ExistsAsync())
            {
                await containerClient.CreateIfNotExistsAsync();
            }
            BlobClient blob = containerClient.GetBlobClient(filename);

            Response<BlobDownloadInfo> result = await blob.DownloadAsync();

            using (var sr = new StreamReader(result.Value.Content))
            {
                return sr.ReadToEnd();
            }
        }

        public async Task SaveFileToBlobStorage(string filename, string content, string mimeType)
        {
            var containerClient = new BlobContainerClient(connectionString, defaultContainerName);
            if (!await containerClient.ExistsAsync())
            {
                await containerClient.CreateIfNotExistsAsync();
            }
            BlobClient blob = containerClient.GetBlobClient(filename);

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
                await writer.FlushAsync();

                stream.Position = 0;
                await blob.UploadAsync(stream, overwrite: true);
            }
            await blob.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = mimeType });
        }
    }
}
