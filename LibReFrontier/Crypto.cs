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
    public static class Crypto
    {
        /// <summary>
        /// Default ECD key index used by all known Monster Hunter Frontier files.
        ///
        /// <para>Analysis of 1,962 encrypted files from MHF showed that 100% use key index 4.
        /// This includes mhfdat.bin, mhfemd.bin, stage files, NPC models, textures, and all
        /// other game assets.</para>
        ///
        /// <para>This discovery means .meta files are technically redundant for MHF files,
        /// as the key index can be assumed to be 4. However, .meta files are retained for
        /// compatibility with potential edge cases (dev builds, regional variants, older versions).</para>
        /// </summary>
        public const int DefaultEcdKeyIndex = 4;

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
        /// <para><b>Key Index Analysis:</b></para>
        /// <list type="table">
        ///   <listheader><term>Key</term><description>Multiplier / Increment / Notes</description></listheader>
        ///   <item><term>0</term><description>0x4A4B522E (1,246,450,222) / 1 / Unique</description></item>
        ///   <item><term>1</term><description>0x00010DCD (69,069) / 1 / Same as keys 2, 3</description></item>
        ///   <item><term>2</term><description>0x00010DCD (69,069) / 1 / Same as keys 1, 3</description></item>
        ///   <item><term>3</term><description>0x00010DCD (69,069) / 1 / Same as keys 1, 2</description></item>
        ///   <item><term>4</term><description>0x0019660D (1,664,525) / 3 / ALL MHF files use this key</description></item>
        ///   <item><term>5</term><description>0x7D2B89DD (2,100,005,341) / 1 / Unique</description></item>
        /// </list>
        ///
        /// <para><b>Notable:</b> Key 4's multiplier 1,664,525 (0x0019660D) is the famous LCG constant
        /// from "Numerical Recipes in C", a well-known pseudo-random number generator.</para>
        ///
        /// <para>Source: Reverse-engineered from game executable at address 0x10292DCC</para>
        /// </summary>
        private static readonly byte[] rndBufEcd = [
            0x4A,
            0x4B,
            0x52,
            0x2E,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x0D,
            0xCD,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x0D,
            0xCD,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x0D,
            0xCD,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x19,
            0x66,
            0x0D,
            0x00,
            0x00,
            0x00,
            0x03,
            0x7D,
            0x2B,
            0x89,
            0xDD,
            0x00,
            0x00,
            0x00,
            0x01
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
            0x4A,
            0x4B,
            0x52,
            0x2E,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x0D,
            0xCD,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x0D,
            0xCD,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x01,
            0x0D,
            0xCD,
            0x00,
            0x00,
            0x00,
            0x01,
            0x02,
            0xE9,
            0x0E,
            0xDD,
            0x00,
            0x00,
            0x00,
            0x03
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
            ArgumentNullException.ThrowIfNull(buffer);
            if (buffer.Length < 0x10)
                throw new DecryptionException("ECD buffer too small: minimum 16 bytes required for header.");

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
        /// <exception cref="ArgumentNullException">Thrown when buffer or bufferMeta is null.</exception>
        /// <exception cref="DecryptionException">Thrown when bufferMeta is too small (less than 6 bytes).</exception>
        public static byte[] EncodeEcd(byte[] buffer, byte[] bufferMeta)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentNullException.ThrowIfNull(bufferMeta);
            if (bufferMeta.Length < 6)
                throw new DecryptionException("ECD meta buffer too small: minimum 6 bytes required for key index.");

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
        /// Encode (encrypt) a file as ECD format using the default key index (4).
        ///
        /// <para>This overload allows encryption without a .meta file by using the default
        /// key index that all known MHF files use. Based on analysis of 1,962 encrypted files,
        /// 100% used key index 4.</para>
        /// </summary>
        /// <param name="buffer">Plaintext file data to encrypt.</param>
        /// <param name="keyIndex">Key index to use (0-5). Defaults to <see cref="DefaultEcdKeyIndex"/> (4).</param>
        /// <returns>Complete ECD file: 16-byte header + encrypted payload.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when keyIndex is not in range 0-5.</exception>
        public static byte[] EncodeEcd(byte[] buffer, int keyIndex = DefaultEcdKeyIndex)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (keyIndex < 0 || keyIndex > 5)
                throw new ArgumentOutOfRangeException(nameof(keyIndex), "Key index must be between 0 and 5.");

            // Build a synthetic meta buffer with the ECD magic and key index
            byte[] syntheticMeta = new byte[16];
            // ECD magic: 0x1A646365 ("ecd\x1A" in little-endian)
            syntheticMeta[0] = 0x65; // 'e'
            syntheticMeta[1] = 0x63; // 'c'
            syntheticMeta[2] = 0x64; // 'd'
            syntheticMeta[3] = 0x1A;
            // Key index at bytes 4-5 (little-endian)
            syntheticMeta[4] = (byte)(keyIndex & 0xFF);
            syntheticMeta[5] = (byte)((keyIndex >> 8) & 0xFF);
            // Bytes 6-7 are padding (zero)
            // Bytes 8-15 will be filled by EncodeEcd (payload size and CRC32)

            return EncodeEcd(buffer, syntheticMeta);
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
            ArgumentNullException.ThrowIfNull(buffer);
            if (buffer.Length < 0x10)
                throw new DecryptionException("EXF buffer too small: minimum 16 bytes required for header.");

            byte[] header = new byte[16];
            Array.Copy(buffer, header, header.Length);

            // Verify EXF magic number
            if (BitConverter.ToUInt32(header, 0) == 0x1a667865)
            {
                byte[] keybuf = CreateXorkeyExf(header);

                for (int i = 16; i < buffer.Length; i++)
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
        /// Encode (encrypt) a file as EXF format.
        ///
        /// <para>This is the inverse of DecodeExf. Creates a new buffer with 16-byte header
        /// followed by encrypted payload.</para>
        ///
        /// <para><b>Algorithm:</b></para>
        /// <para>For each plaintext byte, we need to find the encrypted byte that produces
        /// the correct plaintext when passed through the DecodeExf transformation.
        /// Since the transformation is not easily invertible, we use brute-force search
        /// over all 256 possible values (O(256) per byte).</para>
        /// </summary>
        /// <param name="buffer">Plaintext file data to encrypt.</param>
        /// <param name="bufferMeta">Original EXF header (from .meta file) containing key index and seed.</param>
        /// <returns>Complete EXF file: 16-byte header + encrypted payload.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer or bufferMeta is null.</exception>
        /// <exception cref="DecryptionException">Thrown when bufferMeta is too small (less than 16 bytes).</exception>
        public static byte[] EncodeExf(byte[] buffer, byte[] bufferMeta)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentNullException.ThrowIfNull(bufferMeta);
            if (bufferMeta.Length < 16)
                throw new DecryptionException("EXF meta buffer too small: minimum 16 bytes required for header.");

            // Verify EXF magic in meta
            if (BitConverter.ToUInt32(bufferMeta, 0) != 0x1a667865)
                throw new DecryptionException("Invalid EXF magic in meta buffer.");

            // Create output buffer: 16-byte header + payload
            byte[] outputBuffer = new byte[16 + buffer.Length];
            Array.Copy(bufferMeta, outputBuffer, 16);

            // Generate the XOR key from the header
            byte[] keybuf = CreateXorkeyExf(bufferMeta);

            // Encrypt each byte
            for (int i = 0; i < buffer.Length; i++)
            {
                uint r28 = (uint)i;  // Position offset from payload start
                byte plaintext = buffer[i];
                byte encrypted = FindEncryptedByte(plaintext, r28, keybuf);
                outputBuffer[16 + i] = encrypted;
            }

            return outputBuffer;
        }

        /// <summary>
        /// Find the encrypted byte value that produces the desired plaintext when decrypted.
        ///
        /// <para>This is a brute-force search over all 256 possible byte values.
        /// For each candidate, we apply the DecodeExf transformation and check
        /// if it produces the desired plaintext.</para>
        /// </summary>
        /// <param name="plaintext">The desired decrypted byte value.</param>
        /// <param name="position">Position offset from payload start (r28 in original code).</param>
        /// <param name="keybuf">16-byte XOR key buffer.</param>
        /// <returns>The encrypted byte value that decrypts to plaintext.</returns>
        /// <exception cref="DecryptionException">Thrown if no valid encrypted byte is found (should never happen).</exception>
        private static byte FindEncryptedByte(byte plaintext, uint position, byte[] keybuf)
        {
            // Try all 256 possible encrypted byte values
            for (int candidate = 0; candidate < 256; candidate++)
            {
                // Apply DecodeExf transformation to the candidate
                byte r8 = (byte)candidate;
                int index = (int)(position & 0xf);
                uint r4 = r8 ^ position;
                uint r12 = keybuf[index];
                uint r0 = (r4 & 0xf0) >> 4;
                uint r7 = keybuf[r0];
                uint r9 = r4 >> 4;
                uint r5 = r7 >> 4;
                r9 ^= r12;
                uint r26 = r5 ^ r4;
                r26 = (uint)(r26 & ~0xf0) | ((r9 & 0xf) << 4);

                // Check if this candidate decrypts to the desired plaintext
                if ((byte)r26 == plaintext)
                {
                    return (byte)candidate;
                }
            }

            // This should never happen - there should always be a valid encrypted byte
            throw new DecryptionException($"Failed to find encrypted byte for plaintext 0x{plaintext:X2} at position {position}.");
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
