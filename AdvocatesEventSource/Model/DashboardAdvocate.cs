﻿using System.Text.Json.Serialization;

namespace AdvocatesEventSource.Model
{
    public class DashboardAdvocate
    {
        [JsonIgnore]
        public string UID { get; set; }
        [JsonIgnore]
        public string FileName { get; set; }

        public string GitHubUserName { get; set; }
        public string Team { get; set; }
        public string Alias { get; set; }
    }
}