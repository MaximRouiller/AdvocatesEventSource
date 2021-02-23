using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AdvocatesEventSource.Data.Model;
using LibGit2Sharp;

namespace AdvocatesEventSource.Data.Parser
{
    public class AdvocateEventGenerator
    {
        private readonly DirectoryInfo gitDirectory;
        private readonly string githubToken;

        public string LastProcessedCommitSha { get; private set; } = string.Empty;

        public AdvocateEventGenerator(DirectoryInfo gitDirectory)
        {

            this.gitDirectory = gitDirectory;
        }

        public AdvocateEventGenerator(string githubToken)
        {
            this.githubToken = githubToken;
        }

        public List<AdvocateEvent> GenerateAllEvents()
        {
            var events = new List<AdvocateEvent>();
            if (gitDirectory != null)
            {
                //todo: generate from local
                var repository = new Repository(gitDirectory.FullName);
                var filter = new CommitFilter
                {
                    SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse,
                };

                var lastCommitSha = repository.Commits.First().Sha;
                LastProcessedCommitSha = lastCommitSha;
                IEnumerable<Commit> commits = repository.Commits.QueryBy(filter);

                events = GetAllLocalEventsFromCommitsList(repository, commits);
            }

            if (githubToken != null)
            {
                //todo: generate from remote
            }
            return events;
        }

        private List<AdvocateEvent> GetAllRemoteEventsFromCommitsList()
        {
            var newEvents = new List<AdvocateEvent>();
            return newEvents;
        }

        private List<AdvocateEvent> GetAllLocalEventsFromCommitsList(Repository repository, IEnumerable<Commit> commits)
        {
            List<AdvocateEvent> newEvents = new List<AdvocateEvent>();
            foreach (var commit in commits)
            {
                // we skip merge commits
                if (commit.Parents.Count() != 1) continue;

                TreeChanges treeChanges = repository.Diff.Compare<TreeChanges>(commit.Parents.First()?.Tree, commit.Tree);
                if (treeChanges != null)
                {
                    foreach (var change in treeChanges)
                    {
                        if (!change.Path.StartsWith("advocates/")) continue;

                        if (!IsInExclusionList(Path.GetFileName(change.Path)) && IsYamlFile(change.Path))
                        {
                            string content;

                            switch (change.Status)
                            {
                                case ChangeKind.Renamed:
                                    // If we renamed the markdown file to YAML files, we assume it's an "added" event
                                    var fileExtension = Path.GetExtension(change.OldPath);
                                    if (new[] { ".md", "" }.Contains(fileExtension))
                                    {
                                        content = GetFileContent(GetTreeEntry(commit, change.Path));
                                        newEvents.Add(CreateAdvocateAddedEvent(change.Path, content, commit.Author.When));
                                    }
                                    else
                                    {
                                        content = GetFileContent(GetTreeEntry(commit, change.Path));
                                        var renamedFileContent = GetFileContent(GetTreeEntry(commit.Parents.First().Sha, repository, change.OldPath));
                                        var renamedFileUID = ReadUID(renamedFileContent);
                                        newEvents.Add(CreateAdvocateModifiedEvent(change.Path, content, commit.Author.When, change.OldPath, renamedFileUID));
                                    }
                                    break;
                                case ChangeKind.Added:
                                    content = GetFileContent(GetTreeEntry(commit, change.Path));
                                    newEvents.Add(CreateAdvocateAddedEvent(change.Path, content, commit.Author.When));
                                    break;
                                case ChangeKind.Deleted:
                                    content = GetFileContent(GetTreeEntry(commit.Parents.First().Sha, repository, change.Path));
                                    newEvents.Add(CreateAdvocateRemovedEvent(change.Path, content, commit.Author.When));
                                    break;
                                case ChangeKind.Modified:
                                    content = GetFileContent(GetTreeEntry(commit, change.Path));
                                    var oldContent = GetFileContent(GetTreeEntry(commit.Parents.First().Sha, repository, change.Path));
                                    var oldUID = ReadUID(oldContent);
                                    newEvents.Add(CreateAdvocateModifiedEvent(change.Path, content, commit.Author.When, change.OldPath, oldUID));
                                    break;
                                default:
                                    continue;

                            }
                        }
                    }
                }
            }

            return newEvents;
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

        private bool IsYamlFile(string filename)
        {
            return new[] { ".yml", ".yaml" }.Contains(Path.GetExtension(filename));
        }
    }
}
