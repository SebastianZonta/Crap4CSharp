namespace Crap4CSharp.Tests;

public class SourceFileFinderTest
{
    [Fact]
    public void FindsAllCsFilesUnderSrcOnly()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var src = Path.Combine(tempDir, "src", "main", "cs", "demo");
            Directory.CreateDirectory(src);
            var inSrc = Path.Combine(src, "Sample.cs");
            File.WriteAllText(inSrc, "class Sample {}\n");

            var other = Path.Combine(tempDir, "other");
            Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(other, "Elsewhere.cs"), "class Elsewhere {}\n");

            var files = SourceFileFinder.FindAllCsFilesUnderSrc(tempDir);

            Assert.Equal(new[] { inSrc }, files);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
            catch (UnauthorizedAccessException)
            {
                // best-effort cleanup
            }
        }
    }
}