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
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void TreeSkipsBinAndObjCsFiles()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var kept = Path.Combine(tempDir, "Keep.cs");
            File.WriteAllText(kept, "class Keep {}\n");
            var upper = Path.Combine(tempDir, "Upper.CS");
            File.WriteAllText(upper, "class Upper {}\n");
            var nested = Path.Combine(tempDir, "sub", "Nested.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
            File.WriteAllText(nested, "class Nested {}\n");
            foreach (var skipped in new[]
            {
                Path.Combine(tempDir, "bin", "Skipped.cs"),
                Path.Combine(tempDir, "obj", "Skipped.cs"),
                Path.Combine(tempDir, "sub", "bin", "Skipped.cs"),
                Path.Combine(tempDir, "sub", "obj", "Skipped.cs"),
            })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(skipped)!);
                File.WriteAllText(skipped, "class Skipped {}\n");
            }

            var files = SourceFileFinder.FindAllCsFilesUnderTree(tempDir);

            Assert.Equal(new[] { kept, upper, nested }, files);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void ForDirectoryPrefersSrcWhenNonEmpty()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var inSrc = Path.Combine(tempDir, "src", "InSrc.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(inSrc)!);
            File.WriteAllText(inSrc, "class InSrc {}\n");
            var outside = Path.Combine(tempDir, "Outside.cs");
            File.WriteAllText(outside, "class Outside {}\n");

            var files = SourceFileFinder.FindAllCsFilesForDirectory(tempDir);

            Assert.Equal(new[] { inSrc }, files);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void ForDirectoryFallsBackToTreeWhenNoSrc()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var top = Path.Combine(tempDir, "Top.cs");
            File.WriteAllText(top, "class Top {}\n");

            var files = SourceFileFinder.FindAllCsFilesForDirectory(tempDir);

            Assert.Equal(new[] { top }, files);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Theory]
    [InlineData("App.sln", true)]
    [InlineData("App.slnx", true)]
    [InlineData("APP.SLN", true)]
    [InlineData("APP.SLNX", true)]
    [InlineData("App.Sln", true)]
    [InlineData("Sample.cs", false)]
    [InlineData("Module.csproj", false)]
    [InlineData("Module.CSPROJ", false)]
    [InlineData("Module.CsProj", false)]
    public void IsSolutionFileMatchesSlnAndSlnxOnly(string fileName, bool expected)
    {
        Assert.Equal(expected, SourceFileFinder.IsSolutionFile(fileName));
    }

    [Theory]
    [InlineData("Module.csproj", true)]
    [InlineData("MODULE.CSPROJ", true)]
    [InlineData("Module.CsProj", true)]
    [InlineData("App.sln", false)]
    [InlineData("App.slnx", false)]
    [InlineData("Sample.cs", false)]
    public void IsCsprojFileMatchesCsprojOnly(string fileName, bool expected)
    {
        Assert.Equal(expected, SourceFileFinder.IsCsprojFile(fileName));
    }

    private static void DeleteTempDir(string tempDir)
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}