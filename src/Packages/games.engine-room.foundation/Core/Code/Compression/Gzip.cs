using System.IO;
using System.IO.Compression;

namespace Core.Compression
{
    public static class Gzip
    {
        public static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();

            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            {
                gzip.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }
    }
}
