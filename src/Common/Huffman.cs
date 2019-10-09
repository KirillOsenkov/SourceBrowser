using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.SourceBrowser.Common
{
    public sealed class Huffman
    {
        public const byte MinChar = 32;
        public const byte MaxChar = 126;
        public const byte CharRange = EndChar - MinChar + 1;
        public const byte EndChar = MaxChar + 1;
        public const byte EndCharIndex = EndChar - MinChar;

        [ThreadStatic]
        private static List<bool> reusableBitList;

        public Node root;
        public List<bool>[,] table;

        private Huffman(Node root)
        {
            this.root = root;
            CreateTable();
        }

        private void CreateTable()
        {
            table = new List<bool>[CharRange, CharRange];
            root.FillTable(table, new List<bool>());
        }

        public static Huffman Create(IEnumerable<string> words)
        {
            uint[,] frequencies = new uint[CharRange, CharRange];

            foreach (var word in words)
            {
                RecordFrequencies(frequencies, word);
            }

            var nodes = new List<Node>();
            for (byte from = MinChar; from <= EndChar; from++)
            {
                for (byte to = MinChar; to <= EndChar; to++)
                {
                    uint frequency = frequencies[from - MinChar, to - MinChar];
                    if (frequency > 0)
                    {
                        nodes.Add(new Node(frequency, from, to));
                    }
                }
            }

            ////var final = sorted.OrderBy(t => t.frequency);
            ////File.WriteAllLines("E:\\1.txt", final.Select(t => t.Item2 + "," + t.Item1));

            Node root = CreateHuffmanTree(nodes);

            var huffman = new Huffman(root);
            return huffman;
        }

        private static void RecordFrequencies(uint[,] frequencies, string word)
        {
            if (!IsAscii(word))
            {
                return;
            }

            int length = word.Length;
            for (int i = 0; i < length; i += 2)
            {
                int leftIndex = ToIndex(word[i]);
                int rightIndex = EndCharIndex;
                if ((i + 1) < length)
                {
                    rightIndex = ToIndex(word[i + 1]);
                }

                frequencies[leftIndex, rightIndex]++;
            }

            if (length % 2 == 0)
            {
                frequencies[EndCharIndex, EndCharIndex]++;
            }
        }

        private static Node CreateHuffmanTree(IEnumerable<Node> nodes)
        {
            var queue = new PriorityQueue<Node>(
                nodes.Count(),
                Node.Comparer);
            queue.PushRange(nodes);

            while (queue.Count > 1)
            {
                var node1 = queue.Pop();
                var node2 = queue.Pop();
                Node newNode = new Node(node1, node2);
                queue.Push(newNode);
            }

            return queue.Top;
        }

        public byte[] Compress(string text)
        {
            if (!IsAscii(text))
            {
                byte[] result = new byte[Encoding.UTF8.GetByteCount(text) + 1];

                // "header" of Unicode string - starts with bit 1
                result[0] = byte.MaxValue;
                Encoding.UTF8.GetBytes(text, 0, text.Length, result, 1);
                return result;
            }

            if (reusableBitList == null)
            {
                reusableBitList = new List<bool>((text.Length * 8) + 1);
            }
            else
            {
                reusableBitList.Clear();
            }

            // "header" of Huffman compressed string - starts with bit 0
            reusableBitList.Add(false);

            for (int i = 0; i < text.Length; i += 2)
            {
                char ch1 = text[i];
                char ch2 = (i + 1) < text.Length ? text[i + 1] : (char)EndChar;

                byte byte1 = ToIndex(ch1);
                byte byte2 = ToIndex(ch2);

                var encoding = table[byte1, byte2];
                reusableBitList.AddRange(encoding);
            }

            if (text.Length % 2 == 0)
            {
                reusableBitList.AddRange(table[EndCharIndex, EndCharIndex]);
            }

            return ToByteArray(reusableBitList.ToArray());
        }

        public IntPtr CompressToNative(string text)
        {
            byte[] bytes = Compress(text);
            IntPtr result = Marshal.AllocHGlobal(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                Marshal.WriteByte(result, i, bytes[i]);
            }

            return result;
        }

        public string Uncompress(byte[] bytes)
        {
            var sb = new StringBuilder();
            var current = root;
            var bitArray = new BitArray(bytes);

            // get the "header" and see if it's a Unicode string
            if (bitArray[0])
            {
                return Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1);
            }

            for (int i = 1; i < bitArray.Length; i++)
            {
                bool bit = bitArray[i];
                if (bit)
                {
                    current = current.right;
                }
                else
                {
                    current = current.left;
                }

                if (current.IsLeaf)
                {
                    if (current.from == EndChar)
                    {
                        return sb.ToString();
                    }

                    sb.Append((char)current.from);

                    if (current.to == EndChar)
                    {
                        return sb.ToString();
                    }

                    sb.Append((char)current.to);
                    current = root;
                }
            }

            return sb.ToString();
        }

        public IEnumerable<bool> ReadBits(IntPtr native)
        {
            for (int offset = 0; ; offset++)
            {
                byte currentByte = Marshal.ReadByte(native, offset);
                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    bool bit = (currentByte & (1 << bitIndex)) != 0;
                    yield return bit;
                }
            }
        }

        public string Uncompress(IntPtr nativeBytes)
        {
            var sb = new StringBuilder();
            var current = root;

            bool firstBitSet = (Marshal.ReadByte(nativeBytes) & 1) == 1;
            if (firstBitSet)
            {
                return Marshal.PtrToStringUni(nativeBytes + 1);
            }

            foreach (bool bit in ReadBits(nativeBytes).Skip(1))
            {
                if (bit)
                {
                    current = current.right;
                }
                else
                {
                    current = current.left;
                }

                if (current.IsLeaf)
                {
                    if (current.from == EndChar)
                    {
                        return sb.ToString();
                    }

                    sb.Append((char)current.from);

                    if (current.to == EndChar)
                    {
                        return sb.ToString();
                    }

                    sb.Append((char)current.to);
                    current = root;
                }
            }

            return sb.ToString();
        }

        public static byte ToIndex(char ch)
        {
            return (byte)(ch - MinChar);
        }

        public static bool IsAscii(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                int ch = str[i];
                if (ch < 32 || ch > 126)
                {
                    return false;
                }
            }

            return true;
        }

        private byte[] ToByteArray(bool[] bits)
        {
            ICollection array = new BitArray(bits);
            byte[] result = new byte[(int)Math.Ceiling(array.Count / 8.0)];
            array.CopyTo(result, 0);
            return result;
        }

        public class Node
        {
            public static readonly HuffmanNodeComparer Comparer = new HuffmanNodeComparer();

            public uint frequency;
            public byte from = byte.MaxValue;
            public byte to = byte.MaxValue;
            public Node left;
            public Node right;

            public Node(uint frequency, byte from, byte to)
            {
                this.frequency = frequency;
                this.from = from;
                this.to = to;
            }

            public Node(Node left, Node right)
            {
                this.frequency = left.frequency + right.frequency;
                this.left = left;
                this.right = right;
            }

            public class HuffmanNodeComparer : IComparer<Node>
            {
                public int Compare(Node x, Node y)
                {
                    return x.frequency.CompareTo(y.frequency);
                }
            }

            public bool IsLeaf
            {
                get
                {
                    return from != byte.MaxValue && to != byte.MaxValue;
                }
            }

            public void FillTable(List<bool>[,] table, List<bool> bitVector)
            {
                if (this.left != null)
                {
                    bitVector.Add(false);
                    this.left.FillTable(table, bitVector);
                    bitVector.RemoveAt(bitVector.Count - 1);
                }

                if (this.right != null)
                {
                    bitVector.Add(true);
                    this.right.FillTable(table, bitVector);
                    bitVector.RemoveAt(bitVector.Count - 1);
                }

                if (from != byte.MaxValue && to != byte.MaxValue)
                {
                    table[from - Huffman.MinChar, to - Huffman.MinChar] = new List<bool>(bitVector);
                }
            }
        }

        public static Huffman Read(Stream huffmanStream)
        {
            using (var reader = new BinaryReader(huffmanStream))
            {
                var node = ReadNode(reader);
                var result = new Huffman(node);
                return result;
            }
        }

        private static Node ReadNode(BinaryReader reader)
        {
            var stack = new Stack<Node>();
            while (true)
            {
                int readByte = reader.BaseStream.ReadByte();
                if (readByte == -1)
                {
                    return stack.Pop();
                }

                byte b = (byte)readByte;
                if (b == byte.MaxValue)
                {
                    if (stack.Count > 1)
                    {
                        var right = stack.Pop();
                        var left = stack.Pop();
                        Node newNode = new Node(left, right);
                        stack.Push(newNode);
                    }
                    else
                    {
                        return stack.Pop();
                    }
                }
                else
                {
                    byte to = reader.ReadByte();
                    Node newNode = new Node(0, b, to);
                    stack.Push(newNode);
                }
            }
        }

        public void Write(string huffmanFile)
        {
            using (var fileStream = new FileStream(
                huffmanFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                262144,
                FileOptions.SequentialScan))
            using (var writer = new BinaryWriter(fileStream))
            {
                Write(writer, this.root);
            }
        }

        private void Write(BinaryWriter writer, Node node)
        {
            if (node.IsLeaf)
            {
                writer.Write(node.from);
                writer.Write(node.to);
            }
            else
            {
                Write(writer, node.left);
                Write(writer, node.right);
                writer.Write(byte.MaxValue);
            }
        }
    }
}