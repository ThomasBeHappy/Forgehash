using System.Diagnostics;
using System.Text;
using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

namespace ForgeHash.CrossImplementation.Tests;

/// <summary>
/// Pins .NET ForgeHash-X against toy vectors and, when tools are installed,
/// cross-checks Python / Node / Rust ports on vector2.
/// </summary>
public class XToyVectorInteropTests
{
    private const string Vector2Hash =
        "7e1916d2329e65ccb5c8e211c25c31efdd381cec765d721b35c2878e99ce9e08";

    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void DotNetReference_MatchesFrozenVector2()
    {
        byte[] password = Convert.FromHexString("70617373776f7264");
        byte[] salt = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        var parameters = new ForgeHashXParameters
        {
            MemoryKiB = 1024,
            Iterations = 1,
            Parallelism = 1,
        };

        byte[] hash = ForgeHashXApi.DeriveHash(password, salt, parameters);
        Assert.Equal(Vector2Hash, Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public void PythonPort_MatchesVector2_WhenAvailable()
    {
        string? python = FindOnPath("python") ?? FindOnPath("python3");
        if (python is null)
        {
            return;
        }

        string script = Path.Combine(RepoRoot, "tools", "crosscheck", "x_vector2.py");
        string stdout = Run(python, $"\"{script}\"");
        Assert.Equal(Vector2Hash, stdout.Trim().ToLowerInvariant());
    }

    [Fact]
    public void NodePort_MatchesVector2_WhenAvailable()
    {
        string? node = FindOnPath("node");
        if (node is null)
        {
            return;
        }

        string script = Path.Combine(RepoRoot, "tools", "crosscheck", "x_vector2.mjs");
        string stdout = Run(node, $"\"{script}\"");
        Assert.Equal(Vector2Hash, stdout.Trim().ToLowerInvariant());
    }

    [Fact]
    public void RustPort_ToyVectors_Pass_WhenCargoAvailable()
    {
        string? cargo = FindOnPath("cargo");
        if (cargo is null)
        {
            return;
        }

        string manifest = Path.Combine(RepoRoot, "langs", "rust", "forgehx", "Cargo.toml");
        int code = RunExit(cargo, $"test --manifest-path \"{manifest}\" --release -q", out string stdout, out string stderr);
        Assert.True(code == 0, $"cargo test failed:\n{stdout}\n{stderr}");
    }

    private static string Run(string fileName, string arguments, string? workingDirectory = null)
    {
        int code = RunExit(fileName, arguments, out string stdout, out string stderr, workingDirectory);
        Assert.True(code == 0, $"{fileName} failed ({code}):\n{stdout}\n{stderr}");
        return stdout;
    }

    private static int RunExit(
        string fileName,
        string arguments,
        out string stdout,
        out string stderr,
        string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? RepoRoot,
        };
        using var p = Process.Start(psi)!;
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sbOut.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) sbErr.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        bool exited = p.WaitForExit(600_000);
        if (!exited)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            stdout = sbOut.ToString();
            stderr = sbErr.ToString() + "\n(timeout)";
            return -1;
        }

        p.WaitForExit();
        stdout = sbOut.ToString();
        stderr = sbErr.ToString();
        return p.ExitCode;
    }

    private static string? FindOnPath(string name)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        foreach (string dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            string candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (OperatingSystem.IsWindows())
            {
                foreach (string ext in new[] { ".exe", ".cmd", ".bat" })
                {
                    string withExt = candidate + ext;
                    if (File.Exists(withExt))
                    {
                        return withExt;
                    }
                }
            }
        }

        return null;
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "ForgeHash.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate ForgeHash.sln from test base directory.");
    }
}
