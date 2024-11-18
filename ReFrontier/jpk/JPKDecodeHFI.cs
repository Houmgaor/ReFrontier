using System.IO;

namespace ReFrontier.jpk
{
    /// <summary>
    /// Decode a JPK the same as JPKDecodeLz,
    /// but provides a new inteface for byte reading.
    /// </summary>
    internal class JPKDecodeHFI : JPKDecodeLz
    {
        private byte m_flagHF = 0;
        private int m_flagShift = 0;
        private int m_hfTableOffset = 0;
        private int m_hfDataOffset = 0;
        private int m_hfTableLen = 0;

        /// <summary>
        /// Decompress data the same as JPKDecodeLz, but sets input properties.
        /// </summary>
        /// <param name="inStream">Stream to read from.</param>
        /// <param name="outBuffer">Outpute buffer.</param>
        public override void ProcessOnDecode(Stream inStream, byte[] outBuffer)
        {
            BinaryReader br = new(inStream);
            m_hfTableLen = br.ReadInt16();
            m_hfTableOffset = (int)inStream.Position;
            m_hfDataOffset = m_hfTableOffset + m_hfTableLen * 4 - 0x3fc;
            base.ProcessOnDecode(inStream, outBuffer);
        }

        /// <summary>
        /// Read a byte implementing JpkGetHf
        /// </summary>
        /// <param name="s">Stream to read from</param>
        /// <returns>Read byte.</returns>
        public override byte ReadByte(Stream s)
        {
            int data = m_hfTableLen;
            BinaryReader br = new(s);

            while (data >= 0x100)
            {
                m_flagShift--;
                if (m_flagShift < 0)
                {
                    m_flagShift = 7;
                    s.Seek(m_hfDataOffset++, SeekOrigin.Begin);
                    m_flagHF = br.ReadByte();
                }
                byte bit = (byte)((m_flagHF >> m_flagShift) & 0x1);
                s.Seek((data * 2 - 0x200 + bit) * 2 + m_hfTableOffset, SeekOrigin.Begin);
                data = br.ReadInt16();
            }
            return (byte)data;
        }
    }
}
