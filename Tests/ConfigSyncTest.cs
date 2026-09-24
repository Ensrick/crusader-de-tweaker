// Tests/ConfigSyncTest.cs
//
// PURPOSE: Unit tests for the multiplayer host config sync package (Config/Sync/ConfigSyncCodec.cs)
//          and the template baseline journal (Config/Core/TemplateBaseline.cs, BaselineJournal).
//
// TESTS COVER:
// - Round trip (all ids, exact bytes, same hash on both sides, hash independent of insertion order)
// - Corrupt / truncated / wrong-magic / wrong-format-version packages
// - Size caps: oversize wire, declared body over the cap, content longer or shorter than declared
// - Whitelist: unknown id, duplicate id, every mapped path relative with no ".." / root / drive
// - Empty sections: zero files, zero-length entry, trailing bytes after the last entry
// - Version rule (same / patch differs / incompatible) and multiplier text parsing
// - Baseline journal: first write wins, restore newest-first, unreadable cells counted
//
// NOTE: pure logic only - no file I/O, no game API, no network. Runs on every launch via CoreTestRunner.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CrusaderDETweaker.Config.Core;
using CrusaderDETweaker.Config.Sync;

namespace CrusaderDETweaker.Tests
{
    internal static class ConfigSyncTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- ConfigSync Test Suite ---");

            _passedCount = 0;
            _failedCount = 0;

            Test_RoundTrip_AllIds();
            Test_Hash_IndependentOfInsertionOrder();
            Test_Corrupt_FlippedCompressedByte();
            Test_Corrupt_FlippedHashByte();
            Test_Corrupt_Truncated();
            Test_Corrupt_WrongMagic();
            Test_WrongFormatVersion();
            Test_Oversize_Wire();
            Test_Oversize_DeclaredBody();
            Test_Oversize_PackRefuses();
            Test_ContentLongerThanDeclared();
            Test_Whitelist_UnknownIdRejected();
            Test_Whitelist_DuplicateIdRejected();
            Test_Whitelist_PathsAreFixedAndRelative();
            Test_Empty_NoFiles();
            Test_Empty_ZeroLengthEntry();
            Test_Empty_NullOrEmptyWire();
            Test_TrailingBytesRejected();
            Test_VersionRule();
            Test_Multipliers_RoundTrip();
            Test_Multipliers_Rejects();
            Test_Baseline_FirstWriteWinsAndRestoreOrder();

            int totalTests = _passedCount + _failedCount;
            Plugin.Logger.LogInfo($"  ConfigSync: {_passedCount}/{totalTests} tests passed");
            return (_failedCount == 0, totalTests);
        }

        // ===================================================
        // Helpers
        // ===================================================

        private static Dictionary<SyncFileId, byte[]> SampleEntries()
        {
            var entries = new Dictionary<SyncFileId, byte[]>();
            foreach (var id in ConfigSyncCodec.AllIds)
                entries[id] = Encoding.UTF8.GetBytes($"# {id}\nvalue = {(ushort)id}\n");
            entries[SyncFileId.GlobalMultipliers] = ConfigSyncCodec.FormatMultipliers(new Dictionary<string, float> { { "HealthMultiplier", 1.5f } });
            return entries;
        }

        private static byte[] Pack(Dictionary<SyncFileId, byte[]> entries) =>
            ConfigSyncCodec.Pack("2.7.0", entries, out _, out _);

        /// <summary>Hand-built wire around an arbitrary body (to exercise what Pack never produces).</summary>
        private static byte[] BuildWire(byte[] body, ushort format = ConfigSyncCodec.FormatVersion, int? declaredLength = null, byte[] gzipPayload = null)
        {
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(body);
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    w.Write(new[] { (byte)'C', (byte)'D', (byte)'T', (byte)'S' });
                    w.Write(format);
                    w.Write("2.7.0");
                    w.Write(declaredLength ?? body.Length);
                    w.Write(hash);
                }
                byte[] payload = gzipPayload ?? body;
                using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
                    gz.Write(payload, 0, payload.Length);
                return ms.ToArray();
            }
        }

        private static byte[] Body(params (ushort id, byte[] data)[] entries)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((ushort)entries.Length);
                foreach (var (id, data) in entries)
                {
                    w.Write(id);
                    w.Write(data.Length);
                    w.Write(data);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        private static bool Rejected(byte[] wire, string expectContains, out string error)
        {
            bool ok = ConfigSyncCodec.TryUnpack(wire, out var package, out error);
            return !ok && package == null && error != null
                && (expectContains == null || error.IndexOf(expectContains, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ===================================================
        // Tests
        // ===================================================

        private static void Test_RoundTrip_AllIds()
        {
            var entries = SampleEntries();
            byte[] wire = ConfigSyncCodec.Pack("2.7.0", entries, out byte[] packHash, out int bodyBytes);
            bool ok = ConfigSyncCodec.TryUnpack(wire, out var p, out string error);

            bool same = ok && p.Entries.Count == entries.Count;
            if (same)
                foreach (var kvp in entries)
                    same &= p.Entries.TryGetValue(kvp.Key, out var got) && ConfigSyncCodec.BytesEqual(got, kvp.Value);

            Check("RoundTrip_AllIds",
                same && p.HostModVersion == "2.7.0" && ConfigSyncCodec.BytesEqual(p.BodyHash, packHash)
                && p.BodyBytes == bodyBytes && p.WireBytes == wire.Length,
                ok ? $"entries={p.Entries.Count}" : error);
        }

        private static void Test_Hash_IndependentOfInsertionOrder()
        {
            var forward = SampleEntries();
            var reverse = new Dictionary<SyncFileId, byte[]>();
            var ids = new List<SyncFileId>(forward.Keys);
            ids.Reverse();
            foreach (var id in ids) reverse[id] = forward[id];

            Check("Hash_IndependentOfInsertionOrder",
                ConfigSyncCodec.BytesEqual(ConfigSyncCodec.ComputeBodyHash(forward), ConfigSyncCodec.ComputeBodyHash(reverse))
                && ConfigSyncCodec.ShortHash(ConfigSyncCodec.ComputeBodyHash(forward)).Length == 12,
                "hash differs by insertion order");
        }

        private static void Test_Corrupt_FlippedCompressedByte()
        {
            byte[] wire = Pack(SampleEntries());
            wire[wire.Length - 12] ^= 0xFF; // inside the gzip stream / its CRC trailer
            Check("Corrupt_FlippedCompressedByte", Rejected(wire, null, out string e), e);
        }

        private static void Test_Corrupt_FlippedHashByte()
        {
            byte[] wire = Pack(SampleEntries());
            // header: 4 magic + 2 format + 1+5 version + 4 length = 16; hash starts there
            wire[16] ^= 0x01;
            Check("Corrupt_FlippedHashByte", Rejected(wire, "hash", out string e), e);
        }

        private static void Test_Corrupt_Truncated()
        {
            byte[] wire = Pack(SampleEntries());
            byte[] cut = new byte[wire.Length / 2];
            Array.Copy(wire, cut, cut.Length);
            byte[] tiny = { (byte)'C', (byte)'D' };
            Check("Corrupt_Truncated", Rejected(cut, null, out string e1) && Rejected(tiny, null, out string e2), e1);
        }

        private static void Test_Corrupt_WrongMagic()
        {
            byte[] wire = Pack(SampleEntries());
            wire[0] = (byte)'X';
            Check("Corrupt_WrongMagic", Rejected(wire, "not a Crusader DE Tweaker", out string e), e);
        }

        private static void Test_WrongFormatVersion()
        {
            byte[] body = Body((1, new byte[] { 1 }));
            Check("WrongFormatVersion", Rejected(BuildWire(body, format: 2), "format v2", out string e), e);
        }

        private static void Test_Oversize_Wire()
        {
            byte[] wire = new byte[ConfigSyncCodec.MaxWireBytes + 1];
            Check("Oversize_Wire", Rejected(wire, "limit", out string e), e);
        }

        private static void Test_Oversize_DeclaredBody()
        {
            byte[] body = Body((1, new byte[] { 1 }));
            Check("Oversize_DeclaredBody", Rejected(BuildWire(body, declaredLength: ConfigSyncCodec.MaxBodyBytes + 1), "out of range", out string e), e);
        }

        private static void Test_Oversize_PackRefuses()
        {
            // 300 KB of incompressible bytes cannot fit in 200,000 wire bytes.
            var rng = new Random(12345);
            byte[] noise = new byte[300 * 1024];
            rng.NextBytes(noise);
            bool threw = false;
            try { ConfigSyncCodec.Pack("2.7.0", new Dictionary<SyncFileId, byte[]> { { SyncFileId.MeleeDamage, noise } }, out _, out _); }
            catch (InvalidOperationException) { threw = true; }
            Check("Oversize_PackRefuses", threw, "Pack accepted an oversize package");
        }

        private static void Test_ContentLongerThanDeclared()
        {
            // gzip bomb shape: header declares a small body, the stream inflates to much more.
            byte[] body = Body((1, new byte[] { 1 }));
            byte[] bigger = new byte[body.Length + 100000];
            Array.Copy(body, bigger, body.Length);
            Check("ContentLongerThanDeclared", Rejected(BuildWire(body, gzipPayload: bigger), "longer than declared", out string e1)
                && Rejected(BuildWire(bigger, declaredLength: bigger.Length + 10, gzipPayload: bigger), "shorter than declared", out string e2), e1);
        }

        private static void Test_Whitelist_UnknownIdRejected()
        {
            byte[] body = Body((999, Encoding.UTF8.GetBytes("..\\..\\evil")));
            Check("Whitelist_UnknownIdRejected", Rejected(BuildWire(body), "unknown file id", out string e), e);
        }

        private static void Test_Whitelist_DuplicateIdRejected()
        {
            byte[] body = Body((1, new byte[] { 1 }), (1, new byte[] { 2 }));
            Check("Whitelist_DuplicateIdRejected", Rejected(BuildWire(body), "twice", out string e), e);
        }

        private static void Test_Whitelist_PathsAreFixedAndRelative()
        {
            bool ok = true;
            string bad = null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ConfigSyncCodec.AllIds)
            {
                string p = ConfigSyncCodec.RelativePathFor(id);
                bool safe = !string.IsNullOrEmpty(p) && !Path.IsPathRooted(p) && p.IndexOf("..", StringComparison.Ordinal) < 0
                    && p.IndexOf(':') < 0 && p.IndexOfAny(Path.GetInvalidPathChars()) < 0 && seen.Add(p);
                if (!safe) { ok = false; bad = p; }
            }

            bool unknownThrows = false;
            try { ConfigSyncCodec.RelativePathFor((SyncFileId)999); }
            catch (ArgumentOutOfRangeException) { unknownThrows = true; }

            Check("Whitelist_PathsAreFixedAndRelative", ok && unknownThrows && !ConfigSyncCodec.IsKnown((SyncFileId)999),
                bad ?? "unknown id did not throw");
        }

        private static void Test_Empty_NoFiles()
        {
            bool threw = false;
            try { ConfigSyncCodec.Pack("2.7.0", new Dictionary<SyncFileId, byte[]>(), out _, out _); }
            catch (ArgumentException) { threw = true; }
            Check("Empty_NoFiles", threw && Rejected(BuildWire(Body()), "no files", out string e), "empty package accepted");
        }

        private static void Test_Empty_ZeroLengthEntry()
        {
            bool threw = false;
            try { ConfigSyncCodec.Pack("2.7.0", new Dictionary<SyncFileId, byte[]> { { SyncFileId.Units, new byte[0] } }, out _, out _); }
            catch (ArgumentException) { threw = true; }
            Check("Empty_ZeroLengthEntry", threw && Rejected(BuildWire(Body((1, new byte[0]))), "empty", out string e), "zero-length entry accepted");
        }

        private static void Test_Empty_NullOrEmptyWire()
        {
            Check("Empty_NullOrEmptyWire", Rejected(null, "empty package", out string e1) && Rejected(new byte[0], "empty package", out string e2), e1);
        }

        private static void Test_TrailingBytesRejected()
        {
            byte[] body = Body((1, new byte[] { 1 }));
            byte[] withTail = new byte[body.Length + 3];
            Array.Copy(body, withTail, body.Length);
            Check("TrailingBytesRejected", Rejected(BuildWire(withTail), "after the last file", out string e), e);
        }

        private static void Test_VersionRule()
        {
            Check("VersionRule",
                ConfigSyncCodec.CompareVersions("2.7.0", "2.7.0") == ConfigSyncCodec.VersionVerdict.Same
                && ConfigSyncCodec.CompareVersions("2.7.1", "2.7.0") == ConfigSyncCodec.VersionVerdict.PatchDiffers
                && ConfigSyncCodec.CompareVersions("2.8.0", "2.7.0") == ConfigSyncCodec.VersionVerdict.Incompatible
                && ConfigSyncCodec.CompareVersions("3.7.0", "2.7.0") == ConfigSyncCodec.VersionVerdict.Incompatible
                && ConfigSyncCodec.CompareVersions("garbage", "2.7.0") == ConfigSyncCodec.VersionVerdict.Incompatible,
                "version verdicts wrong");
        }

        private static void Test_Multipliers_RoundTrip()
        {
            var values = new Dictionary<string, float> { { "HealthMultiplier", 1.25f }, { "LowWallCostMultiplier", 0.1f }, { "A_0", 0f } };
            byte[] text = ConfigSyncCodec.FormatMultipliers(values);
            bool ok = ConfigSyncCodec.TryParseMultipliers(text, out var parsed, out string error);
            bool same = ok && parsed.Count == values.Count;
            if (same)
                foreach (var kvp in values)
                    same &= parsed.TryGetValue(kvp.Key, out float v) && v == kvp.Value; // exact: "R" round-trips
            Check("Multipliers_RoundTrip", same, error);
        }

        private static void Test_Multipliers_Rejects()
        {
            bool Bad(string s) => !ConfigSyncCodec.TryParseMultipliers(Encoding.UTF8.GetBytes(s), out _, out _);
            Check("Multipliers_Rejects",
                Bad("") && Bad("Health=NaN\n") && Bad("Health=Infinity\n") && Bad("Health=-1\n")
                && Bad("Health=1\nHealth=2\n") && Bad("../x=1\n") && Bad("NoEquals\n") && Bad("Health=1,5\n"),
                "an invalid multiplier list was accepted");
        }

        private static void Test_Baseline_FirstWriteWinsAndRestoreOrder()
        {
            var journal = new BaselineJournal();
            var restoredOrder = new List<string>();
            int cell = 10; // "game value"

            journal.BeforeWrite("a", () => { int original = cell; return () => { cell = original; restoredOrder.Add("a"); }; });
            cell = 20; // first mod write
            journal.BeforeWrite("a", () => { int original = cell; return () => { cell = original; restoredOrder.Add("a2"); }; });
            cell = 30; // second mod write of the same cell must not replace the remembered 10
            journal.BeforeWrite("b", () => () => restoredOrder.Add("b"));
            journal.BeforeWrite("unreadable", () => null);

            var (restored, failed) = journal.RestoreAll();
            Check("Baseline_FirstWriteWinsAndRestoreOrder",
                cell == 10 && restored == 2 && failed == 0 && journal.Count == 2 && journal.UnreadableCount == 1
                && restoredOrder.Count == 2 && restoredOrder[0] == "b" && restoredOrder[1] == "a",
                $"cell={cell} restored={restored} order={string.Join(",", restoredOrder)}");
        }

        // ===================================================
        // Assertion
        // ===================================================

        private static void Check(string testName, bool condition, string failureDetail)
        {
            if (condition)
            {
                _passedCount++;
                Plugin.Logger.LogDebug($"    [PASS] {testName}");
            }
            else
            {
                _failedCount++;
                Plugin.Logger.LogError($"    [FAIL] {testName}: {failureDetail}");
            }
        }
    }
}
