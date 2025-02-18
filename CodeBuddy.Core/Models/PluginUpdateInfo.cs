using System;

namespace CodeBuddy.Core.Models
{
    public class PluginUpdateInfo
    {
        public string PluginId { get; set; }
        public string PluginName { get; set; }
        public Version CurrentVersion { get; set; }
        public Version LatestVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public DateTime ReleaseDate { get; set; }
        public bool HasBreakingChanges { get; set; }
        public long DownloadSize { get; set; }
    }
}