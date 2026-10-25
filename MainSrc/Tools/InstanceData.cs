using System;
using System.Collections.Generic;

namespace Project.Launch.Tools
{
    public class InstanceInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        /// <summary>"standard"（.minecraft 根目录） 或 "isolated"（隔离实例）</summary>
        public string Layout { get; set; } = "standard";
        public string VersionId { get; set; } = string.Empty;
        public string JavaPath { get; set; } = string.Empty;
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    }

    public class InstancesRoot
    {
        public List<InstanceInfo> Instances { get; set; } = new();
        public string SelectedInstanceId { get; set; } = string.Empty;
    }
}