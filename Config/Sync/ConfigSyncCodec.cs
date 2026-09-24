// Config/Sync/ConfigSyncCodec.cs
//
// PURPOSE: The multiplayer host-config package: wire format, validation, hashing, the host/client
//          version rule and the multiplier text format. Pure and game-independent (no BepInEx, no SE,
//          no file I/O), so the "ConfigSync" on-load test suite covers all of it.
//
// WIRE FORMAT v1 (little-endian, see docs/HOST_SYNC_DESIGN.md section 5):
//   header: "CDTS" | uint16 format version | string host mod version | int32 body length | SHA-256(body)
//   then gzip(body); body: uint16 count, then per entry: uint16 id | int32 length | bytes
//
// IMPORTANT FOR AI AGENTS:
// - Entries carry numeric ids, NEVER file names. The id -> file name mapping below is the whitelist;
//   an unknown or duplicate id rejects the whole package. Do not add a "name" field to the format.
// - Validation is all-or-nothing and happens before anything is written or applied.
// - Changing the layout means bumping FormatVersion (clients reject other versions).
// - Adding a synced file = new SyncFileId value + RelativePathFor case + ConfigSyncManager source path.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Config.Toml;

namespace CrusaderDETweaker.Config.Sync
{
    /// <summary>Fixed identifiers of the synced config files (the path whitelist).</summary>
    internal enum SyncFileId : ushort
    {
        Units = 1,
        Structures = 2,
        GameplaySettings = 3,
        MeleeDamage = 10,
        RangedDamage = 11,
        EunuchAoeDamage = 12,
        BallistaDamage = 13,
        UnitFireDamage = 14,
        BedouinHeal = 15,
        BuildingFireDamage = 16,
        GlobalMultipliers = 20
    }

    /// <summary>A validated host package.</summary>
    internal sealed class ConfigSyncPackage
    {
        public string HostModVersion;
        public Dictionary<SyncFileId, byte[]> Entries;
        public byte[] BodyHash;
        public int BodyBytes;
        public int WireBytes;
    }

    internal static class ConfigSyncCodec
    {
        internal const ushort FormatVersion = 1;

        /// <summary>
        /// Largest package on the wire. Steam's reliable-message cap is 524,288 bytes
        /// (Steamworks.Constants.k_cbMaxSteamNetworkingSocketsMessageSizeSend) and the game's own map
        /// transfer uses 250,000-byte messages; SE's envelope adds a few hundred bytes on top.
        /// </summary>
        internal const int MaxWireBytes = 200000;

        /// <summary>Largest uncompressed body (bounds decompression).</summary>
        internal const int MaxBodyBytes = 4 * 1024 * 1024;

        /// <summary>Largest single entry (the biggest real file, the melee CSV, is ~71 KB).</summary>
        internal const int MaxEntryBytes = 1024 * 1024;

        internal const int MaxVersionBytes = 32;
        internal const int MaxMultiplierEntries = 64;

        private static readonly byte[] Magic = { (byte)'C', (byte)'D', (byte)'T', (byte)'S' };
        private const int HashBytes = 32;

        // ------------------------------------------------------------------
        // Whitelist
        // ------------------------------------------------------------------

        /// <summary>Every id this build knows, in pack order.</summary>
        internal static readonly SyncFileId[] AllIds =
        {
            SyncFileId.Units, SyncFileId.Structures, SyncFileId.GameplaySettings,
            SyncFileId.MeleeDamage, SyncFileId.RangedDamage, SyncFileId.EunuchAoeDamage,
            SyncFileId.BallistaDamage, SyncFileId.UnitFireDamage, SyncFileId.BedouinHeal,
            SyncFileId.BuildingFireDamage, SyncFileId.GlobalMultipliers
        };

        internal static bool IsKnown(SyncFileId id) => Array.IndexOf(AllIds, id) >= 0;

        /// <summary>
        /// The fixed file name (relative to the host-sync folder) an id is stored under. The name
        /// never comes from the package. Throws for an unknown id.
        /// </summary>
        internal static string RelativePathFor(SyncFileId id)
        {
            switch (id)
            {
                case SyncFileId.Units: return ConfigPaths.UnitsFileName;
                case SyncFileId.Structures: return ConfigPaths.StructuresFileName;
                case SyncFileId.GameplaySettings: return ConfigPaths.GlobalsFileName;
                case SyncFileId.MeleeDamage: return Path.Combine(MatrixPaths.MatrixDirName, MatrixPaths.MeleeDamageFileName);
                case SyncFileId.RangedDamage: return Path.Combine(MatrixPaths.MatrixDirName, MatrixPaths.RangedDamageFileName);
                case SyncFileId.EunuchAoeDamage: return Path.Combine(MatrixPaths.MatrixDirName, MatrixPaths.EunuchAoeDamageFileName);
                case SyncFileId.BallistaDamage: return Path.Combine(MatrixPaths.MatrixDirName, MatrixPaths.BallistaDamageFileName);
                case SyncFileId.UnitFireDamage: return Path.Combine(MatrixPaths.MatrixDirName, MatrixPaths.UnitFireDamageFileName);
                case SyncFileId.BedouinHeal: return Path.Combine(MatrixPaths.MatrixDirName, MatrixPaths.BedouinHealFileName);
                case SyncFileId.BuildingFireDamage: return Path.Combine(MatrixPaths.MatrixDirName, MatrixPaths.BuildingFireDamageFileName);
                case SyncFileId.GlobalMultipliers: return "CrusaderDETweaker_GlobalMultipliers.host.txt";
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown sync file id");
            }
        }

        // ------------------------------------------------------------------
        // Pack
        // ------------------------------------------------------------------

        /// <summary>
        /// Build a package. Entries must use known ids and be non-empty. Throws ArgumentException on
        /// bad input and InvalidOperationException when the result would exceed MaxWireBytes.
        /// </summary>
        internal static byte[] Pack(string modVersion, IDictionary<SyncFileId, byte[]> entries, out byte[] bodyHash, out int bodyBytes)
        {
            ValidateVersionString(modVersion);
            if (entries == null || entries.Count == 0) throw new ArgumentException("Package holds no files", nameof(entries));

            byte[] body = BuildBody(entries);
            bodyBytes = body.Length;
            bodyHash = Sha256(body);

            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    w.Write(Magic);
                    w.Write(FormatVersion);
                    w.Write(modVersion);
                    w.Write(body.Length);
                    w.Write(bodyHash);
                }
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    gz.Write(body, 0, body.Length);

                byte[] wire = ms.ToArray();
                if (wire.Length > MaxWireBytes)
                    throw new InvalidOperationException($"Package is {wire.Length} bytes, over the {MaxWireBytes}-byte limit");
                return wire;
            }
        }

        /// <summary>SHA-256 of the body the given entries would produce (host-side change check).</summary>
        internal static byte[] ComputeBodyHash(IDictionary<SyncFileId, byte[]> entries) => Sha256(BuildBody(entries));

        private static byte[] BuildBody(IDictionary<SyncFileId, byte[]> entries)
        {
            if (entries.Count > AllIds.Length) throw new ArgumentException("Too many entries", nameof(entries));

            int total = 2;
            foreach (var kvp in entries)
            {
                if (!IsKnown(kvp.Key)) throw new ArgumentException($"Unknown sync file id {(ushort)kvp.Key}", nameof(entries));
                if (kvp.Value == null || kvp.Value.Length == 0) throw new ArgumentException($"Entry {kvp.Key} is empty", nameof(entries));
                if (kvp.Value.Length > MaxEntryBytes) throw new ArgumentException($"Entry {kvp.Key} is {kvp.Value.Length} bytes, over {MaxEntryBytes}", nameof(entries));
                total += 6 + kvp.Value.Length;
            }
            if (total > MaxBodyBytes) throw new ArgumentException($"Body is {total} bytes, over {MaxBodyBytes}", nameof(entries));

            using (var ms = new MemoryStream(total))
            using (var w = new BinaryWriter(ms))
            {
                w.Write((ushort)entries.Count);
                // Fixed order (AllIds), so the same files always give the same body and hash.
                foreach (var id in AllIds)
                {
                    if (!entries.TryGetValue(id, out var data)) continue;
                    w.Write((ushort)id);
                    w.Write(data.Length);
                    w.Write(data);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        // ------------------------------------------------------------------
        // Unpack
        // ------------------------------------------------------------------

        /// <summary>
        /// Validate and decode a package. Returns false with a one-line, user-readable reason on any
        /// problem; nothing is partially accepted.
        /// </summary>
        internal static bool TryUnpack(byte[] wire, out ConfigSyncPackage package, out string error)
        {
            package = null;
            error = null;
            try
            {
                if (wire == null || wire.Length == 0) { error = "empty package (the host may run an older version, or its package failed to build)"; return false; }
                if (wire.Length > MaxWireBytes) { error = $"package is {wire.Length} bytes, over the {MaxWireBytes}-byte limit"; return false; }

                using (var ms = new MemoryStream(wire, writable: false))
                using (var r = new BinaryReader(ms, Encoding.UTF8))
                {
                    if (ms.Length < Magic.Length + 2) { error = "package is truncated"; return false; }
                    byte[] magic = r.ReadBytes(Magic.Length);
                    for (int i = 0; i < Magic.Length; i++)
                        if (magic[i] != Magic[i]) { error = "not a Crusader DE Tweaker config package"; return false; }

                    ushort format = r.ReadUInt16();
                    if (format != FormatVersion) { error = $"package format v{format}, this build reads v{FormatVersion}"; return false; }

                    // BinaryReader string = 7-bit length prefix + UTF-8. Bound the length before reading.
                    int versionLength = ms.Position < ms.Length ? wire[(int)ms.Position] : -1;
                    if (versionLength < 1 || versionLength > MaxVersionBytes) { error = "invalid host version field"; return false; }
                    string hostVersion = r.ReadString();
                    if (!IsValidVersionString(hostVersion)) { error = "invalid host version field"; return false; }

                    int bodyLength = r.ReadInt32();
                    if (bodyLength < 2 || bodyLength > MaxBodyBytes) { error = $"declared size {bodyLength} is out of range"; return false; }

                    byte[] expectedHash = r.ReadBytes(HashBytes);
                    if (expectedHash.Length != HashBytes) { error = "package is truncated"; return false; }

                    byte[] body = new byte[bodyLength];
                    int read = 0;
                    using (var gz = new GZipStream(ms, CompressionMode.Decompress, leaveOpen: true))
                    {
                        while (read < bodyLength)
                        {
                            int n = gz.Read(body, read, bodyLength - read);
                            if (n <= 0) break;
                            read += n;
                        }
                        if (read != bodyLength) { error = $"content is shorter than declared ({read} of {bodyLength} bytes)"; return false; }
                        // Anything past the declared size (bounded read of one byte) is rejected, never buffered.
                        if (gz.ReadByte() != -1) { error = "content is longer than declared"; return false; }
                    }

                    if (!BytesEqual(Sha256(body), expectedHash)) { error = "content hash mismatch (corrupted in transit?)"; return false; }

                    if (!TryParseBody(body, out var entries, out error)) return false;

                    package = new ConfigSyncPackage
                    {
                        HostModVersion = hostVersion,
                        Entries = entries,
                        BodyHash = expectedHash,
                        BodyBytes = bodyLength,
                        WireBytes = wire.Length
                    };
                    return true;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidDataException || ex is IOException || ex is DecoderFallbackException || ex is FormatException)
            {
                error = $"package is malformed ({ex.GetType().Name})";
                return false;
            }
        }

        private static bool TryParseBody(byte[] body, out Dictionary<SyncFileId, byte[]> entries, out string error)
        {
            entries = new Dictionary<SyncFileId, byte[]>();
            error = null;
            using (var ms = new MemoryStream(body, writable: false))
            using (var r = new BinaryReader(ms))
            {
                ushort count = r.ReadUInt16();
                if (count == 0) { error = "package holds no files"; return false; }
                if (count > AllIds.Length) { error = $"package lists {count} files, this build knows {AllIds.Length}"; return false; }

                for (int i = 0; i < count; i++)
                {
                    if (ms.Length - ms.Position < 6) { error = "file table is truncated"; return false; }
                    var id = (SyncFileId)r.ReadUInt16();
                    int length = r.ReadInt32();
                    if (!IsKnown(id)) { error = $"unknown file id {(ushort)id}"; return false; }
                    if (entries.ContainsKey(id)) { error = $"file {id} listed twice"; return false; }
                    if (length <= 0) { error = $"file {id} is empty"; return false; }
                    if (length > MaxEntryBytes) { error = $"file {id} is {length} bytes, over {MaxEntryBytes}"; return false; }
                    if (ms.Length - ms.Position < length) { error = $"file {id} is truncated"; return false; }
                    entries[id] = r.ReadBytes(length);
                }

                if (ms.Position != ms.Length) { error = "unexpected data after the last file"; return false; }
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Version rule (docs/HOST_SYNC_DESIGN.md section 7)
        // ------------------------------------------------------------------

        internal enum VersionVerdict { Same, PatchDiffers, Incompatible }

        /// <summary>Same major.minor = compatible (a patch difference is only warned about).</summary>
        internal static VersionVerdict CompareVersions(string hostVersion, string localVersion)
        {
            if (string.Equals(hostVersion, localVersion, StringComparison.Ordinal)) return VersionVerdict.Same;
            if (!TryMajorMinor(hostVersion, out int hMaj, out int hMin) || !TryMajorMinor(localVersion, out int lMaj, out int lMin))
                return VersionVerdict.Incompatible;
            return hMaj == lMaj && hMin == lMin ? VersionVerdict.PatchDiffers : VersionVerdict.Incompatible;
        }

        private static bool TryMajorMinor(string version, out int major, out int minor)
        {
            major = minor = 0;
            if (string.IsNullOrEmpty(version)) return false;
            string[] parts = version.Split('.', '-', '+');
            return parts.Length >= 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor);
        }

        // ------------------------------------------------------------------
        // Multiplier values (entry GlobalMultipliers): "Key=value" lines, sorted, invariant culture
        // ------------------------------------------------------------------

        internal static byte[] FormatMultipliers(IDictionary<string, float> values)
        {
            if (values == null || values.Count == 0) throw new ArgumentException("No multiplier values", nameof(values));
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (var key in keys)
            {
                if (!IsValidKey(key)) throw new ArgumentException($"Invalid multiplier key '{key}'", nameof(values));
                sb.Append(key).Append('=').Append(values[key].ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        internal static bool TryParseMultipliers(byte[] data, out Dictionary<string, float> values, out string error)
        {
            values = new Dictionary<string, float>(StringComparer.Ordinal);
            error = null;
            if (data == null || data.Length == 0) { error = "multiplier list is empty"; return false; }

            string text;
            try { text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(data); }
            catch (DecoderFallbackException) { error = "multiplier list is not valid UTF-8"; return false; }

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) { error = $"multiplier line without '=': '{Truncate(line)}'"; return false; }
                string key = line.Substring(0, eq);
                string valueText = line.Substring(eq + 1);
                if (!IsValidKey(key)) { error = $"invalid multiplier key '{Truncate(key)}'"; return false; }
                if (values.ContainsKey(key)) { error = $"multiplier '{key}' listed twice"; return false; }
                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    || float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    error = $"multiplier '{key}' has an invalid value '{Truncate(valueText)}'";
                    return false;
                }
                if (values.Count >= MaxMultiplierEntries) { error = $"more than {MaxMultiplierEntries} multipliers"; return false; }
                values[key] = value;
            }

            if (values.Count == 0) { error = "multiplier list is empty"; return false; }
            return true;
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 64) return false;
            foreach (char c in key)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
            return true;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>First 12 hex digits of a hash, the form both sides log.</summary>
        internal static string ShortHash(byte[] hash)
        {
            if (hash == null) return "none";
            var sb = new StringBuilder(12);
            for (int i = 0; i < 6 && i < hash.Length; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] Sha256(byte[] data)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(data);
        }

        private static void ValidateVersionString(string version)
        {
            if (!IsValidVersionString(version)) throw new ArgumentException($"Invalid version '{version}'", nameof(version));
        }

        private static bool IsValidVersionString(string version)
        {
            if (string.IsNullOrEmpty(version) || Encoding.UTF8.GetByteCount(version) > MaxVersionBytes) return false;
            foreach (char c in version)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '+' || c == '-')) return false;
            return true;
        }

        private static string Truncate(string s) => s.Length <= 40 ? s : s.Substring(0, 40) + "...";
    }
}
