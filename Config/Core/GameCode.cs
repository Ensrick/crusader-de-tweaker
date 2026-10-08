// Config/Core/GameCode.cs
//
// PURPOSE: Finds code and data addresses in the game's native module (CrusaderDE.dll) by byte pattern, for the
//          features that read or write game memory the Script Extender does not expose (skirmish starting troops)
//          and for the self-test (worker yield function).
//
// HOW: patterns are matched in the .text section of CrusaderDE.dll as read FROM DISK (the Script Extender's detours
//      have replaced function entries in memory); a hit's RVA is then valid for the loaded module. A pattern must
//      match exactly once, or nothing is returned.
//
// IMPORTANT FOR AI AGENTS:
// - Callers must still check what they found (EngageDistancePatch checks the bytes in memory; SkirmishStartingTroops
//   checks the game's own table rows) before writing anything.
// - Reads only: this class never writes game memory.
//
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CrusaderDETweaker.Config.Core
{
    internal static class GameCode
    {
        internal const string ModuleName = "CrusaderDE.dll";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder fileName, int size);

        /// <summary>Base address of the loaded game module, or zero.</summary>
        internal static IntPtr ModuleBase => GetModuleHandleW(ModuleName);

        /// <summary>The DLL file of the loaded game module (about 3.5 MB; read it once per lookup and drop it).</summary>
        internal static byte[] ReadModuleFile()
        {
            IntPtr module = ModuleBase;
            if (module == IntPtr.Zero) throw new InvalidOperationException(ModuleName + " is not loaded");
            var path = new StringBuilder(1024);
            if (GetModuleFileNameW(module, path, path.Capacity) == 0) throw new IOException(ModuleName + " path not found");
            return File.ReadAllBytes(path.ToString());
        }

        /// <summary>
        /// The single RVA in .text where <paramref name="pattern"/> ("4C 8D 1D ? ? ? ? 49 2B C4") matches
        /// <paramref name="file"/>; false (with the reason) when it matches 0 or 2+ times.
        /// </summary>
        internal static bool TryFindUnique(byte[] file, string pattern, out uint rva, out string problem)
        {
            rva = 0;
            problem = null;
            GetTextSection(file, out uint textRva, out int textOffset, out int textSize);
            ParsePattern(pattern, out byte[] bytes, out bool[] wild);

            int hits = 0;
            for (int i = 0; i + bytes.Length <= textSize; i++)
            {
                int k = 0;
                while (k < bytes.Length && (wild[k] || file[textOffset + i + k] == bytes[k])) k++;
                if (k != bytes.Length) continue;
                if (++hits == 1) rva = textRva + (uint)i;
            }
            if (hits == 1) return true;
            problem = $"code pattern '{pattern}' found {hits} times in {ModuleName} (expected once; a game update?)";
            rva = 0;
            return false;
        }

        /// <summary><see cref="TryFindUnique(byte[], string, out uint, out string)"/> on the module file read from disk.</summary>
        internal static bool TryFindUnique(string pattern, out uint rva, out string problem)
        {
            byte[] file;
            try { file = ReadModuleFile(); }
            catch (Exception ex) { rva = 0; problem = $"{ModuleName} could not be read ({ex.Message})"; return false; }
            return TryFindUnique(file, pattern, out rva, out problem);
        }

        /// <summary>Signed 32-bit value at RVA <paramref name="rva"/> (inside .text) of the DLL file: an instruction operand.</summary>
        internal static int ReadFileInt32(byte[] file, uint rva)
        {
            GetTextSection(file, out uint textRva, out int textOffset, out _);
            return BitConverter.ToInt32(file, textOffset + (int)(rva - textRva));
        }

        private static void GetTextSection(byte[] file, out uint rva, out int offset, out int size)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int sections = BitConverter.ToUInt16(file, pe + 6);
            int optional = BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < sections; i++)
            {
                int s = pe + 24 + optional + i * 40;
                if (Encoding.ASCII.GetString(file, s, 8).TrimEnd('\0') != ".text") continue;
                rva = BitConverter.ToUInt32(file, s + 12);
                size = BitConverter.ToInt32(file, s + 16);
                offset = BitConverter.ToInt32(file, s + 20);
                return;
            }
            throw new InvalidDataException(ModuleName + " has no .text section");
        }

        private static void ParsePattern(string pattern, out byte[] bytes, out bool[] wild)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bytes = new byte[tokens.Length];
            wild = new bool[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].StartsWith("?")) wild[i] = true;
                else bytes[i] = Convert.ToByte(tokens[i], 16);
            }
        }
    }
}
