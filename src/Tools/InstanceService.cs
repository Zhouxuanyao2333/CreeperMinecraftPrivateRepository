using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Project.Launch.Tools
{
    public static class InstanceService
    {
        private static readonly object _lock = new object();

        private static string FilePath => Path.Combine(LauncherPaths.Root, "instances.json");

        public static InstancesRoot LoadInstances()
        {
            lock (_lock)
            {
                if (!File.Exists(FilePath))
                    return new InstancesRoot();

                try
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<InstancesRoot>(json) ?? new InstancesRoot();
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"读取实例失败: {ex.Message}",
                        $"Failed to load instances: {ex.Message}");
                    return new InstancesRoot();
                }
            }
        }

        public static void SaveInstances(InstancesRoot root)
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(root,
                        new JsonSerializerOptions { WriteIndented = true });
                    string tmp = FilePath + ".tmp";
                    File.WriteAllText(tmp, json);
                    File.Move(tmp, FilePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"保存实例失败: {ex.Message}",
                        $"Failed to save instances: {ex.Message}");
                }
            }
        }

        public static InstanceInfo? GetSelectedInstance()
        {
            var root = LoadInstances();
            if (string.IsNullOrEmpty(root.SelectedInstanceId))
                return null;
            return root.Instances.FirstOrDefault(i => i.Id == root.SelectedInstanceId);
        }

        public static void SetSelectedInstance(InstanceInfo instance)
        {
            var root = LoadInstances();
            var existing = root.Instances.FirstOrDefault(i => i.Id == instance.Id);
            if (existing == null)
                root.Instances.Add(instance);
            else
            {
                existing.Name = instance.Name;
                existing.RootPath = instance.RootPath;
                existing.Layout = instance.Layout;
                existing.VersionId = instance.VersionId;
                existing.JavaPath = instance.JavaPath;
                existing.LastUsed = DateTime.UtcNow;
            }
            root.SelectedInstanceId = instance.Id;
            SaveInstances(root);
        }
    }
}