using System;
using System.IO.Hashing;

using LibReFrontier.Exceptions;

namespace LibReFrontier
{
    /// <summary>
    /// Cryptographic features for Monster Hunter Frontier file formats.
    ///
    /// This class implements decryption/encryption for ECD and EXF file formats
    /// used by Monster Hunter Frontier Online. The algorithms were reverse-engineered
    /// from the game executable.
    ///
    /// <para><b>ECD Format (0x1A646365 magic):</b></para>
    /// <para>Uses a Linear Congruential Generator (LCG) for pseudo-random key generation,
    /// combined with nibble-based XOR cipher. The header contains key index, payload size,
    /// and CRC32 checksum.</para>
    ///
    /// <para><b>EXF Format (0x1A667865 magic):</b></para>
    /// <para>Alternative encryption format using a 16-byte XOR key derived from header values
    /// via LCG. Uses position-dependent nibble transformation.</para>
    ///
    /// <para><b>Variable Naming Convention:</b></para>
    /// <para>Variables like r8, r10, r11, r12, r26, r28 correspond to PowerPC register names
    /// from the original reverse-engineered assembly code. This naming is preserved for
    /// easier cross-reference with disassembly.</para>
    ///
    /// With major help from enler.
    /// </summary>
    public class Crypto
    {
        /// <summary>
        /// ECD/EXF encryption keys containing LCG (Linear Congruential Generator) parameters.
        ///
        /// <para><b>Structure:</b> 6 key sets, 8 bytes each (48 bytes total)</para>
        /// <para>Each 8-byte key set contains:</para>
        /// <list type="bullet">
        ///   <item>Bytes 0-3: Multiplier (big-endian) for LCG formula</item>
        ///   <item>Bytes 4-7: Increment (big-endian) for LCG formula</item>
        /// </list>
        ///
        /// <para><b>LCG Formula:</b> next = current * multiplier + increment</para>
        ///
        /// <para>Source: Reverse-engineered from game executable at address 0x10292DCC</para>
        /// </summary>
        private static readonly byte[] rndBufEcd = [
            0x4A, 0x4B, 0x52, 0x2E, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D,
            0xCD, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00,
            0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x19, 0x66, 0x0D, 0x00, 0x00, 0x00, 0x03, 0x7D, 0x2B, 0x89, 0xDD,
            0x00, 0x00, 0x00, 0x01
        ];

        /// <summary>
        /// EXF encryption keys containing LCG parameters (similar structure to rndBufEcd).
        ///
        /// <para><b>Structure:</b> 5 key sets, 8 bytes each (40 bytes total)</para>
        /// <para>Note: Smaller than ECD buffer, supports fewer key indices.</para>
        ///
        /// <para>Source: Reverse-engineered from game executable at address 0x1025F4E0</para>
        /// </summary>
        private static readonly byte[] rndBufExf = [
            0x4A, 0x4B, 0x52, 0x2E, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D,
            0xCD, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00,
            0x00, 0x01, 0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00, 0x00, 0x01, 0x02,
            0xE9, 0x0E, 0xDD, 0x00, 0x00, 0x00, 0x03
        ];

        /// <summary>
        /// Load 4 consecutive bytes in the buffer as an integer.
        /// </summary>
        /// <param name="buffer">Data buffer to read from.</param>
        /// <param name="offset">First byte index to read</param>
        /// <returns>First four bytes as an integer.</returns>
        private static uint LoadUInt32BE(byte[] buffer, int offset)
        {
            uint value = (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3]);
            return value;
        }

        /// <summary>
        /// Generate next pseudo-random value using Linear Congruential Generator (LCG).
        ///
        /// <para><b>Algorithm:</b> rnd = rnd * multiplier + increment</para>
        /// <para>Where multiplier and increment are loaded from rndBufEcd at index ecdKey.</para>
        ///
        /// <para>This is a standard LCG commonly used in older games for deterministic
        /// pseudo-random number generation.</para>
        /// </summary>
        /// <param name="ecdKey">Key index (0-5) to select LCG parameters from rndBufEcd.</param>
        /// <param name="rnd">Current LCG state, modified in place to next state.</param>
        /// <returns>The new LCG state (same as modified rnd parameter).</returns>
        private static uint GetRndEcd(int ecdKey, ref uint rnd)
        {
            rnd = rnd * LoadUInt32BE(rndBufEcd, 8 * ecdKey) + LoadUInt32BE(rndBufEcd, 8 * ecdKey + 4);
            return rnd;
        }

        /// <summary>
        /// Decode an ECD encrypted file in place.
        ///
        /// <para><b>ECD Header Structure (16 bytes at offset 0x00):</b></para>
        /// <list type="bullet">
        ///   <item>Bytes 0-3: Magic number (0x1A646365 "ecd\x1A")</item>
        ///   <item>Bytes 4-5: Key index for LCG parameter selection</item>
        ///   <item>Bytes 6-7: (unused/padding)</item>
        ///   <item>Bytes 8-11: Payload size (encrypted data length)</item>
        ///   <item>Bytes 12-15: CRC32 of decrypted payload (for validation)</item>
        /// </list>
        ///
        /// <para><b>Decryption Algorithm:</b></para>
        /// <para>1. Initialize LCG state from CRC32 with bit rotation: (crc &lt;&lt; 16) | (crc &gt;&gt; 16) | 1</para>
        /// <para>2. For each byte: XOR with previous output, split into nibbles, perform 8-round
        ///    Feistel-like transformation using LCG-generated XOR pad</para>
        /// <para>3. Recombine nibbles to produce decrypted byte</para>
        ///
        /// <para><b>Variable names from reverse engineering:</b></para>
        /// <para>r8 = previous decrypted byte (feedback), r10/r11/r12 = working registers for nibble transform</para>
        /// </summary>
        /// <param name="buffer">Input file buffer to decode (modified in place). Decrypted data starts at offset 0x10.</param>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        /// <exception cref="DecryptionException">Thrown when buffer is too small (less than 16 bytes).</exception>
        public static void DecodeEcd(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < 0x10)
                throw new DecryptionException("ECD buffer too small: minimum 16 bytes required for header.", (string)null);

            int ecdKey = BitConverter.ToUInt16(buffer, 4);      // Key index at offset 4
            uint payloadSize = BitConverter.ToUInt32(buffer, 8); // Payload size at offset 8
            uint crc32 = BitConverter.ToUInt32(buffer, 12);      // CRC32 at offset 12
            // Initialize LCG state: rotate CRC32 and set LSB to ensure odd value
            uint rnd = (crc32 << 16) | (crc32 >> 16) | 1;

            uint xorpad = GetRndEcd(ecdKey, ref rnd);

            byte r8 = (byte)xorpad; // r8: previous decrypted byte for feedback chain

            for (int i = 0; i < payloadSize; i++)
            {
                xorpad = GetRndEcd(ecdKey, ref rnd); // Generate new XOR pad for this byte

                byte data = buffer[0x10 + i];      // Read encrypted byte
                uint r11 = (uint)(data ^ r8);      // XOR with previous output (cipher feedback)
                uint r12 = (r11 >> 4) & 0xFF;      // Extract high nibble

                // 8-round Feistel-like nibble transformation
                for (int j = 0; j < 8; j++)
                {
                    uint r10 = xorpad ^ r11;       // XOR current nibble with pad
                    r11 = r12;                      // Swap nibbles
                    r12 ^= r10;                     // Mix with XOR result
                    r12 &= 0xFF;                    // Keep byte range
                    xorpad >>= 4;                   // Shift to next nibble of XOR pad
                }

                // Recombine nibbles: low nibble from r12, high nibble from r11
                r8 = (byte)((r12 & 0xF) | ((r11 & 0xF) << 4));
                buffer[0x10 + i] = r8;             // Store decrypted byte
            }
        }

        /// <summary>
        /// Encode (encrypt) a file as ECD format.
        ///
        /// <para>This is the inverse of DecodeEcd. Creates a new buffer with 16-byte header
        /// followed by encrypted payload.</para>
        /// </summary>
        /// <param name="buffer">Plaintext file data to encrypt.</param>
        /// <param name="bufferMeta">Original ECD header (from .meta file) containing key index and magic.</param>
        /// <returns>Complete ECD file: 16-byte header + encrypted payload.</returns>
        public static byte[] EncodeEcd(byte[] buffer, byte[] bufferMeta)
        {
            // Update meta data
            int payloadSize = buffer.Length;
            uint crc32w = Crc32.HashToUInt32(buffer);
            int index = BitConverter.ToUInt16(bufferMeta, 4);

            // Write meta data
            byte[] outputBuffer = new byte[16 + payloadSize];
            Array.Copy(bufferMeta, outputBuffer, bufferMeta.Length);
            Array.Copy(BitConverter.GetBytes(payloadSize), 0, outputBuffer, 8, 4);
            Array.Copy(BitConverter.GetBytes(crc32w), 0, outputBuffer, 12, 4);

            // Encrypt data
            int i;
            uint rnd = (crc32w << 16) | (crc32w >> 16) | 1;
            uint xorpad = GetRndEcd(index, ref rnd);
            byte r8 = (byte)xorpad;

            for (i = 0; i < payloadSize; i++)
            {
                xorpad = GetRndEcd(index, ref rnd);
                byte data = buffer[i];
                uint r11 = 0;
                uint r12 = 0;
                for (int j = 0; j < 8; j++)
                {
                    uint r10 = xorpad ^ r11;
                    r11 = r12;
                    r12 ^= r10;
                    r12 &= 0xFF;
                    xorpad >>= 4;
                }

                uint dig2 = data;
                uint dig1 = (dig2 >> 4) & 0xFF;
                dig1 ^= r11;
                dig2 ^= r12;
                dig1 ^= dig2;

                byte rr = (byte)((dig2 & 0xF) | ((dig1 & 0xF) << 4));
                rr = (byte)(rr ^ r8);
                outputBuffer[16 + i] = rr;
                r8 = data;
            }
            return outputBuffer;
        }

        /// <summary>
        /// Generate 16-byte XOR key for EXF decryption using LCG.
        ///
        /// <para>Derives 4 uint32 values from header using LCG, XORs each with
        /// initial seed value to create position-based decryption key.</para>
        /// </summary>
        /// <param name="header">First 16 bytes of the EXF file containing key index and seed.</param>
        /// <returns>16-byte XOR key buffer for position-based decryption.</returns>
        private static byte[] CreateXorkeyExf(byte[] header)
        {
            byte[] keyBuffer = new byte[16];
            int index = BitConverter.ToUInt16(header, 4);
            uint tempVal = BitConverter.ToUInt32(header, 0xc);
            uint value = BitConverter.ToUInt32(header, 0xc);
            for (int i = 0; i < 4; i++)
            {
                tempVal = tempVal * LoadUInt32BE(rndBufExf, index * 8) + LoadUInt32BE(rndBufExf, index * 8 + 4);
                uint key = tempVal ^ value;
                byte[] tempKey = BitConverter.GetBytes(key);
                Array.Copy(tempKey, 0, keyBuffer, i * 4, 4);
            }
            return keyBuffer;
        }

        /// <summary>
        /// Decode an EXF encrypted file in place.
        ///
        /// <para><b>EXF Header Structure (16 bytes):</b></para>
        /// <list type="bullet">
        ///   <item>Bytes 0-3: Magic number (0x1A667865 "exf\x1A")</item>
        ///   <item>Bytes 4-5: Key index for LCG parameter selection</item>
        ///   <item>Bytes 12-15: Seed value for XOR key generation</item>
        /// </list>
        ///
        /// <para><b>Decryption Algorithm:</b></para>
        /// <para>1. Generate 16-byte XOR key from header using LCG</para>
        /// <para>2. For each payload byte at position i:</para>
        /// <para>   - XOR with position offset (i - 0x10)</para>
        /// <para>   - Look up key bytes using nibble indices</para>
        /// <para>   - Perform nibble transformation and recombination</para>
        ///
        /// <para><b>Variable names from reverse engineering (PowerPC registers):</b></para>
        /// <para>r28 = position offset, r8 = input byte, r4/r5/r7/r9/r12/r26 = working registers</para>
        /// </summary>
        /// <param name="buffer">Input file buffer to decode (modified in place). Decrypted data starts at offset 0x10.</param>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        /// <exception cref="DecryptionException">Thrown when buffer is too small (less than 16 bytes).</exception>
        public static void DecodeExf(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < 0x10)
                throw new DecryptionException("EXF buffer too small: minimum 16 bytes required for header.", (string)null);

            byte[] header = new byte[16];
            Array.Copy(buffer, header, header.Length);

            // Verify EXF magic number
            if (BitConverter.ToUInt32(header, 0) == 0x1a667865)
            {
                byte[] keybuf = CreateXorkeyExf(header);

                for (int i = 16; i < buffer.Length - header.Length; i++)
                {
                    uint r28 = (uint)(i - 0x10);    // Position offset from payload start
                    byte r8 = buffer[i];            // Read encrypted byte
                    int index = (int)(r28 & 0xf);   // Low nibble of position -> key index
                    uint r4 = r8 ^ r28;             // XOR with position
                    uint r12 = keybuf[index];       // Lookup key byte by position nibble
                    uint r0 = (r4 & 0xf0) >> 4;     // High nibble of XOR result
                    uint r7 = keybuf[r0];           // Lookup key byte by high nibble
                    uint r9 = r4 >> 4;              // Shift r4 right by nibble
                    uint r5 = r7 >> 4;              // Shift key byte right by nibble
                    r9 ^= r12;                      // XOR with first key lookup
                    uint r26 = r5 ^ r4;             // XOR shifted key with r4
                    // Recombine: low nibble from r26, high nibble from r9
                    r26 = (uint)(r26 & ~0xf0) | ((r9 & 0xf) << 4);
                    buffer[i] = (byte)r26;          // Store decrypted byte
                }
            }
        }


        /// <summary>
        /// Compute CRC32 for byte array.
        /// 
        /// It is just to remove dependency from FrontierTextTool
        /// </summary>
        /// <param name="array">Array to hash</param>
        /// <returns>CRC32 bits hash</returns>
        public static uint GetCrc32(byte[] array)
        {
            return Crc32.HashToUInt32(array);
        }
    }
}
