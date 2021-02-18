using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdvocatesEventSource.Model
{
    public class GenerateCurrentState
    {
        private string _connectionString;

        public GenerateCurrentState(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task ExecuteAsync()
        {
            var events = (await GetAllEvents())
                .OrderBy(x => x.EventDate)                
                .ToList();

            List<Advocate> advocates = new List<Advocate>();

            foreach (var @event in events)
            {
                var existingAdvocate = advocates.FirstOrDefault(x => x.UID == @event.UID || x.FileName == @event.FileName);

                if (@event is AdvocateAdded)
                {
                    var addedAdvocate = @event as AdvocateAdded;
                    advocates.Add(new Advocate
                    {
                        UID = addedAdvocate.UID,
                        FileName = addedAdvocate.FileName,
                        GitHubUserName = addedAdvocate.GitHubUserName,
                        Name = addedAdvocate.Name,
                        Team = addedAdvocate.Team,
                        TwitterHandle = addedAdvocate.TwitterHandle,
                        Alias = addedAdvocate.Alias,
                    });
                }
                if (@event is AdvocateModified)
                {
                    if (existingAdvocate == null)
                    {
                        existingAdvocate = new Advocate();
                        advocates.Add(existingAdvocate);
                        Console.WriteLine($"Modified event without Added for Advocate: '{@event.UID}' ");
                    }
                    var modifiedAdvocate = @event as AdvocateModified;

                    existingAdvocate.FileName = modifiedAdvocate.FileName;
                    existingAdvocate.GitHubUserName = modifiedAdvocate.NewGitHubUserName;
                    existingAdvocate.Name = modifiedAdvocate.NewName;
                    existingAdvocate.Team = modifiedAdvocate.NewTeam;
                    existingAdvocate.TwitterHandle = modifiedAdvocate.NewTwitterHandle;
                    existingAdvocate.Alias = modifiedAdvocate.NewAlias;
                }
                if (@event is AdvocateRemoved)
                {
                    if (existingAdvocate == null) { Console.WriteLine($"Can't remove existing advocate {@event.UID} "); continue; }
                    advocates.Remove(existingAdvocate);
                }
            }

            Console.WriteLine($"Advocate count: {advocates.Count}");

            await SaveCurrentStateAsync(advocates);
        }

        private async Task SaveCurrentStateAsync(List<Advocate> advocates)
        {
            var json = JsonSerializer.Serialize(advocates);

            var containerClient = new BlobContainerClient(_connectionString, "advocates-events");
            await containerClient.CreateIfNotExistsAsync();
            BlobClient blob = containerClient.GetBlobClient("current-advocates.json");

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                await writer.FlushAsync();

                stream.Position = 0;
                await blob.UploadAsync(stream, overwrite: true);
            }
            await blob.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "application/json" });
        }

        private async Task<List<AdvocateEvent>> GetAllEvents()
        {
            var containerClient = new BlobContainerClient(_connectionString, "advocates-events");
            await containerClient.CreateIfNotExistsAsync();
            BlobClient blob = containerClient.GetBlobClient("all-events.json");

            Response<BlobDownloadInfo> result = await blob.DownloadAsync();

            using (var sr = new StreamReader(result.Value.Content))
            {
                string json = sr.ReadToEnd();

                var options = new JsonSerializerOptions { Converters = { new AdvocateEventsConverter() } };
                return JsonSerializer.Deserialize<List<AdvocateEvent>>(json, options);
            }
        }
    }
}