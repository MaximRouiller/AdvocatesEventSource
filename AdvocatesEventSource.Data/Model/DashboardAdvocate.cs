﻿using System;
using System.Text.Json.Serialization;

namespace AdvocatesEventSource.Data.Model
{
    [System.Diagnostics.DebuggerDisplay("Advocate: {UID}")]
    public class DashboardAdvocate
    {
        [JsonIgnore]
        public string UID { get; set; }
        [JsonIgnore]
        public string FileName { get; set; }

        public string GitHubUserName { get; set; }
        public string RedditUserName { get; set; }
        public string Team { get; set; }
        public string Alias { get; set; }
        public string Name { get; set; }
        public DateTimeOffset AddedDate { get; set; }
        public DateTimeOffset? RemovedDate { get; set; }
    }
}