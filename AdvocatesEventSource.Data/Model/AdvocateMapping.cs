using System;
using System.Text.Json.Serialization;

namespace AdvocatesEventSource.Data.Model
{
    public class AdvocateMapping
    {
        [JsonPropertyName("GitHubUserName")]
        public string GitHubUsername { get; set; }
        [JsonPropertyName("Alias")]
        public string MicrosoftAlias { get; set; }
        [JsonPropertyName("RedditUserName")]
        public string RedditUserName { get; set; }
        [JsonPropertyName("Team")]
        public string Team { get; set; }
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            return obj is AdvocateMapping other &&
                   GitHubUsername == other.GitHubUsername &&
                   MicrosoftAlias == other.MicrosoftAlias &&
                   Team == other.Team;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GitHubUsername, MicrosoftAlias, Team);
        }
    }
}
