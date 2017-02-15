using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public class Serialization
    {
        public static IEnumerable<AssemblyInfo> ReadAssemblies(string rootPath)
        {
            var assemblyInfoFile = Path.Combine(rootPath, Constants.MasterAssemblyMap + ".txt");
            var result = File.ReadLines(assemblyInfoFile)
                .Select(l => new AssemblyInfo(l));
            return result;
        }

        public static IEnumerable<string> ReadProjects(string folderPath)
        {
            var projectInfoFile = Path.Combine(folderPath, Constants.MasterProjectMap + ".txt");
            var result = File.ReadLines(projectInfoFile);
            return result;
        }

        public static IndexEntry ReadDeclaredSymbol(BinaryReader reader)
        {
            var symbol = new IndexEntry();
            symbol.AssemblyNumber = (ushort)Read7BitEncodedInt(reader);
            symbol.Name = reader.ReadString();
            symbol.ID = BitConverter.ToUInt64(reader.ReadBytes(8), 0);
            symbol.Description = WriteNativeBytes(ReadBytes(reader));
            symbol.Glyph = (ushort)Read7BitEncodedInt(reader);
            return symbol;
        }

        public static IntPtr WriteNativeBytes(byte[] bytes)
        {
            IntPtr result = Marshal.AllocHGlobal(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                Marshal.WriteByte(result, i, bytes[i]);
            }

            return result;
        }

        public static byte[] ReadBytes(BinaryReader reader)
        {
            var numberOfBytes = Serialization.Read7BitEncodedInt(reader);
            var bytes = reader.ReadBytes(numberOfBytes);
            return bytes;
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

        public static int Read7BitEncodedInt(BinaryReader reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadByte handles end of stream cases for us.
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return count;
        }
    }
}