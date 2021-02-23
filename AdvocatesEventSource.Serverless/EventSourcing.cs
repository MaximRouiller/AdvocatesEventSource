using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AdvocatesEventSource.Data;
using AdvocatesEventSource.Data.Model;
using AdvocatesEventSource.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AdvocatesEventSource.Serverless
{
    public class EventSourcing
    {
        private string GitHubToken = Environment.GetEnvironmentVariable("GitHubToken");
        private readonly AzureStorageHelper storage;

        public EventSourcing(AzureStorageHelper storage)
        {
            this.storage = storage;
        }

        // every hours
        [FunctionName(nameof(UpdateNewEventsTimer))]
        public void UpdateNewEventsTimer([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, [DurableClient] IDurableOrchestrationClient client, ILogger log)
        {
            log.LogInformation($"{nameof(UpdateNewEventsTimer)} has been run.");
            client.StartNewAsync(nameof(UpdateNewEventsOrchestrator));
        }

        [FunctionName(nameof(Advocates))]
        public async Task<List<AdvocateMapping>> Advocates(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            List<AdvocateMapping> advocatesResult = JsonSerializer.Deserialize<List<AdvocateMapping>>(await storage.ReadFileContent("current-advocates.json"));

            return advocatesResult;
        }

        [FunctionName(nameof(UpdateNewEventsOrchestrator))]
        public async Task UpdateNewEventsOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            string lastCommit = await context.CallActivityAsync<string>(nameof(GetLatestSyncCommitSHA), null);

            var lastCommitSha = await context.CallActivityAsync<string>(nameof(CreateNewEventsFromNewCommits), lastCommit);
            
            if (!string.IsNullOrWhiteSpace(lastCommitSha))
                await context.CallActivityAsync(nameof(UpdateLastProcessedCommit), lastCommitSha);
            
        }


        [FunctionName(nameof(UpdateLastProcessedCommit))]
        public async Task UpdateLastProcessedCommit([ActivityTrigger] IDurableActivityContext context)
        {
            var lastCommitSha = context.GetInput<string>();
            await storage.SaveFileToBlobStorage("last-processed-commit.txt", lastCommitSha, "text/plain");
        }

        [FunctionName(nameof(GetLatestSyncCommitSHA))]
        public async Task<string> GetLatestSyncCommitSHA([ActivityTrigger] IDurableActivityContext context)
        {
            string lastCommitSha = await storage.ReadFileContent("last-processed-commit.txt");

            return lastCommitSha;
        }


        [FunctionName(nameof(CreateNewEventsFromNewCommits))]
        public async Task<string> CreateNewEventsFromNewCommits([ActivityTrigger] IDurableActivityContext context)
        {
            var newEvents = new List<AdvocateEvent>();
            var github = GetGitHubClient();
            string lastSha = context.GetInput<string>();

            var changesSinceLastCheck = await github
                .Repository
                .Commit
                .Compare("MicrosoftDocs", "cloud-developer-advocates", lastSha, "master");

            var commits = changesSinceLastCheck.Commits;

            foreach (var commit in commits)
            {
                // we skip merge commits
                if (commit.Parents.Count() != 1) continue;

                var fullCommit = await github.Repository.Commit.Get("MicrosoftDocs", "cloud-developer-advocates", commit.Sha);

                CompareResult changes = await github
                    .Repository
                    .Commit
                    .Compare("MicrosoftDocs", "cloud-developer-advocates", fullCommit.Parents.First().Sha, commit.Sha);

                foreach (var change in changes.Files)
                {
                    if (!change.Filename.StartsWith("advocates/")) continue;

                    if (!IsInExclusionList(Path.GetFileName(change.Filename)) && IsYamlFile(change.Filename))
                    {
                        string content;

                        switch (change.Status)
                        {
                            case "renamed":
                                var fileExtension = Path.GetExtension(change.PreviousFileName);
                                if (new[] { ".md", "" }.Contains(fileExtension))
                                {
                                    content = ConvertByteToString(await github.Repository.Content.GetRawContentByRef("MicrosoftDocs", "cloud-developer-advocates", change.Filename, commit.Sha));
                                    newEvents.Add(CreateAdvocateAddedEvent(change.Filename, content, new DateTimeOffset()));
                                }
                                else
                                {
                                    content = ConvertByteToString(await github.Repository.Content.GetRawContentByRef("MicrosoftDocs", "cloud-developer-advocates", change.Filename, commit.Sha));
                                    var renamedFileContent = ConvertByteToString(await github.Repository.Content.GetRawContentByRef("MicrosoftDocs", "cloud-developer-advocates", change.PreviousFileName, commit.Parents.First().Sha));
                                    var renamedFileUID = ReadUID(renamedFileContent);
                                    newEvents.Add(CreateAdvocateModifiedEvent(change.Filename, content, fullCommit.Commit.Committer.Date, change.PreviousFileName, renamedFileUID));
                                }
                                break;
                            case "added":
                                content = ConvertByteToString(await github.Repository.Content.GetRawContentByRef("MicrosoftDocs", "cloud-developer-advocates", change.Filename, commit.Sha));
                                newEvents.Add(CreateAdvocateAddedEvent(change.Filename, content, fullCommit.Commit.Committer.Date));
                                break;
                            case "deleted":
                                content = ConvertByteToString(await github.Repository.Content.GetRawContentByRef("MicrosoftDocs", "cloud-developer-advocates", change.Filename, commit.Sha));
                                newEvents.Add(CreateAdvocateRemovedEvent(change.Filename, content, fullCommit.Commit.Committer.Date));
                                break;
                            case "modified":
                                content = ConvertByteToString(await github.Repository.Content.GetRawContentByRef("MicrosoftDocs", "cloud-developer-advocates", change.Filename, commit.Sha));
                                string oldUID = null;
                                if (change.PreviousFileName != null)
                                {
                                    string oldContent = ConvertByteToString(await github.Repository.Content.GetRawContentByRef("MicrosoftDocs", "cloud-developer-advocates", change.PreviousFileName, commit.Parents.First().Sha));
                                    oldUID = ReadUID(oldContent);
                                }
                                newEvents.Add(CreateAdvocateModifiedEvent(change.Filename, content, fullCommit.Commit.Committer.Date, change.PreviousFileName ?? change.Filename, oldUID ?? ReadUID(content)));
                                break;
                            default:
                                System.Diagnostics.Debug.WriteLine($"Unhandled event: {change.Status}");
                                break;
                        }
                    }
                }
            }

            if (newEvents.Count > 0)
            {
                string json = await storage.ReadFileContent("all-events.json");
                var options = new JsonSerializerOptions { Converters = { new AdvocateEventsConverter() } };
                var allEvents = JsonSerializer.Deserialize<List<AdvocateEvent>>(json, options);

                allEvents.AddRange(newEvents);

                string newJson = JsonSerializer.Serialize(allEvents, options);
                await storage.SaveFileToBlobStorage("all-events.json", newJson, "application/json");
            }
            return commits.LastOrDefault()?.Sha;
        }

        private string ConvertByteToString(byte[] rawBytesContent)
        {
            return System.Text.Encoding.Default.GetString(rawBytesContent);
        }

        private GitHubClient GetGitHubClient()
        {
            var github = new GitHubClient(new ProductHeaderValue("cloud-advocate-eventsource"));

            var tokenAuth = new Credentials(GitHubToken);
            github.Credentials = tokenAuth;
            return github;
        }


        private bool IsInExclusionList(string filename)
        {
            var exclusionList = new[] { "index.html.yml", "map.yml", "toc.yml", "index.yml", "twitter.yml", "tweets.yml" };
            return exclusionList.Contains(filename);
        }

        private bool IsYamlFile(string filename)
        {
            return new[] { ".yml", ".yaml" }.Contains(Path.GetExtension(filename));
        }

        private AdvocateModified CreateAdvocateModifiedEvent(string path, string content, DateTimeOffset when, string oldPath, string oldUID)
        {
            var addedEvent = CreateAdvocateAddedEvent(path, content, when);
            return new AdvocateModified
            {
                UID = oldUID,
                FileName = oldPath,
                NewUID = addedEvent.UID,
                NewFileName = addedEvent.FileName,
                EventDate = addedEvent.EventDate,

                NewName = addedEvent.Name,
                NewGitHubUserName = addedEvent.GitHubUserName,
                NewAlias = addedEvent.Alias,
                NewTeam = addedEvent.Team,
                NewTwitterHandle = addedEvent.TwitterHandle,
            };
        }

        private AdvocateRemoved CreateAdvocateRemovedEvent(string filename, string content, DateTimeOffset eventDate)
        {
            return new AdvocateRemoved
            {
                FileName = filename,
                UID = ReadUID(content),
                EventDate = eventDate
            };
        }
        private AdvocateAdded CreateAdvocateAddedEvent(string filename, string content, DateTimeOffset eventDate)
        {
            return new AdvocateAdded
            {
                UID = ReadUID(content),
                FileName = filename,
                EventDate = eventDate,
                Name = ReadName(content),
                Alias = ReadAlias(content),
                Team = ReadTeam(content),
                GitHubUserName = ReadGitHubUsername(content),
                TwitterHandle = ReadTwitterUsername(content)
            };
        }
        private string ReadUID(string content)
        {
            Regex uid = new Regex("uid: (?<uid>.*)\n");
            return uid.Match(content).Groups["uid"].Value.Replace("\r", string.Empty);
        }

        private string ReadName(string content)
        {
            Regex name = new Regex($"name: (?<{nameof(name)}>.*)\n");
            return name.Match(content).Groups[nameof(name)].Value.Replace("\r", string.Empty);
        }
        private string ReadAlias(string content)
        {
            Regex alias = new Regex($"ms.author: (?<{nameof(alias)}>.*)\n");
            return alias.Match(content).Groups[nameof(alias)].Value.Replace("\r", string.Empty);
        }
        private string ReadTeam(string content)
        {
            Regex team = new Regex($"team: (?<{nameof(team)}>.*)\n");
            return team.Match(content).Groups[nameof(team)].Value.Replace("\r", string.Empty);
        }

        private string ReadGitHubUsername(string content)
        {
            Regex github_v1 = new Regex($"github: (http|https)://github.com/(?<{nameof(github_v1)}>.*)\n");
            Regex github_v2 = new Regex($"    url: (http|https)://github.com/(?<{nameof(github_v2)}>.*)\n");
            var githubUsername_v1 = MatchAndClean(github_v1, nameof(github_v1), content);
            var githubUsername_v2 = MatchAndClean(github_v2, nameof(github_v2), content);

            return githubUsername_v1 == string.Empty ? githubUsername_v2 : githubUsername_v1;
        }

        private string ReadTwitterUsername(string content)
        {
            Regex twitter_v1 = new Regex($"github: (http|https)://twitter.com/(?<{nameof(twitter_v1)}>.*)\n");
            Regex twitter_v2 = new Regex($"    url: (http|https)://twitter.com/(?<{nameof(twitter_v2)}>.*)\n");
            var twitterUsername_v1 = MatchAndClean(twitter_v1, nameof(twitter_v1), content);
            var twitterUsername_v2 = MatchAndClean(twitter_v2, nameof(twitter_v2), content);
            return twitterUsername_v1 == string.Empty ? twitterUsername_v2 : twitterUsername_v1;
        }

        private string MatchAndClean(Regex regex, string groupName, string content)
        {
            return regex.Match(content).Groups[groupName].Value.Replace("\r", string.Empty);
        }
    }
}