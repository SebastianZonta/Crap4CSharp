using System.Diagnostics;

namespace Crap4CSharp.Tests;

public sealed class MainTest : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("crap4csharp-main-").FullName;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
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
        GC.SuppressFinalize(this);
    }

    private sealed class FakeExecutor : ICommandExecutor
    {
        private readonly Func<IReadOnlyList<string>, string, int> _run;
        public FakeExecutor(Func<IReadOnlyList<string>, string, int> run) => _run = run;
        public int Run(IReadOnlyList<string> command, string directory) => _run(command, directory);
    }

    private static readonly CoverageRunner NoopCoverage =
        new(new FakeExecutor((_, _) => 0));

    [Fact]
    public void HelpWritesUsageToStdout()
    {
        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = Program.Run(["--help"], _tempDir, output, err, NoopCoverage);

        Assert.Equal(0, exit);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MainProcessExitsZeroForHelp()
    {
        Assert.Equal(0, RunTool("--help", _tempDir));
    }

    [Fact]
    public void MainProcessExitsNonZeroForUnknownOption()
    {
        Assert.Equal(1, RunTool("--changed " + Path.Combine("src", "Sample.cs"), _tempDir));
    }

    [Fact]
    public void ExplicitFileArgsAreAnalyzed()
    {
        string sourceRoot = Path.Combine(_tempDir, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        string source = Path.Combine(sourceRoot, "Sample.cs");
        File.WriteAllText(source, """
            namespace Demo;
            class Sample {
                int Alpha(bool a) {
                    if (a) {
                        return 1;
                    }
                    return 0;
                }
            }
            """);

        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = Program.Run(
            [Path.Combine("src", "Demo", "Sample.cs")],
            _tempDir,
            output,
            err,
            NoopCoverage);

        Assert.Equal(0, exit);
        Assert.Contains("Sample", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Alpha", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DirectoryArgAnalyzesCsFilesUnderThatDirectorySrc()
    {
        string moduleRoot = Path.Combine(_tempDir, "module-a");
        string sourceRoot = Path.Combine(moduleRoot, "src", "Demo");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "Sample.cs"), """
            namespace Demo;
            class Sample {
                int Alpha(bool a) {
                    if (a) {
                        return 1;
                    }
                    return 0;
                }
            }
            """);

        using var output = new StringWriter();
        using var err = new StringWriter();

        int exit = Program.Run(["module-a"], _tempDir, output, err, NoopCoverage);

        Assert.Equal(0, exit);
        Assert.Contains("Sample", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Alpha", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MaxCrapReturnsLargestNonNullScore()
    {
        var metrics = new List<MethodMetrics>
        {
            new("alpha", "Demo.Sample", 1, null, null),
            new("beta", "Demo.Sample", 1, 75.0, 4.5),
            new("gamma", "Demo.Sample", 1, 85.0, 7.0),
        };

        Assert.Equal(7.0, Program.MaxCrap(metrics));
    }

    private static int RunTool(string arguments, string workingDirectory)
    {
        string dll = FindToolDll();
        using var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{dll}\" {arguments}")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Tool process timed out: dotnet " + arguments);
        }
        return process.ExitCode;
    }

    private static string FindToolDll()
    {
        // .../test/Crap4CSharp.Tests/bin/<config>/<tfm>/ -> repo/src/Crap4CSharp/bin/<config>/<tfm>/
        string baseDir = AppContext.BaseDirectory;
        string tfm = Path.GetFileName(baseDir.TrimEnd(Path.DirectorySeparatorChar));
        string config = Path.GetFileName(Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar))!);
        string? dir = baseDir;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Crap4CSharp.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        if (dir is null)
        {
            throw new DirectoryNotFoundException("Repo root (Crap4CSharp.sln) not found above " + baseDir);
        }
        string dll = Path.Combine(dir, "src", "Crap4CSharp", "bin", config, tfm, "Crap4CSharp.dll");
        if (!File.Exists(dll))
        {
            throw new FileNotFoundException("Tool dll not built: " + dll);
        }
        return dll;
    }
}
