// Config/Core/AtomicFileWriter.cs
//
// PURPOSE: The one way this plugin writes a user config file (TOML and CSV).
//
// WHY: Every launch regenerates/migrates the configs in place. A plain File.WriteAllText truncates
//   the file first, so a crash, kill or power loss mid-write left a user with an empty or half-written
//   config and their edits gone. It also rewrote (and re-timestamped) files whose content had not
//   changed, which confuses editors that have the file open.
//
// HOW:
//   - Content identical to what is on disk -> no write at all (returns false).
//   - Otherwise write "<file>.tmp" in the same folder, then swap it in:
//       target exists  -> File.Replace(tmp, target, "<file>.bak")  (previous version kept as .bak)
//       target missing -> File.Move(tmp, target)
//   - UTF-8 without BOM, the same encoding File.WriteAllText used.
//
// IMPORTANT FOR AI AGENTS:
// - Use this for every config write; do not reintroduce File.WriteAllText on a user file.
// - The .bak holds the version from before the LAST content change, not a history.
//
using System;
using System.IO;
using System.Text;

namespace CrusaderDETweaker.Config.Core
{
    internal static class AtomicFileWriter
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Write <paramref name="content"/> to <paramref name="filePath"/> atomically, creating the
        /// directory if needed. Returns false (and touches nothing) when the file already holds exactly
        /// this content; true when the file was written.
        /// </summary>
        internal static bool WriteIfChanged(string filePath, string content)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path is null or empty", nameof(filePath));
            content = content ?? string.Empty;

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            bool exists = File.Exists(filePath);
            if (exists && string.Equals(File.ReadAllText(filePath), content, StringComparison.Ordinal))
                return false;

            string tmpPath = filePath + ".tmp";
            File.WriteAllText(tmpPath, content, Utf8NoBom);

            if (exists)
            {
                try
                {
                    File.Replace(tmpPath, filePath, filePath + ".bak", ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    // No ReplaceFile on this file system: keep the .bak by copy, then overwrite from the
                    // complete .tmp (still never truncates the target before the new content exists).
                    File.Copy(filePath, filePath + ".bak", overwrite: true);
                    File.Copy(tmpPath, filePath, overwrite: true);
                    File.Delete(tmpPath);
                }
            }
            else
            {
                File.Move(tmpPath, filePath);
            }
            return true;
        }
    }
}
