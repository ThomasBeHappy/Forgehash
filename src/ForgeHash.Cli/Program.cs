using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ForgeHash;
using ForgeHashApi = ForgeHash.ForgeHash;

var root = new RootCommand("ForgeHash-B3 experimental password hashing CLI. Not for production use.");

var memoryOption = new Option<int>("--memory", () => ForgeHashParameters.DefaultMemoryKiB, "Memory cost in KiB.");
var iterationsOption = new Option<int>("--iterations", () => ForgeHashParameters.DefaultIterations, "Number of passes.");
var parallelismOption = new Option<int>("--parallelism", () => ForgeHashParameters.DefaultParallelism, "Lane count.");
var saltLengthOption = new Option<int>("--salt-length", () => ForgeHashParameters.DefaultSaltLength, "Salt length in bytes.");
var passwordStdinOption = new Option<bool>("--password-stdin", () => false, "Read password from stdin (testing only).");

var hashCommand = new Command("hash", "Hash a password and print the encoded result.");
hashCommand.AddOption(memoryOption);
hashCommand.AddOption(iterationsOption);
hashCommand.AddOption(parallelismOption);
hashCommand.AddOption(saltLengthOption);
hashCommand.AddOption(passwordStdinOption);
hashCommand.SetHandler(
    (memory, iterations, parallelism, saltLength, passwordStdin) =>
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = memory,
            Iterations = iterations,
            Parallelism = parallelism,
            SaltLength = saltLength,
        };

        string password = passwordStdin ? ReadPasswordFromStdin() : ReadPasswordSecurely("Password: ");
        try
        {
            string encoded = ForgeHashApi.HashPassword(password, parameters);
            Console.WriteLine(encoded);
        }
        finally
        {
            // Best-effort: strings are immutable and cannot be cleared.
        }
    },
    memoryOption,
    iterationsOption,
    parallelismOption,
    saltLengthOption,
    passwordStdinOption);

var encodedArgument = new Argument<string>("encoded-hash", "Encoded ForgeHash string to verify.");
var verifyCommand = new Command("verify", "Verify a password against an encoded hash.");
verifyCommand.AddArgument(encodedArgument);
verifyCommand.AddOption(passwordStdinOption);
verifyCommand.SetHandler(
    (encoded, passwordStdin) =>
    {
        if (!ForgeHashParser.TryParse(encoded, out _))
        {
            Console.Error.WriteLine("Malformed hash.");
            Environment.ExitCode = 2;
            return;
        }

        string password = passwordStdin ? ReadPasswordFromStdin() : ReadPasswordSecurely("Password: ");
        try
        {
            bool ok = ForgeHashApi.VerifyPassword(password, encoded);
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
benchmarkCommand.AddOption(memoryOption);
benchmarkCommand.AddOption(iterationsOption);
benchmarkCommand.AddOption(parallelismOption);
benchmarkCommand.AddOption(samplesOption);
benchmarkCommand.AddOption(warmupOption);
benchmarkCommand.SetHandler(
    (memory, iterations, parallelism, samples, warmup) =>
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = memory,
            Iterations = iterations,
            Parallelism = parallelism,
        };

        byte[] password = "benchmark-password"u8.ToArray();
        byte[] salt = new byte[16];
        Random.Shared.NextBytes(salt);

        for (int i = 0; i < warmup; i++)
        {
            _ = ForgeHashApi.DeriveHash(password, salt, parameters);
        }

        var times = new List<double>(samples);
        long peakWorkingSet = 0;
        for (int i = 0; i < samples; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetTotalMemory(forceFullCollection: true);
            var sw = Stopwatch.StartNew();
            _ = ForgeHashApi.DeriveHash(password, salt, parameters);
            sw.Stop();
            long after = GC.GetTotalMemory(forceFullCollection: false);
            peakWorkingSet = Math.Max(peakWorkingSet, Process.GetCurrentProcess().WorkingSet64);

            times.Add(sw.Elapsed.TotalMilliseconds);
            _ = after - before;
        }

        times.Sort();
        double average = times.Average();
        double min = times[0];
        double max = times[^1];
        double median = times[times.Count / 2];
        double allocatedMiB = memory / 1024.0;
        double bandwidth = (allocatedMiB * iterations * 2) / (average / 1000.0); // rough read+write estimate
        double ops = 1000.0 / average;

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"average_ms={average:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"minimum_ms={min:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"maximum_ms={max:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"median_ms={median:F3}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"allocated_memory_kib={memory}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"peak_working_set_bytes={peakWorkingSet}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"effective_memory_bandwidth_mib_s={bandwidth:F2}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"operations_per_second={ops:F3}"));
    },
    memoryOption,
    iterationsOption,
    parallelismOption,
    samplesOption,
    warmupOption);

var passwordHexOption = new Option<string>("--password-hex", () => "", "Password bytes as hex.") { IsRequired = true };
var saltHexOption = new Option<string>("--salt-hex", () => "", "Salt bytes as hex.") { IsRequired = true };
var fullOption = new Option<bool>("--full", () => false, "Emit a full intermediate snapshot as JSON.");
var vectorCommand = new Command("vector", "Generate a reproducible test-vector dump.");
vectorCommand.AddOption(passwordHexOption);
vectorCommand.AddOption(saltHexOption);
vectorCommand.AddOption(memoryOption);
vectorCommand.AddOption(iterationsOption);
vectorCommand.AddOption(parallelismOption);
vectorCommand.AddOption(fullOption);
vectorCommand.SetHandler(
    (passwordHex, saltHex, memory, iterations, parallelism, full) =>
    {
        byte[] password = Convert.FromHexString(passwordHex);
        byte[] salt = Convert.FromHexString(saltHex);
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = memory,
            Iterations = iterations,
            Parallelism = parallelism,
        };

        if (full)
        {
            TestVectorSnapshot snapshot = ForgeHashTestVectors.Generate("cli-vector", password, salt, parameters);
            Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        byte[] seed = ForgeHashApi.ComputeSeed(password, salt, parameters);
        byte[] hash = ForgeHashApi.DeriveHash(password, salt, parameters);
        string encoded = ForgeHashEncoding.Encode(1, memory, iterations, parallelism, salt, hash);

        Console.WriteLine($"password_hex={Convert.ToHexString(password).ToLowerInvariant()}");
        Console.WriteLine($"salt_hex={Convert.ToHexString(salt).ToLowerInvariant()}");
        Console.WriteLine($"memory_kib={memory}");
        Console.WriteLine($"iterations={iterations}");
        Console.WriteLine($"parallelism={parallelism}");
        Console.WriteLine($"seed_hex={Convert.ToHexString(seed).ToLowerInvariant()}");
        Console.WriteLine($"hash_hex={Convert.ToHexString(hash).ToLowerInvariant()}");
        Console.WriteLine($"encoded={encoded}");
    },
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
