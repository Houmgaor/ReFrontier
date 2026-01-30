namespace ReFrontier.Tests;

public class TestProgram
{
    private const string TestDataFile = "./mhfdat.bin";

    [Fact]
    public void TestStartProcessingFile()
    {
        // Skip test if mhfdat.bin is not available (it's a game file not included in repo)
        if (!File.Exists(TestDataFile))
        {
            return;
        }

        string tmpFilePath = "./mhfdat_copy.bin";
        try
        {
            File.Copy(TestDataFile, tmpFilePath, overwrite: true);
            InputArguments inputArguments = new();
            Program.StartProcessingFile(tmpFilePath, inputArguments);
        }
        finally
        {
            // Clean up temporary file
            if (File.Exists(tmpFilePath))
            {
                File.Delete(tmpFilePath);
            }
        }
    }
}