using AdvocatesEventSource.Data;
using AdvocatesEventSource.Data.Model;
using AdvocatesEventSource.Data.Parser;
using AdvocatesEventSource.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdvocatesEventSource.Model
{
    public class GenerateAllEvents
    {
        private readonly string gitPath;
        private readonly AzureStorageHelper storage;

        public GenerateAllEvents(string gitPath, string azureStorageAccountConnectionString)
        {
            this.gitPath = gitPath;
            storage = new AzureStorageHelper(azureStorageAccountConnectionString);
        }

        public async Task ExecuteAsync()
        {

            AdvocateEventGenerator generator = new AdvocateEventGenerator(new DirectoryInfo(gitPath));

            List<AdvocateEvent> events = generator.GenerateAllEvents();
            var lastCommitSha = generator.LastProcessedCommitSha;

            var options = new JsonSerializerOptions { Converters = { new AdvocateEventsConverter() } };
            var json = JsonSerializer.Serialize(events, options);
            await storage.SaveFileToBlobStorage("all-events.json", json, "application/json");
            await storage.SaveFileToBlobStorage("last-processed-commit.txt", lastCommitSha, "text/plain");

            Console.WriteLine("GenerateAllEvents completed.");
        }


    }
}
