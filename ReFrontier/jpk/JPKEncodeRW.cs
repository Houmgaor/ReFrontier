using System.IO;

namespace ReFrontier.jpk
{
    class JPKEncodeRW : IJPKEncode
    {
        public void ProcessOnEncode(byte[] inBuffer, Stream outStream, int level = 16, ShowProgress progress = null)
        {
            long perc, perc0 = 0;
            progress?.Invoke(0);
            for (int iin = 0; iin < inBuffer.Length; iin++)
            {
                perc = 100 * iin / inBuffer.Length;
                if (perc > perc0)
                {
                    perc0 = perc;
                    progress?.Invoke(perc);
                }
                WriteByte(outStream, inBuffer[iin]);
            }
            progress?.Invoke(100);
        }
        public void WriteByte(Stream s, byte b)
        {
            s.WriteByte(b);
        }
    }
}
