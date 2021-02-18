using AdvocatesEventSource.Model;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdvocatesEventSource.CliCommands
{
    public class GenerateAdvocatesForDashboard
    {
        private string _connectionString;

        public GenerateAdvocatesForDashboard(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        public async Task ExecuteAsync()
        {
            var events = (await GetAllEvents())
                .OrderBy(x => x.EventDate)
                .ToList();

            List<DashboardAdvocate> advocates = new List<DashboardAdvocate>();

            foreach (var @event in events)
            {
                var existingAdvocate = advocates.FirstOrDefault(x => x.UID == @event.UID || x.FileName == @event.FileName);

                if (@event is AdvocateAdded)
                {
                    var addedAdvocate = @event as AdvocateAdded;
                    advocates.Add(new DashboardAdvocate
                    {
                        UID = addedAdvocate.UID,
                        FileName = addedAdvocate.FileName,
                        GitHubUserName = addedAdvocate.GitHubUserName,
                        Team = addedAdvocate.Team,
                        Alias = addedAdvocate.Alias,
                    });
                }
                if (@event is AdvocateModified)
                {
                    if (existingAdvocate == null)
                    {
                        existingAdvocate = new DashboardAdvocate();
                        advocates.Add(existingAdvocate);
                        Console.WriteLine($"Modified event without Added for Advocate: '{@event.UID}' ");
                    }
                    var modifiedAdvocate = @event as AdvocateModified;

                    existingAdvocate.FileName = modifiedAdvocate.FileName;
                    existingAdvocate.GitHubUserName = modifiedAdvocate.NewGitHubUserName;
                    existingAdvocate.Team = modifiedAdvocate.NewTeam;
                    existingAdvocate.Alias = modifiedAdvocate.NewAlias;
                }
                //if (@event is AdvocateRemoved)
                //{
                //    if (existingAdvocate == null) { Console.WriteLine($"Can't remove existing advocate {@event.UID} "); continue; }
                //    advocates.Remove(existingAdvocate);
                //}
            }

            Console.WriteLine($"Advocate count: {advocates.Count}");

            await SaveAdvocatesAsync(advocates);
        }

        private async Task SaveAdvocatesAsync(List<DashboardAdvocate> advocates)
        {
            var json = JsonSerializer.Serialize(advocates);

            var containerClient = new BlobContainerClient(_connectionString, "advocates-events");
            await containerClient.CreateIfNotExistsAsync();
            BlobClient blob = containerClient.GetBlobClient("dashboard-advocates.json");

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
