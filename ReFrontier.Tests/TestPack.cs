using ReFrontier;
using LibReFrontier;

namespace ReFrontier.Tests;

public class TestPack
{
    private readonly string _testDir;

    public TestPack()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ReFrontierTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    private string CreateTestFile(string content)
    {
        string filepath = Path.Combine(_testDir, "test_" + Guid.NewGuid().ToString("N")[..8]);
        File.WriteAllText(filepath, content);
        return filepath;
    }

    [Fact]
    public void JPKEncode_LZ_CompressesFile()
    {
        Compression compression = new()
        {
            type = CompressionType.LZ,
            level = 15
        };
        string filepath = CreateTestFile("This is a random text, the number of bytes is important.");
        string outputPath = filepath + ".jpk";

        var pack = new Pack();
        pack.JPKEncode(compression, filepath, outputPath);

        Assert.True(File.Exists(outputPath));
        byte[] output = File.ReadAllBytes(outputPath);
        // Check JKR header magic
        Assert.Equal(0x4A, output[0]); // 'J'
        Assert.Equal(0x4B, output[1]); // 'K'
        Assert.Equal(0x52, output[2]); // 'R'
        Assert.Equal(0x1A, output[3]); // magic byte
    }

    [Fact]
    public void JPKEncode_RW_ProducesValidOutput()
    {
        Compression compression = new()
        {
            type = CompressionType.RW,
            level = 10
        };
        string filepath = CreateTestFile("Test content for RW compression");
        string outputPath = filepath + ".jpk";

        var pack = new Pack();
        pack.JPKEncode(compression, filepath, outputPath);

        Assert.True(File.Exists(outputPath));
        byte[] output = File.ReadAllBytes(outputPath);
        // Check JKR header magic
        Assert.Equal(0x4A, output[0]);
        Assert.Equal(0x4B, output[1]);
        Assert.Equal(0x52, output[2]);
        Assert.Equal(0x1A, output[3]);
        // Check compression type is RW (0)
        Assert.Equal(0, BitConverter.ToUInt16(output, 6));
    }

    [Fact]
    public void JPKEncode_HFI_ProducesValidOutput()
    {
        Compression compression = new()
        {
            type = CompressionType.HFI,
            level = 20
        };
        string filepath = CreateTestFile("Test content for HFI Huffman compression encoding test.");
        string outputPath = filepath + ".jpk";

        var pack = new Pack();
        pack.JPKEncode(compression, filepath, outputPath);

        Assert.True(File.Exists(outputPath));
        byte[] output = File.ReadAllBytes(outputPath);
        // Check JKR header magic
        Assert.Equal(0x4A, output[0]);
        Assert.Equal(0x4B, output[1]);
        Assert.Equal(0x52, output[2]);
        Assert.Equal(0x1A, output[3]);
        // Check compression type is HFI (4)
        Assert.Equal(4, BitConverter.ToUInt16(output, 6));
    }

    [Fact]
    public void JPKEncode_InvalidCompressionType_ThrowsException()
    {
        Compression compression = new()
        {
            type = CompressionType.None,  // None is not a valid encoding type
            level = 10
        };
        string filepath = CreateTestFile("Test");
        string outputPath = filepath + ".jpk";

        var pack = new Pack();
        Assert.Throws<InvalidOperationException>(() =>
            pack.JPKEncode(compression, filepath, outputPath)
        );
    }

    [Fact]
    public void JPKEncode_HFIRW_CreatesValidFile()
    {
        Compression compression = new()
        {
            type = CompressionType.HFIRW,
            level = 10
        };
        string filepath = CreateTestFile("Test content for HFIRW encoding");
        string outputPath = filepath + ".jpk";

        var pack = new Pack();
        pack.JPKEncode(compression, filepath, outputPath);

        // Verify file was created and has JKR magic header
        Assert.True(File.Exists(outputPath));
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.True(output.Length > 16);
        // JKR magic: 0x1A524B4A
        Assert.Equal(0x4A, output[0]); // 'J'
        Assert.Equal(0x4B, output[1]); // 'K'
        Assert.Equal(0x52, output[2]); // 'R'
        Assert.Equal(0x1A, output[3]);
    }

    [Fact]
    public void JPKEncode_OverwritesExistingFile()
    {
        Compression compression = new()
        {
            type = CompressionType.LZ,
            level = 10
        };
        string filepath = CreateTestFile("Original content");
        string outputPath = filepath + ".jpk";

        // Create existing output file with dummy content
        File.WriteAllBytes(outputPath, [0x00, 0x01, 0x02]);

        var pack = new Pack();
        pack.JPKEncode(compression, filepath, outputPath);

        byte[] output = File.ReadAllBytes(outputPath);
        // Should have JKR magic, not the dummy content
        Assert.Equal(0x4A, output[0]);
    }
}
