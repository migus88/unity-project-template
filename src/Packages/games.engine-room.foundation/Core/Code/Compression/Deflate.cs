using System;
using System.IO;
using System.IO.Compression;
using Core.Results;
using OneOf;

namespace Core.Compression
{
    public static class Deflate
    {
        private const int CopyBufferSize = 4096;

        public static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();

            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
            {
                deflate.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        public static OneOf<byte[], Corrupted> Decompress(byte[] data, int maxLength)
        {
            if (maxLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "Maximum length must not be negative.");
            }

            try
            {
                using var input = new MemoryStream(data, false);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                var buffer = new byte[CopyBufferSize];
                int read;

                while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > maxLength)
                    {
                        return new Corrupted($"Inflated data exceeds {maxLength} bytes.");
                    }

                    output.Write(buffer, 0, read);
                }

                return output.ToArray();
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                return new Corrupted($"Invalid deflate data: {exception.Message}");
            }
        }
    }
}
