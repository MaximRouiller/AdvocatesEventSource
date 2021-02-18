using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdvocatesEventSource.Model
{
    public class GenerateAllEvents
    {
        private readonly string _gitPath;
        private readonly string _connectionString;

        public GenerateAllEvents(string gitPath, string azureStorageAccountConnectionString)
        {
            _gitPath = gitPath;
            _connectionString = azureStorageAccountConnectionString;
        }

        public async Task ExecuteAsync()
        {
            var events = new List<AdvocateEvent>();

            var repository = new Repository(_gitPath);
            var filter = new CommitFilter
            {
                SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse,
            };

            foreach (var commit in repository.Commits.QueryBy(filter))
            {
                //todo: inspect if possible bug is here?
                // we skip merge commits
                if (commit.Parents.Count() != 1) continue;

                TreeChanges treeChanges = repository.Diff.Compare<TreeChanges>(commit.Parents.First()?.Tree, commit.Tree);
                if (treeChanges != null)
                {
                    foreach (var change in treeChanges)
                    {
                        if (!change.Path.StartsWith("advocates/")) continue;

                        if (!IsInExclusionList(Path.GetFileName(change.Path)) && new[] {".yml" }.Contains(Path.GetExtension(change.Path)))
                        {
                            string content;
                            // todo: handle renames as added (?)
                            switch (change.Status)
                            {
                                case ChangeKind.Renamed:
                                    break;
                                case ChangeKind.Added:
                                    content = GetFileContent(GetTreeEntry(commit, change.Path));
                                    events.Add(CreateAdvocateAddedEvent(change.Path, content, commit.Author.When));
                                    break;
                                case ChangeKind.Deleted:
                                    content = GetFileContent(GetTreeEntry(commit.Parents.First().Sha, repository, change.Path));
                                    events.Add(CreateAdvocateRemovedEvent(change.Path, content, commit.Author.When));
                                    break;
                                case ChangeKind.Modified:
                                    content = GetFileContent(GetTreeEntry(commit, change.Path));
                                    events.Add(CreateAdvocateModifiedEvent(change.Path, content, commit.Author.When));
                                    break;
                                default:
                                    continue;

                            }
                        }
                    }
                }
            }

            var options = new JsonSerializerOptions { Converters = { new AdvocateEventsConverter() }};
            var json = JsonSerializer.Serialize(events, options);
            var containerClient = new BlobContainerClient(_connectionString, "advocates-events");
            await containerClient.CreateIfNotExistsAsync();
            BlobClient blob = containerClient.GetBlobClient("all-events.json");

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                await writer.FlushAsync();

                stream.Position = 0;
                await blob.UploadAsync(stream, overwrite: true);
            }
            await blob.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "application/json" });

            Console.WriteLine(json);
            Console.WriteLine("Upload completed.");
        }

        private AdvocateModified CreateAdvocateModifiedEvent(string path, string content, DateTimeOffset when)
        {
            var addedEvent = CreateAdvocateAddedEvent(path, content, when);
            return new AdvocateModified
            {
                UID = addedEvent.UID,
                FileName = addedEvent.FileName,
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
            Regex uid = new Regex("uid: (?<uid>.*)\n");
            return new AdvocateRemoved
            {
                FileName = filename,
                UID = uid.Match(content).Groups["uid"].Value.Replace("\r", string.Empty),
                EventDate = eventDate
            };

        }
        private AdvocateAdded CreateAdvocateAddedEvent(string filename, string content, DateTimeOffset eventDate)
        {
            Regex uid = new Regex($"uid: (?<{nameof(uid)}>.*)\n");
            Regex name = new Regex($"name: (?<{nameof(name)}>.*)\n");
            Regex alias = new Regex($"ms.author: (?<{nameof(alias)}>.*)\n");
            Regex team = new Regex($"team: (?<{nameof(team)}>.*)\n");

            Regex github_v1 = new Regex($"github: (http|https)://github.com/(?<{nameof(github_v1)}>.*)\n");
            Regex github_v2 = new Regex($"    url: (http|https)://github.com/(?<{nameof(github_v2)}>.*)\n");
            var githubUsername_v1 = MatchAndClean(github_v1, nameof(github_v1), content);
            var githubUsername_v2 = MatchAndClean(github_v2, nameof(github_v2), content);
            string unifiedGithubUserName = githubUsername_v1 == string.Empty ? githubUsername_v2 : githubUsername_v1;

            Regex twitter_v1 = new Regex($"github: (http|https)://twitter.com/(?<{nameof(twitter_v1)}>.*)\n");
            Regex twitter_v2 = new Regex($"    url: (http|https)://twitter.com/(?<{nameof(twitter_v2)}>.*)\n");
            var twitterUsername_v1 = MatchAndClean(twitter_v1, nameof(twitter_v1), content);
            var twitterUsername_v2 = MatchAndClean(twitter_v2, nameof(twitter_v2), content);
            string unifiedTwitterUserName = twitterUsername_v1 == string.Empty ? twitterUsername_v2 : twitterUsername_v1;

            return new AdvocateAdded
            {
                UID = MatchAndClean(uid, nameof(uid), content),
                FileName = filename,
                EventDate = eventDate,
                Name = MatchAndClean(name, nameof(name), content),
                Alias = MatchAndClean(alias, nameof(alias), content),
                Team = MatchAndClean(team, nameof(team), content),
                GitHubUserName = unifiedGithubUserName,
                TwitterHandle = unifiedTwitterUserName
            };
        }

        private string MatchAndClean(Regex regex, string groupName, string content)
        {
            return regex.Match(content).Groups[groupName].Value.Replace("\r", string.Empty);
        }

        private TreeEntry GetTreeEntry(Commit commit, string path)
        {
            return commit[path];
        }
        private TreeEntry GetTreeEntry(string commitSha, Repository repository, string path)
        {
            var commit = repository.Lookup<Commit>(commitSha);
            return GetTreeEntry(commit, path);
        }
        private string GetFileContent(TreeEntry treeEntry)
        {
            var blob = (Blob)treeEntry.Target;
            var contentStream = blob.GetContentStream();

            using (var sr = new StreamReader(contentStream, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private bool IsInExclusionList(string filename)
        {
            var exclusionList = new[] { "index.html.yml", "map.yml", "toc.yml", "index.yml", "twitter.yml", "tweets.yml" };
            return exclusionList.Contains(filename);
        }
    }
}
