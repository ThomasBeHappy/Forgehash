using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ForgeHash;
using ForgeHashX;
using ForgeHashApi = ForgeHash.ForgeHash;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

var root = new RootCommand(
    "ForgeHash experimental password hashing CLI (B3 and X). Not for production use.");

var algoOption = new Option<string>(
    "--algo",
    () => "b3",
    "Algorithm: b3 (forgeh v1) or x (forgehx v0 sandbox).")
{ ArgumentHelpName = "b3|x" };

var memoryOption = new Option<int?>("--memory", "Memory cost in KiB (defaults: B3 65536, X 1024).");
var iterationsOption = new Option<int?>("--iterations", "Number of passes (defaults: B3 3, X 1).");
var parallelismOption = new Option<int?>("--parallelism", "Lane count (default 1).");
var saltLengthOption = new Option<int?>("--salt-length", "Salt length in bytes (default 16).");
var passwordStdinOption = new Option<bool>("--password-stdin", () => false, "Read password from stdin (testing only).");

var hashCommand = new Command("hash", "Hash a password and print the encoded result.");
hashCommand.AddOption(algoOption);
hashCommand.AddOption(memoryOption);
hashCommand.AddOption(iterationsOption);
hashCommand.AddOption(parallelismOption);
hashCommand.AddOption(saltLengthOption);
hashCommand.AddOption(passwordStdinOption);
hashCommand.SetHandler(
    (algo, memory, iterations, parallelism, saltLength, passwordStdin) =>
    {
        string password = passwordStdin ? ReadPasswordFromStdin() : ReadPasswordSecurely("Password: ");
        try
        {
            if (IsX(algo))
            {
                var p = MakeXParams(memory, iterations, parallelism, saltLength);
                Console.WriteLine(ForgeHashXApi.HashPassword(password, p));
            }
            else
            {
                var p = MakeB3Params(memory, iterations, parallelism, saltLength);
                Console.WriteLine(ForgeHashApi.HashPassword(password, p));
            }
        }
        finally
        {
        }
    },
    algoOption,
    memoryOption,
    iterationsOption,
    parallelismOption,
    saltLengthOption,
    passwordStdinOption);

var encodedArgument = new Argument<string>("encoded-hash", "Encoded ForgeHash string to verify.");
var verifyCommand = new Command("verify", "Verify a password against an encoded hash (auto-detects b3/x).");
verifyCommand.AddArgument(encodedArgument);
verifyCommand.AddOption(passwordStdinOption);
verifyCommand.SetHandler(
    (encoded, passwordStdin) =>
    {
        bool looksX = encoded.StartsWith("$forgehx$", StringComparison.Ordinal);
        bool looksB3 = encoded.StartsWith("$forgeh$", StringComparison.Ordinal);
        if (!looksX && !looksB3)
        {
            Console.Error.WriteLine("Malformed hash.");
            Environment.ExitCode = 2;
            return;
        }

        string password = passwordStdin ? ReadPasswordFromStdin() : ReadPasswordSecurely("Password: ");
        try
        {
            bool ok = looksX
                ? ForgeHashXApi.VerifyPassword(password, encoded)
                : ForgeHashApi.VerifyPassword(password, encoded);
            if (ok)
            {
                Console.WriteLine("Password valid.");
                Environment.ExitCode = 0;
            }
            else
            {
                Console.WriteLine("Password invalid.");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Internal error: {ex.Message}");
            Environment.ExitCode = 3;
        }
    },
    encodedArgument,
    passwordStdinOption);

var samplesOption = new Option<int>("--samples", () => 5, "Timed samples.");
var warmupOption = new Option<int>("--warmup", () => 1, "Warmup iterations.");
var benchmarkCommand = new Command("benchmark", "Benchmark ForgeHash with the given parameters.");
benchmarkCommand.AddOption(algoOption);
benchmarkCommand.AddOption(memoryOption);
benchmarkCommand.AddOption(iterationsOption);
benchmarkCommand.AddOption(parallelismOption);
benchmarkCommand.AddOption(samplesOption);
benchmarkCommand.AddOption(warmupOption);
benchmarkCommand.SetHandler(
    (algo, memory, iterations, parallelism, samples, warmup) =>
    {
        byte[] password = "benchmark-password"u8.ToArray();
        byte[] salt = new byte[16];
        Random.Shared.NextBytes(salt);
        int mem = memory ?? (IsX(algo) ? 1024 : ForgeHashParameters.DefaultMemoryKiB);
        int it = iterations ?? (IsX(algo) ? 1 : ForgeHashParameters.DefaultIterations);
        int par = parallelism ?? 1;

        Func<byte[]> derive = IsX(algo)
            ? () => ForgeHashXApi.DeriveHash(password, salt, new ForgeHashXParameters
            {
                MemoryKiB = mem,
                Iterations = it,
                Parallelism = par,
                OutputLength = 32,
                SaltLength = 16,
            })
            : () => ForgeHashApi.DeriveHash(password, salt, new ForgeHashParameters
            {
                MemoryKiB = mem,
                Iterations = it,
                Parallelism = par,
            });

        for (int i = 0; i < warmup; i++)
        {
            _ = derive();
        }

        var times = new List<double>(samples);
        long peakWorkingSet = 0;
        for (int i = 0; i < samples; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw = Stopwatch.StartNew();
            _ = derive();
            sw.Stop();
            peakWorkingSet = Math.Max(peakWorkingSet, Process.GetCurrentProcess().WorkingSet64);
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        double average = times.Average();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"algo={(IsX(algo) ? "x" : "b3")}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"average_ms={average:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"minimum_ms={times[0]:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"maximum_ms={times[^1]:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"median_ms={times[times.Count / 2]:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"allocated_memory_kib={mem}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"peak_working_set_bytes={peakWorkingSet}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"operations_per_second={1000.0 / average:F3}"));
    },
    algoOption,
    memoryOption,
    iterationsOption,
    parallelismOption,
    samplesOption,
    warmupOption);

var passwordHexOption = new Option<string>("--password-hex", () => "", "Password bytes as hex.") { IsRequired = true };
var saltHexOption = new Option<string>("--salt-hex", () => "", "Salt bytes as hex.") { IsRequired = true };
var fullOption = new Option<bool>("--full", () => false, "Emit a full intermediate snapshot as JSON (B3 only).");
var vectorCommand = new Command("vector", "Generate a reproducible test-vector dump.");
vectorCommand.AddOption(algoOption);
vectorCommand.AddOption(passwordHexOption);
vectorCommand.AddOption(saltHexOption);
vectorCommand.AddOption(memoryOption);
vectorCommand.AddOption(iterationsOption);
vectorCommand.AddOption(parallelismOption);
vectorCommand.AddOption(fullOption);
vectorCommand.SetHandler(
    (algo, passwordHex, saltHex, memory, iterations, parallelism, full) =>
    {
        byte[] password = Convert.FromHexString(passwordHex);
        byte[] salt = Convert.FromHexString(saltHex);

        if (IsX(algo))
        {
            if (full)
            {
                Console.Error.WriteLine("--full snapshots are only implemented for B3.");
                Environment.ExitCode = 2;
                return;
            }

            var p = MakeXParams(memory, iterations, parallelism, salt.Length);
            byte[] seed = ForgeHashXApi.ComputeSeed(password, salt, p);
            byte[] hash = ForgeHashXApi.DeriveHash(password, salt, p);
            string encoded = ForgeHashXEncoding.Encode(p, salt, hash);
            WriteVectorDump(password, salt, p.MemoryKiB, p.Iterations, p.Parallelism, seed, hash, encoded);
            return;
        }

        var parameters = MakeB3Params(memory, iterations, parallelism, salt.Length);
        if (full)
        {
            TestVectorSnapshot snapshot = ForgeHashTestVectors.Generate("cli-vector", password, salt, parameters);
            Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        byte[] b3Seed = ForgeHashApi.ComputeSeed(password, salt, parameters);
        byte[] b3Hash = ForgeHashApi.DeriveHash(password, salt, parameters);
        string b3Encoded = ForgeHashEncoding.Encode(
            1, parameters.MemoryKiB, parameters.Iterations, parameters.Parallelism, salt, b3Hash);
        WriteVectorDump(
            password, salt, parameters.MemoryKiB, parameters.Iterations, parameters.Parallelism,
            b3Seed, b3Hash, b3Encoded);
    },
    algoOption,
    passwordHexOption,
    saltHexOption,
    memoryOption,
    iterationsOption,
    parallelismOption,
    fullOption);

root.AddCommand(hashCommand);
root.AddCommand(verifyCommand);
root.AddCommand(benchmarkCommand);
root.AddCommand(vectorCommand);

return await root.InvokeAsync(args);

static bool IsX(string algo)
{
    string a = algo.Trim().ToLowerInvariant();
    return a is "x" or "forgehx" or "forgehash-x" or "forgehashx";
}

static ForgeHashParameters MakeB3Params(int? memory, int? iterations, int? parallelism, int? saltLength)
    => new()
    {
        MemoryKiB = memory ?? ForgeHashParameters.DefaultMemoryKiB,
        Iterations = iterations ?? ForgeHashParameters.DefaultIterations,
        Parallelism = parallelism ?? ForgeHashParameters.DefaultParallelism,
        SaltLength = saltLength ?? ForgeHashParameters.DefaultSaltLength,
    };

static ForgeHashXParameters MakeXParams(int? memory, int? iterations, int? parallelism, int? saltLength)
    => new()
    {
        MemoryKiB = memory ?? 1024,
        Iterations = iterations ?? 1,
        Parallelism = parallelism ?? 1,
        OutputLength = 32,
        SaltLength = saltLength ?? 16,
    };

static void WriteVectorDump(
    byte[] password,
    byte[] salt,
    int memory,
    int iterations,
    int parallelism,
    byte[] seed,
    byte[] hash,
    string encoded)
{
    Console.WriteLine($"password_hex={Convert.ToHexString(password).ToLowerInvariant()}");
    Console.WriteLine($"salt_hex={Convert.ToHexString(salt).ToLowerInvariant()}");
    Console.WriteLine($"memory_kib={memory}");
    Console.WriteLine($"iterations={iterations}");
    Console.WriteLine($"parallelism={parallelism}");
    Console.WriteLine($"seed_hex={Convert.ToHexString(seed).ToLowerInvariant()}");
    Console.WriteLine($"hash_hex={Convert.ToHexString(hash).ToLowerInvariant()}");
    Console.WriteLine($"encoded={encoded}");
}

static string ReadPasswordFromStdin()
{
    using Stream input = Console.OpenStandardInput();
    using var reader = new StreamReader(input, Encoding.UTF8);
    return reader.ReadLine() ?? string.Empty;
}

static string ReadPasswordSecurely(string prompt)
{
    Console.Error.Write(prompt);
    var buffer = new StringBuilder();
    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.Error.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (buffer.Length > 0)
            {
                buffer.Length--;
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            buffer.Append(key.KeyChar);
        }
    }

    return buffer.ToString();
}
