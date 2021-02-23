using System;

namespace AdvocatesEventSource.Data.Model
{
    [System.Diagnostics.DebuggerDisplay("Advocate: {UID}, {Name}")]
    public class Advocate
    {
        public string UID { get; set; }
        public string FileName { get; set; }

        public string Name { get; set; }
        public string GitHubUserName { get; set; }
        public string Team { get; set; }
        public string Alias { get; set; }
        public string TwitterHandle { get; set; }
    }
}