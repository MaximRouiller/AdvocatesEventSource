using AdvocatesEventSource.Data;
using AdvocatesEventSource.Data.Model;
using AdvocatesEventSource.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdvocatesEventSource.CliCommands
{
    public class GenerateAdvocatesForDashboard
    {
        private AzureStorageHelper storage;

        public GenerateAdvocatesForDashboard(string connectionString)
        {
            storage = new AzureStorageHelper(connectionString);
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
                    DashboardAdvocate advocateToAdd = new DashboardAdvocate
                    {
                        UID = addedAdvocate.UID,
                        FileName = addedAdvocate.FileName,
                        GitHubUserName = addedAdvocate.GitHubUserName,
                        Team = addedAdvocate.Team,
                        Alias = addedAdvocate.Alias,
                    };
                    if (!advocates.Any(x => x.FileName == addedAdvocate.FileName || x.UID == addedAdvocate.UID))
                    {
                        advocates.Add(advocateToAdd);
                    }
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
                        existingAdvocate = new DashboardAdvocate();
                        advocates.Add(existingAdvocate);
                        Console.WriteLine($"Modified event without Added for Advocate: '{@event.UID}' ");
                    }

                    existingAdvocate.UID = modifiedAdvocate.NewUID;
                    existingAdvocate.FileName = modifiedAdvocate.NewFileName;
                    existingAdvocate.GitHubUserName = modifiedAdvocate.NewGitHubUserName;
                    existingAdvocate.Team = modifiedAdvocate.NewTeam;
                    existingAdvocate.Alias = modifiedAdvocate.NewAlias;
                }
            }

            Console.WriteLine($"Advocate count: {advocates.Count}");
            Console.WriteLine("GenerateAdvocatesForDashboard completed.");
            //var emptyResults = advocates.Where(x => x.Team == "" && x.Alias == "" && x.GitHubUserName == "").ToList();
            //var someEmptyResults = advocates.Where(x => x.Team == "" || x.Alias == "" || x.GitHubUserName == "").ToList();

            List<DashboardAdvocate> advocatesToSave = advocates.Where(x => x.Alias != "" && x.GitHubUserName != "").ToList();
            await storage.SaveFileToBlobStorage("dashboard-advocates.json", JsonSerializer.Serialize(advocatesToSave), "application/json");
        }

        private async Task<List<AdvocateEvent>> GetAllEvents()
        {
            string json = await storage.ReadFileContent("all-events.json");
            var options = new JsonSerializerOptions { Converters = { new AdvocateEventsConverter() } };
            return JsonSerializer.Deserialize<List<AdvocateEvent>>(json, options);
        }
    }
}
