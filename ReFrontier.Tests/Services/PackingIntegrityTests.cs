using System;

using LibReFrontier;
using LibReFrontier.Exceptions;

using ReFrontier.Jpk;
using ReFrontier.Services;
using ReFrontier.Tests.Mocks;

namespace ReFrontier.Tests.Services
{
    /// <summary>
    /// Tests that a pack which cannot complete reports what is wrong and leaves no
    /// output behind.
    ///
    /// <para>Unpacking is recursive by default, which replaces a nested <c>entry.jkr</c>
    /// with <c>entry.jkr.bin</c> while the log keeps naming the original. Packing used to
    /// discover this only part way through writing the archive, leaving a truncated file
    /// at the output path that reads as a result.</para>
    /// </summary>
    public class PackingIntegrityTests
    {
        private readonly InMemoryFileSystem _fileSystem;
        private readonly TestLogger _logger;
        private readonly FileProcessingConfig _config;
        private readonly PackingService _service;

        public PackingIntegrityTests()
        {
            _fileSystem = new InMemoryFileSystem();
            _logger = new TestLogger();
            _config = FileProcessingConfig.Default();
            _service = new PackingService(_fileSystem, _logger, new DefaultCodecFactory(), _config);
        }

        [Fact]
        public void ProcessPackInput_MissingEntry_Throws()
        {
            AddLog("SimpleArchive\ntest.bin\n2\nfile1.bin,0,10,0\nfile2.jkr,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/file1.bin", new byte[] { 1, 2, 3 });

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Contains("file2.jkr", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_MissingEntry_WritesNoOutput()
        {
            AddLog("SimpleArchive\ntest.bin\n2\nfile1.bin,0,10,0\nfile2.jkr,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/file1.bin", new byte[] { 1, 2, 3 });

            Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            // A truncated archive at the output path is worse than no archive.
            Assert.False(_fileSystem.FileExists("output/test.bin"));
        }

        [Fact]
        public void ProcessPackInput_MissingEntry_LeavesEarlierOutputUntouched()
        {
            byte[] previous = [0xAA, 0xBB, 0xCC];
            _fileSystem.AddFile("output/test.bin", previous);
            AddLog("SimpleArchive\ntest.bin\n2\nfile1.bin,0,10,0\nfile2.jkr,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/file1.bin", new byte[] { 1, 2, 3 });

            Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Equal(previous, _fileSystem.ReadAllBytes("output/test.bin"));
        }

        [Fact]
        public void ProcessPackInput_SeveralMissingEntries_ReportsThemAllAtOnce()
        {
            AddLog("SimpleArchive\ntest.bin\n3\na.bin,0,10,0\nb.jkr,10,20,0\nc.jkr,20,30,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1 });

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            // Fixing one entry at a time across a 6-entry archive is a bad experience.
            Assert.Contains("b.jkr", ex.Message, StringComparison.Ordinal);
            Assert.Contains("c.jkr", ex.Message, StringComparison.Ordinal);
            Assert.Contains("2 of 3 entries", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_EntryUnpackedInPlace_PointsAtItsRecipe()
        {
            AddLog("SimpleArchive\ntest.bin\n2\na.bin,0,10,0\nb.jkr,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1 });
            _fileSystem.AddFile("/test/dir.unpacked/b.jkr.bin", new byte[] { 2 });

            var recipe = new ExtractionRecipe { SourceFile = "b.jkr", ExtractedFile = "b.jkr.bin" };
            recipe.Layers.Add(new RecipeLayer { Kind = RecipeLayerKind.Jpk, Algorithm = CompressionType.HFI });
            _fileSystem.AddFile($"/test/dir.unpacked/b.jkr{ExtractionRecipe.FileSuffix}", recipe.Serialize());

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Contains("unpacked to b.jkr.bin", ex.Message, StringComparison.Ordinal);
            Assert.Contains("--restore", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_EntryUnpackedIntoDirectory_PointsAtThatDirectory()
        {
            AddLog("SimpleArchive\ntest.bin\n2\na.bin,0,10,0\nb.bin,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1 });
            _fileSystem.AddFile("/test/dir.unpacked/b.bin.unpacked/inner.bin", new byte[] { 2 });

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Contains("b.bin.unpacked", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_EntryRenamed_NamesWhatWasFoundInstead()
        {
            AddLog("SimpleArchive\ntest.bin\n2\na.bin,0,10,0\nb.jkr,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1 });
            _fileSystem.AddFile("/test/dir.unpacked/b.jkr.bin", new byte[] { 2 });

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Contains("b.jkr.bin", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_ExplainsHowToRecover()
        {
            AddLog("SimpleArchive\ntest.bin\n2\na.bin,0,10,0\nb.jkr,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1 });

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Contains("--nonRecursive", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_NullEntries_AreNotTreatedAsMissing()
        {
            AddLog("SimpleArchive\ntest.bin\n2\na.bin,0,10,0\nnull,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1, 2, 3 });

            _service.ProcessPackInput("/test/dir.unpacked");

            Assert.True(_fileSystem.FileExists("output/test.bin"));
        }

        [Fact]
        public void ProcessPackInput_LogDeclaresMoreEntriesThanItLists_Throws()
        {
            AddLog("SimpleArchive\ntest.bin\n5\na.bin,0,10,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1 });

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Contains("truncated or corrupt", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_LogTooShort_Throws()
        {
            AddLog("SimpleArchive");

            var ex = Assert.Throws<PackingException>(() => _service.ProcessPackInput("/test/dir.unpacked"));

            Assert.Contains("too short", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessPackInput_Success_LeavesNoTemporaryFileBehind()
        {
            AddLog("SimpleArchive\ntest.bin\n2\na.bin,0,10,0\nb.bin,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1, 2, 3 });
            _fileSystem.AddFile("/test/dir.unpacked/b.bin", new byte[] { 4, 5, 6 });

            _service.ProcessPackInput("/test/dir.unpacked");

            Assert.True(_fileSystem.FileExists("output/test.bin"));
            Assert.DoesNotContain(
                _fileSystem.Files.Keys,
                path => path.EndsWith(".packing", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void ProcessPackInput_Success_OverwritesAnEarlierOutput()
        {
            _fileSystem.AddFile("output/test.bin", new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE });
            AddLog("SimpleArchive\ntest.bin\n2\na.bin,0,10,0\nb.bin,10,20,0");
            _fileSystem.AddFile("/test/dir.unpacked/a.bin", new byte[] { 1, 2, 3 });
            _fileSystem.AddFile("/test/dir.unpacked/b.bin", new byte[] { 4, 5, 6 });

            _service.ProcessPackInput("/test/dir.unpacked");

            byte[] packed = _fileSystem.ReadAllBytes("output/test.bin");
            Assert.Equal(2, BitConverter.ToInt32(packed, 0));
        }

        private void AddLog(string content)
        {
            _fileSystem.AddFile("/test/dir.unpacked/dir.unpacked.log", content);
        }
    }
}
