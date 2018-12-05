using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Serialization
    {
        public static void WriteDeclaredSymbols(
            string projectDestinationFolder,
            IEnumerable<string> lines)
        {
            var fileName = Path.Combine(projectDestinationFolder, Constants.DeclaredSymbolsFileName + ".txt");
            File.AppendAllLines(fileName, lines, Encoding.UTF8);
        }

        public static string GetIconForExtension(string document)
        {
            switch (Path.GetExtension(document).ToLowerInvariant())
            {
                case ".cs":
                    return "196";
                case ".vb":
                    return "195";
                case ".ts":
                    return "228";
                default:
                    return "227";
            }
        }

        public static void ParseDeclaredSymbol(string separated, DeclaredSymbolInfo declaredSymbolInfo)
        {
            ushort glyph = ushort.MaxValue; // to save space and avoid extra field, this indicates an invalid symbol

            var parts = separated.Split(';');
            if (parts.Length == 5)
            {
                declaredSymbolInfo.Name = string.Intern(parts[0]);
                declaredSymbolInfo.ID = HexStringToULong(parts[1]);
                declaredSymbolInfo.Kind = string.Intern(parts[2]);
                declaredSymbolInfo.Description = parts[3];
                ushort.TryParse(parts[4], out glyph);
            }

            declaredSymbolInfo.Glyph = glyph;
        }

        public static void WriteDeclaredSymbol(BinaryWriter writer, DeclaredSymbolInfo symbol, Huffman huffman)
        {
            Write7BitEncodedInt(writer, symbol.AssemblyNumber);
            writer.Write(symbol.Name);
            writer.Write(symbol.ID);
            WriteBytes(writer, huffman.Compress(symbol.Description));
            Write7BitEncodedInt(writer, symbol.Glyph);
        }

        public static string ByteArrayToHexString(byte[] bytes, int digits = 0)
        {
            if (digits == 0)
            {
                digits = bytes.Length * 2;
            }

            char[] c = new char[digits];
            byte b;
            for (int i = 0; i < digits / 2; i++)
            {
                b = ((byte)(bytes[i] >> 4));
                c[i * 2] = (char)(b > 9 ? b + 87 : b + 0x30);
                b = ((byte)(bytes[i] & 0xF));
                c[(i * 2) + 1] = (char)(b > 9 ? b + 87 : b + 0x30);
            }

            return new string(c);
        }

        public static ulong HexStringToULong(string hex)
        {
            ulong result = 0;
            for (int i = 0; i < 8; i++)
            {
                ulong b = GetHexVal(hex[i * 2]);
                result = result | (b << ((i * 8) + 4));
                b = GetHexVal(hex[(i * 2) + 1]);
                result = result | (b << (i * 8));
            }

            return result;
        }

        public static string ULongToHexString(ulong number)
        {
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                int shift = 8 * i + 4;
                int b = (byte)((number & ((ulong)15 << shift)) >> shift);
                sb.Append(Convert.ToString(b, 16));
                shift = 8 * i;
                b = (byte)((number & ((ulong)15 << shift)) >> shift);
                sb.Append(Convert.ToString(b, 16));
            }

            return sb.ToString();
        }

        public static byte[] HexStringToByteArray(string hex)
        {
            byte[] result = new byte[hex.Length >> 1];

            for (int i = 0; i < (hex.Length >> 1); i++)
            {
                result[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return result;
        }

        public static byte GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            return (byte)(val - (val < 58 ? 48 : 87));
            //Or the two combined, but a bit slower:
            //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        public static string ReadValue(IEnumerable<string> lines, string name)
        {
            name += "=";
            var line = lines.FirstOrDefault(l => l.StartsWith(name));
            if (line == null)
            {
                return string.Empty;
            }

            return line.Substring(name.Length);
        }

        public static long ReadLong(IEnumerable<string> lines, string name)
        {
            string value = ReadValue(lines, name);
            long result = 0;
            long.TryParse(value, out result);
            return result;
        }

        public static void WriteBytes(BinaryWriter writer, byte[] array)
        {
            Serialization.Write7BitEncodedInt(writer, array.Length);
            writer.Write(array);
        }

        public static void WriteDeclaredSymbols(List<DeclaredSymbolInfo> declaredSymbols, string outputPath)
        {
            if (declaredSymbols.Count == 0)
            {
                return;
            }

            using (Measure.Time("Writing declared symbols"))
            {
                string masterIndexFile = Path.Combine(outputPath, Constants.MasterIndexFileName);
                string huffmanFile = Path.Combine(outputPath, Constants.HuffmanFileName);

                using (Measure.Time("Sorting symbols"))
                {
                    SymbolSorter.SortSymbols(declaredSymbols);
                }

                Huffman huffman = null;
                using (Measure.Time("Creating Huffman tables"))
                {
                    huffman = Huffman.Create(declaredSymbols.Select(d => d.Description));
                    huffman.Write(huffmanFile);
                }

                using (Measure.Time("Writing declared symbols to disk..."))
                using (var fileStream = new FileStream(
                    masterIndexFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    262144,
                    FileOptions.SequentialScan))
                using (var writer = new BinaryWriter(fileStream))
                {
                    writer.Write(declaredSymbols.Count);
                    foreach (var declaredSymbol in declaredSymbols)
                    {
                        Serialization.WriteDeclaredSymbol(writer, declaredSymbol, huffman);
                    }
                }
            }
        }

        public static byte[] ReadNativeBytes(IntPtr pointer, int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Marshal.ReadByte(pointer, i);
            }

            return result;
        }

        public static void Write7BitEncodedInt(BinaryWriter writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }

            writer.Write((byte)v);
        }

        public static void WriteProjectMap(
            string outputPath,
            IEnumerable<Tuple<string, string>> listOfAssemblyNamesAndProjects,
            IDictionary<string, int> referencingAssembliesCount)
        {
            IEnumerable<Tuple<string, int>> assemblies;
            IEnumerable<string> projects;
            using (Measure.Time("Normalizing..."))
            {
                Normalize(listOfAssemblyNamesAndProjects, out assemblies, out projects);
            }

            using (Measure.Time("Writing project map"))
            {
                string masterAssemblyMap = Path.Combine(outputPath, Constants.MasterAssemblyMap + ".txt");
                File.WriteAllLines(
                    masterAssemblyMap,
                    assemblies.Select(
                        t => t.Item1 + ";" +
                        t.Item2.ToString() + ";" +
                        (referencingAssembliesCount.ContainsKey(t.Item1) ? referencingAssembliesCount[t.Item1] : 0)),
                    Encoding.UTF8);

                string masterProjectMap = Path.Combine(outputPath, Constants.MasterProjectMap + ".txt");
                File.WriteAllLines(
                    masterProjectMap,
                    projects,
                    Encoding.UTF8);
            }
        }

        public static void Normalize(
            IEnumerable<Tuple<string, string>> listOfAssemblyNamesAndProjects,
            out IEnumerable<Tuple<string, int>> assemblies,
            out IEnumerable<string> projects)
        {
            listOfAssemblyNamesAndProjects = listOfAssemblyNamesAndProjects
                .OrderBy(t => t.Item1, StringComparer.OrdinalIgnoreCase);

            var projectList = listOfAssemblyNamesAndProjects
                .Select((t, i) => Tuple.Create(t.Item2, i))
                .Where(t => !string.IsNullOrEmpty(t.Item1))
                .OrderBy(t => t.Item1, StringComparer.OrdinalIgnoreCase);

            var assemblyList = listOfAssemblyNamesAndProjects
                .Select((t, i) => Tuple.Create(t.Item1, -1))
                .ToArray();
            int j = 0;
            foreach (var index in projectList.Select(t => t.Item2))
            {
                assemblyList[index] = Tuple.Create(assemblyList[index].Item1, j++);
            }

            assemblies = assemblyList;
            projects = projectList.Select(t => t.Item1);
        }
    }
}
