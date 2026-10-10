using System;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using Core.Compression;
using Core.Results;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Compression
{
    public sealed class DeflateTests
    {
        private const int MaxLength = 1 << 20;

        [Test]
        public void Decompress_Compressed_RoundTrips()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("strike-card;", 200)));
            var compressed = Deflate.Compress(data);

            // Act
            var result = Deflate.Decompress(compressed, MaxLength);

            // Assert
            compressed.Length.Should().BeLessThan(data.Length);
            result.Should().BeCase<byte[]>().Which.Should().Equal(data);
        }

        [Test]
        public void Decompress_Garbage_Corrupted()
        {
            // Arrange
            var garbage = new byte[64];
            new Random(7).NextBytes(garbage);
            garbage[0] = 0xFF;

            // Act
            var result = Deflate.Decompress(garbage, MaxLength);

            // Assert
            result.Should().BeCase<Corrupted>();
        }

        [Test]
        public void Decompress_LargerThanMax_Corrupted()
        {
            // Arrange
            var compressed = Deflate.Compress(new byte[10000]);

            // Act
            var result = Deflate.Decompress(compressed, 9999);

            // Assert
            result.Should().BeCase<Corrupted>();
        }
    }
}
