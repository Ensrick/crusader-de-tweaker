// Tests/CsvHelperTest.cs
//
// PURPOSE: Unit tests for CsvHelper.ParseMatrix (the damage-matrix CSV parser).
//
// TESTS COVER:
// - Well-formed matrix parsing (headers, row labels, values)
// - Spreadsheet round-trip artifacts: comma-only "empty record" lines, UTF-8 BOM,
//   trailing empty header cells (the 2026-09-07 Nexus report: a comma-only line under
//   the comment block was taken as the header row and the real header became a -1 row)
// - Refusal of a header with an empty column name (so a broken file is never written back)
// - Rows with an empty defender name are skipped
// - Unparseable / missing cells read as -1 (skip), never 0
//
// NOTE: pure parsing only - no file I/O, no game API. Runs on every launch via CoreTestRunner.
//
using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;

namespace CrusaderDETweaker.Tests
{
    /// <summary>
    /// Unit tests for CsvHelper.ParseMatrix.
    /// </summary>
    internal static class CsvHelperTest
    {
        private static int _passedCount;
        private static int _failedCount;

        public static (bool passed, int testCount) RunTests()
        {
            Plugin.Logger.LogInfo("--- CsvHelper Test Suite ---");

            _passedCount = 0;
            _failedCount = 0;

            Test_Parse_WellFormed();
            Test_Parse_SkipsSpreadsheetEmptyRecordLine();
            Test_Parse_StripsBom();
            Test_Parse_TrimsTrailingEmptyHeaderCells();
            Test_Parse_EmptyHeaderNameThrows();
            Test_Parse_SkipsRowWithEmptyDefenderName();
            Test_Parse_UnparseableCellIsMinusOne();
            Test_Parse_ShortRowPadsWithMinusOne();

            int totalTests = _passedCount + _failedCount;
            bool allPassed = _failedCount == 0;

            Plugin.Logger.LogInfo($"  CsvHelper: {_passedCount}/{totalTests} tests passed");

            return (allPassed, totalTests);
        }

        // ===================================================
        // Tests
        // ===================================================

        private static void Test_Parse_WellFormed()
        {
            var (rows, cols, m) = CsvHelper.ParseMatrix(new[]
            {
                "# Crusader DE Tweaker - Ranged Damage Matrix",
                "#",
                "",
                "                               , Arrow , Bolt ",
                "CHIMP_TYPE_ARAB_ASSASIN        , 1500  , 8000 ",
                "CHIMP_TYPE_ARAB_BALLISTA       , 500   , 500  ",
            });

            Check("Parse_WellFormed",
                cols.Length == 2 && cols[0] == "Arrow" && cols[1] == "Bolt"
                && rows.Length == 2 && rows[1] == "CHIMP_TYPE_ARAB_BALLISTA"
                && m[0, 1] == 8000 && m[1, 0] == 500,
                $"cols={cols.Length} rows={rows.Length}");
        }

        private static void Test_Parse_SkipsSpreadsheetEmptyRecordLine()
        {
            // Exactly the reported file: Excel / Sheets saved the blank line under the comments
            // as a line of commas. It must not be taken as the header row.
            var (rows, cols, m) = CsvHelper.ParseMatrix(new[]
            {
                "# ========================================",
                "                               ,       ,      ,      ,      ",
                "                               , Arrow , Bolt , Slinger, Javelin",
                "CHIMP_TYPE_ARAB_ASSASIN        , 1500  , 8000 , 500    , 2500   ",
            });

            Check("Parse_SkipsSpreadsheetEmptyRecordLine",
                cols.Length == 4 && cols[0] == "Arrow" && cols[3] == "Javelin"
                && rows.Length == 1 && rows[0] == "CHIMP_TYPE_ARAB_ASSASIN"
                && m[0, 0] == 1500 && m[0, 3] == 2500,
                $"cols={cols.Length} rows={rows.Length} col0='{(cols.Length > 0 ? cols[0] : "")}'");
        }

        private static void Test_Parse_StripsBom()
        {
            var (rows, cols, m) = CsvHelper.ParseMatrix(new[]
            {
                "\uFEFF# comment line carrying a byte-order mark",
                " , Arrow",
                "CHIMP_TYPE_ARCHER, 1",
            });

            Check("Parse_StripsBom",
                cols.Length == 1 && cols[0] == "Arrow" && rows.Length == 1 && m[0, 0] == 1,
                $"cols={cols.Length} rows={rows.Length}");
        }

        private static void Test_Parse_TrimsTrailingEmptyHeaderCells()
        {
            var (rows, cols, m) = CsvHelper.ParseMatrix(new[]
            {
                " , Arrow, Bolt, ,",
                "CHIMP_TYPE_ARCHER, 1, 2, ,",
            });

            Check("Parse_TrimsTrailingEmptyHeaderCells",
                cols.Length == 2 && cols[1] == "Bolt" && m.GetLength(1) == 2 && m[0, 1] == 2,
                $"cols={cols.Length}");
        }

        private static void Test_Parse_EmptyHeaderNameThrows()
        {
            bool threw = false;
            try
            {
                CsvHelper.ParseMatrix(new[]
                {
                    " , Arrow, , Bolt",
                    "CHIMP_TYPE_ARCHER, 1, 2, 3",
                });
            }
            catch (FormatException)
            {
                threw = true;
            }
            catch (Exception)
            {
                // wrong exception type - counts as a failure below
            }

            Check("Parse_EmptyHeaderNameThrows", threw);
        }

        private static void Test_Parse_SkipsRowWithEmptyDefenderName()
        {
            var (rows, cols, m) = CsvHelper.ParseMatrix(new[]
            {
                " , Arrow, Bolt",
                " , -1, -1",
                "CHIMP_TYPE_ARCHER, 1, 2",
            });

            Check("Parse_SkipsRowWithEmptyDefenderName",
                rows.Length == 1 && rows[0] == "CHIMP_TYPE_ARCHER" && m.GetLength(0) == 1 && m[0, 0] == 1,
                $"rows={rows.Length}");
        }

        private static void Test_Parse_UnparseableCellIsMinusOne()
        {
            var (rows, cols, m) = CsvHelper.ParseMatrix(new[]
            {
                " , Arrow, Bolt",
                "CHIMP_TYPE_ARCHER, abc, 5",
            });

            Check("Parse_UnparseableCellIsMinusOne", m[0, 0] == -1 && m[0, 1] == 5, $"m00={m[0, 0]}");
        }

        private static void Test_Parse_ShortRowPadsWithMinusOne()
        {
            var (rows, cols, m) = CsvHelper.ParseMatrix(new[]
            {
                " , Arrow, Bolt",
                "CHIMP_TYPE_ARCHER, 7",
            });

            Check("Parse_ShortRowPadsWithMinusOne", m[0, 0] == 7 && m[0, 1] == -1, $"m01={m[0, 1]}");
        }

        // ===================================================
        // Helpers
        // ===================================================

        private static void Check(string name, bool condition, string detail = null)
        {
            if (condition)
            {
                _passedCount++;
                Plugin.Logger.LogDebug($"    [PASS] {name}");
            }
            else
            {
                _failedCount++;
                Plugin.Logger.LogError($"    [FAIL] {name}{(detail != null ? ": " + detail : "")}");
            }
        }
    }
}
