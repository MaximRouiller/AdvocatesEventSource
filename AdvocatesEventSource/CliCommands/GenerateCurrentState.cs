using AdvocatesEventSource.Data;
using AdvocatesEventSource.Data.Model;
using AdvocatesEventSource.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdvocatesEventSource.Model
{
    public class GenerateCurrentState
    {
        private readonly AzureStorageHelper storage;

        public GenerateCurrentState(string connectionString)
        {
            storage = new AzureStorageHelper(connectionString);
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
                    var modifiedAdvocate = @event as AdvocateModified;

                    // made to handle file renames
                    if (existingAdvocate == null)
                    {
                        existingAdvocate = advocates.FirstOrDefault(x => x.UID == modifiedAdvocate.NewUID || x.FileName == modifiedAdvocate.NewFileName);
                    }
                    if (existingAdvocate == null)
                    {
                        existingAdvocate = new Advocate();
                        advocates.Add(existingAdvocate);
                        Console.WriteLine($"Modified event without Added for Advocate: '{@event.UID}' ");
                    }

                    existingAdvocate.UID = modifiedAdvocate.NewUID;
                    existingAdvocate.FileName = modifiedAdvocate.NewFileName;
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
            Console.WriteLine("GenerateCurrentState completed.");

            var json = JsonSerializer.Serialize(advocates);

            await storage.SaveFileToBlobStorage("current-advocates.json", json, "application/json");
        }

        private async Task<List<AdvocateEvent>> GetAllEvents()
        {
            string json = await storage.ReadFileContent("all-events.json");
            var options = new JsonSerializerOptions { Converters = { new AdvocateEventsConverter() } };
            return JsonSerializer.Deserialize<List<AdvocateEvent>>(json, options);
        }
    }
}