using System;
using System.Collections.Generic;
using System.IO;

namespace ReFrontier.Jpk
{
    /// <summary>
    /// Huffman + LZ77 compression encoder for Monster Hunter Frontier JPK files (type 4: HFI).
    ///
    /// <para><b>Algorithm Overview:</b></para>
    /// <para>Combines Huffman coding with LZ77 compression. First applies LZ77 to find
    /// repeated sequences, then encodes the output bytes using a Huffman tree for
    /// additional compression of frequently-occurring byte values.</para>
    ///
    /// <para><b>Output Format:</b></para>
    /// <list type="bullet">
    ///   <item>2 bytes: Huffman table length (0x1FE = 510 entries)</item>
    ///   <item>510 × 2 bytes: Huffman tree table (1020 bytes)</item>
    ///   <item>Variable: Huffman-encoded LZ77 data stream</item>
    /// </list>
    ///
    /// <para><b>Huffman Tree Structure:</b></para>
    /// <para>The tree is stored as an array where:</para>
    /// <list type="bullet">
    ///   <item>Values 0-255: Leaf nodes containing the decoded byte value</item>
    ///   <item>Values 256-510: Internal nodes whose children are stored at
    ///   (value-256)*2 and (value-256)*2+1</item>
    /// </list>
    ///
    /// <para><b>Encoding Process:</b></para>
    /// <para>Each byte from the LZ77 stage is replaced by its Huffman code (variable-length
    /// bit sequence determined by traversing the tree from root to leaf).</para>
    /// </summary>
    internal class JPKEncodeHFI : JPKEncodeLz
    {
        /// <summary>
        /// Number of possible byte values (0-255).
        /// </summary>
        private const int m_headerLength = 0x100;

        /// <summary>
        /// Maximum code length supported by the encoder's 32-bit path representation.
        /// </summary>
        private const int MaxCodeLength = 30;

        /// <summary>
        /// Total nodes in a full binary tree with 256 leaves.
        /// </summary>
        private const int HuffmanNodeCount = m_headerLength * 2 - 1;

        private const int NoNode = -1;

        /// <summary>
        /// Huffman table length: 256 leaves + 254 internal nodes = 510 entries.
        /// </summary>
        protected const short m_hfTableLen = 0x1fe;

        /// <summary>
        /// Huffman tree table. Values 0-255 are leaves, 256+ are internal nodes.
        /// </summary>
        protected readonly short[] m_hfTable = new short[m_hfTableLen];

        /// <summary>
        /// Huffman code paths for each byte value (0-255). The path is the bit sequence
        /// to reach that leaf from the root.
        /// </summary>
        private readonly int[] m_paths = new int[m_headerLength];

        /// <summary>
        /// Bit lengths for each Huffman code (how many bits in m_paths are valid).
        /// </summary>
        private readonly short[] m_lengths = new short[m_headerLength];

        private byte m_bits = 0;
        private int m_bitcount = 0;
        private bool m_writingLzOutput = false;

        private void GetPaths(int nodeValue, int level, int path)
        {
            if (nodeValue < m_headerLength)
            {
                m_paths[nodeValue] = path;
                m_lengths[nodeValue] = (short)level;
                return;
            }

            if (level >= MaxCodeLength)
                throw new InvalidOperationException("Huffman tree exceeds the 30-bit code limit.");

            int childIndex = 2 * (nodeValue - m_headerLength);
            GetPaths(m_hfTable[childIndex], level + 1, path << 1);
            GetPaths(m_hfTable[childIndex + 1], level + 1, (path << 1) | 1);
        }

        /// <summary>
        /// Build a deterministic, frequency-based Huffman table for <paramref name="data"/>.
        /// All 256 symbols remain in the tree because the on-disk table always has 510 entries.
        /// </summary>
        /// <param name="data">Bytes that will be Huffman-encoded.</param>
        protected void FillTable(ReadOnlySpan<byte> data)
        {
            long[] frequencies = new long[m_headerLength];
            for (int i = 0; i < data.Length; i++)
                frequencies[data[i]]++;

            int[] codeLengths = BuildCodeLengths(frequencies);
            SerializeTree(codeLengths);

            Array.Clear(m_paths, 0, m_paths.Length);
            Array.Clear(m_lengths, 0, m_lengths.Length);
            m_bits = 0;
            m_bitcount = 0;
            GetPaths(m_hfTableLen, 0, 0);
        }

        private static int[] BuildCodeLengths(long[] frequencies)
        {
            int[] parents = new int[HuffmanNodeCount];
            long[] nodeFrequencies = new long[HuffmanNodeCount];
            Array.Fill(parents, NoNode);

            PriorityQueue<int, (long Frequency, int Node)> queue = new();
            for (int symbol = 0; symbol < m_headerLength; symbol++)
            {
                nodeFrequencies[symbol] = frequencies[symbol];
                queue.Enqueue(symbol, (frequencies[symbol], symbol));
            }

            int nextNode = m_headerLength;
            while (queue.Count > 1)
            {
                int left = queue.Dequeue();
                int right = queue.Dequeue();

                parents[left] = nextNode;
                parents[right] = nextNode;
                nodeFrequencies[nextNode] = nodeFrequencies[left] + nodeFrequencies[right];
                queue.Enqueue(nextNode, (nodeFrequencies[nextNode], nextNode));
                nextNode++;
            }

            if (nextNode != HuffmanNodeCount)
                throw new InvalidOperationException("Huffman tree has an invalid node count.");

            int[] codeLengths = new int[m_headerLength];
            for (int symbol = 0; symbol < m_headerLength; symbol++)
            {
                int node = symbol;
                while (parents[node] != NoNode)
                {
                    codeLengths[symbol]++;
                    node = parents[node];
                }
            }

            LimitCodeLengths(codeLengths, frequencies);
            return codeLengths;
        }

        /// <summary>
        /// Rebalance an over-deep Huffman tree while preserving a complete prefix code.
        /// The least frequent symbols receive the longest codes after rebalancing.
        /// </summary>
        private static void LimitCodeLengths(int[] codeLengths, long[] frequencies)
        {
            int maximumLength = 0;
            for (int i = 0; i < codeLengths.Length; i++)
                maximumLength = Math.Max(maximumLength, codeLengths[i]);

            if (maximumLength <= MaxCodeLength)
                return;

            int[] lengthCounts = new int[MaxCodeLength + 1];
            long occupiedSlots = 0;
            for (int i = 0; i < codeLengths.Length; i++)
            {
                int length = Math.Min(codeLengths[i], MaxCodeLength);
                lengthCounts[length]++;
                occupiedSlots += 1L << (MaxCodeLength - length);
            }

            long excessSlots = occupiedSlots - (1L << MaxCodeLength);
            while (excessSlots > 0)
            {
                int length = MaxCodeLength - 1;
                while (length > 0 && lengthCounts[length] == 0)
                    length--;

                if (length == 0 || lengthCounts[MaxCodeLength] == 0)
                    throw new InvalidOperationException("Unable to limit Huffman code lengths.");

                lengthCounts[length]--;
                lengthCounts[length + 1] += 2;
                lengthCounts[MaxCodeLength]--;
                excessSlots--;
            }

            int[] symbols = new int[m_headerLength];
            for (int symbol = 0; symbol < symbols.Length; symbol++)
                symbols[symbol] = symbol;

            Array.Sort(symbols, (left, right) =>
            {
                int frequencyComparison = frequencies[left].CompareTo(frequencies[right]);
                return frequencyComparison != 0 ? frequencyComparison : left.CompareTo(right);
            });

            int symbolIndex = 0;
            for (int length = MaxCodeLength; length > 0; length--)
            {
                for (int count = 0; count < lengthCounts[length]; count++)
                    codeLengths[symbols[symbolIndex++]] = length;
            }

            if (symbolIndex != m_headerLength)
                throw new InvalidOperationException("Length-limited Huffman tree lost a symbol.");
        }

        /// <summary>
        /// Convert canonical Huffman codes to the JPK table layout. The root has value 510;
        /// every other internal node receives a value from 256 through 509.
        /// </summary>
        private void SerializeTree(int[] codeLengths)
        {
            int[] lengthCounts = new int[MaxCodeLength + 1];
            for (int symbol = 0; symbol < codeLengths.Length; symbol++)
                lengthCounts[codeLengths[symbol]]++;

            int[] nextCodes = new int[MaxCodeLength + 1];
            int code = 0;
            for (int length = 1; length <= MaxCodeLength; length++)
            {
                code = (code + lengthCounts[length - 1]) << 1;
                nextCodes[length] = code;
            }

            int[,] children = new int[HuffmanNodeCount, 2];
            for (int node = 0; node < HuffmanNodeCount; node++)
            {
                children[node, 0] = NoNode;
                children[node, 1] = NoNode;
            }

            int root = m_headerLength;
            int nextInternalNode = root + 1;
            for (int symbol = 0; symbol < m_headerLength; symbol++)
            {
                int length = codeLengths[symbol];
                int symbolCode = nextCodes[length]++;
                int node = root;

                for (int bitPosition = length - 1; bitPosition >= 0; bitPosition--)
                {
                    int bit = (symbolCode >> bitPosition) & 1;
                    int child = children[node, bit];

                    if (bitPosition == 0)
                    {
                        if (child != NoNode)
                            throw new InvalidOperationException("Huffman code is not prefix-free.");
                        children[node, bit] = symbol;
                        continue;
                    }

                    if (child == NoNode)
                    {
                        if (nextInternalNode >= HuffmanNodeCount)
                            throw new InvalidOperationException("Huffman tree has too many internal nodes.");
                        child = nextInternalNode++;
                        children[node, bit] = child;
                    }
                    else if (child < m_headerLength)
                    {
                        throw new InvalidOperationException("Huffman code extends an existing leaf.");
                    }

                    node = child;
                }
            }

            if (nextInternalNode != HuffmanNodeCount)
                throw new InvalidOperationException("Huffman tree has an invalid internal node count.");

            int[] tableValues = new int[HuffmanNodeCount];
            tableValues[root] = m_hfTableLen;
            int nextTableValue = m_headerLength;
            for (int node = root + 1; node < HuffmanNodeCount; node++)
                tableValues[node] = nextTableValue++;

            Array.Clear(m_hfTable, 0, m_hfTable.Length);
            for (int node = root; node < HuffmanNodeCount; node++)
            {
                int tableIndex = (tableValues[node] - m_headerLength) * 2;
                for (int bit = 0; bit < 2; bit++)
                {
                    int child = children[node, bit];
                    if (child == NoNode)
                        throw new InvalidOperationException("Huffman tree contains an incomplete branch.");
                    m_hfTable[tableIndex + bit] =
                        (short)(child < m_headerLength ? child : tableValues[child]);
                }
            }
        }


        /// <summary>
        /// Compress the file in two passes: buffer the LZ output, then Huffman-encode it
        /// with a table built from the buffered byte frequencies.
        /// </summary>
        /// <param name="inBuffer">Input bytes buffer.</param>
        /// <param name="outStream">Stream to write to.</param>
        /// <param name="level">Compression level. Level will be truncated between 6 and 8191.</param>
        public override void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16)
        {
            using MemoryStream lzStream = new();
            m_writingLzOutput = true;
            try
            {
                base.ProcessOnEncode(inBuffer, lzStream, level);
            }
            finally
            {
                m_writingLzOutput = false;
            }

            ReadOnlySpan<byte> lzData = lzStream.GetBuffer().AsSpan(0, checked((int)lzStream.Length));
            FillTable(lzData);

            BinaryWriter bw = new(outStream);
            bw.Write(m_hfTableLen);
            for (int i = 0; i < m_hfTableLen; i++)
                bw.Write(m_hfTable[i]);

            for (int i = 0; i < lzData.Length; i++)
                WriteByte(outStream, lzData[i]);

            FlushWrite(outStream);
        }

        private void WriteBit(Stream outStream, byte inByte)
        {
            if (m_bitcount == 8)
            {
                outStream.WriteByte(m_bits);
                m_bits = 0;
                m_bitcount = 0;
            }
            m_bits <<= 1;
            m_bits |= inByte;
            m_bitcount++;
        }

        private void WriteBits(Stream outStream, int bits, int length)
        {
            while (length > 0)
            {
                length--;
                WriteBit(outStream, (byte)((bits >> length) & 1));
            }
        }
        protected void FlushWrite(Stream outStream)
        {
            if (m_bitcount > 0)
            {
                m_bits <<= 8 - m_bitcount;
                outStream.WriteByte(m_bits);
            }
            m_bits = 0;
            m_bitcount = 0;
        }

        /// <summary>
        /// Write a single byte from <paramref name="inByte"/> to <paramref name="outStream"/>.
        /// </summary>
        /// <param name="outStream">Stream to write to</param>
        /// <param name="inByte">byte to write</param>
        public override void WriteByte(Stream outStream, byte inByte)
        {
            if (m_writingLzOutput)
            {
                base.WriteByte(outStream, inByte);
                return;
            }

            int bits = m_paths[inByte];
            int len = m_lengths[inByte];
            WriteBits(outStream, bits, len);
        }
    }
}
