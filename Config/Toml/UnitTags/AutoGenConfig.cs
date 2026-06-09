// Config/Toml/UnitTags/AutoGenConfig.cs
using System;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Toml.UnitTags
{
    internal static class AutoGenConfig
    {
        public static bool AutoGenerateTags { get; private set; } = true;
        public static bool ForceRegenerate { get; private set; } = true; // FORCE ON for debugging

        public static void Load()
        {
            string filePath = ConfigPaths.AutoGen;
            if (!File.Exists(filePath))
            {
                SaveDefault();
                return;
            }

            try
            {
                string toml = File.ReadAllText(filePath);
                var model = Tomlyn.Toml.ToModel(toml);
                
                if (model.ContainsKey("AutoGeneration"))
                {
                    var table = (TomlTable)model["AutoGeneration"];
                    if (table.ContainsKey("Enabled"))
                        AutoGenerateTags = (bool)table["Enabled"];
                    if (table.ContainsKey("ForceRegenerate"))
                        ForceRegenerate = (bool)table["ForceRegenerate"];
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Failed to load AutoGen config: {ex.Message}. Using defaults.");
            }
        }

        public static void SaveDefault()
        {
            const string content = @"# CrusaderDETweaker_AutoGen.toml
# ================================================================================
# AUTO-GENERATION SETTINGS
#
# These settings control the Tag Compression and Auto-Generation system.
# ================================================================================

[AutoGeneration]
Enabled = true              # If true, the system will automatically discover and compress interactions.
ForceRegenerate = false     # If true, will regenerate both .generated.toml and .toml on next launch.
";
            try
            {
                File.WriteAllText(ConfigPaths.AutoGen, content);
            }
            catch { }
        }

        public static void ClearForceRegenerate()
        {
            if (!ForceRegenerate) return;
            
            // Set flag to false and save back
            string content = File.ReadAllText(ConfigPaths.AutoGen);
            content = content.Replace("ForceRegenerate = true", "ForceRegenerate = false");
            File.WriteAllText(ConfigPaths.AutoGen, content);
            ForceRegenerate = false;
        }
    }
}
