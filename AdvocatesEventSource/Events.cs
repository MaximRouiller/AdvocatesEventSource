using System;
using System.Collections.Generic;
using System.Text;

namespace AdvocatesEventSource
{
    /// <summary>
    /// Specifies the type of statistic exported from an Omnified game.
    /// </summary>
    public enum AdvocateEventType
    {
        AdvocateAdded = 1,
        AdvocateModified = 2,
        AdvocateRemoved = 3,
    }

    public record AdvocateEvent
    {
        public string UID { get; set; }
        public DateTimeOffset EventDate { get; set; }
        public string FileName { get; set; }
    }

    public record AdvocateAdded : AdvocateEvent
    {
        public string Name { get; set; }
        public string GitHubUserName { get; set; }
        public string Team { get; set; }
        public string Alias { get; set; }
        public string TwitterHandle { get; set; }
    }

    public record AdvocateModified : AdvocateEvent
    {
        public string NewName { get; set; }
        public string NewGitHubUserName { get; set; }
        public string NewTeam { get; set; }
        public string NewAlias { get; set; }
        public string NewTwitterHandle { get; set; }
        public string NewUID { get; set; }
        public string NewFileName { get; set; }
    }

    public record AdvocateRemoved : AdvocateEvent
    {
    }
}
