using ForgeHash;
using ForgeHashApi = global::ForgeHash.ForgeHash;

Console.WriteLine("ForgeHash sample — EXPERIMENTAL, not for production passwords.");
Console.WriteLine();

// Prefer Development only for local demos. Interactive/Sensitive for realistic cost.
var parameters = ForgeHashParameters.Development;

string password = args.Length > 0 ? args[0] : "correct horse battery staple";
string encoded = ForgeHashApi.HashPassword(password, parameters);

Console.WriteLine($"encoded : {encoded}");
Console.WriteLine($"verify  : {ForgeHashApi.VerifyPassword(password, encoded)}");
Console.WriteLine($"rehash? : {ForgeHashApi.NeedsRehash(encoded, ForgeHashParameters.Interactive)}");

if (ForgeHashParser.TryParse(encoded, out ParsedForgeHash? parsed) && parsed is not null)
{
    Console.WriteLine($"parsed  : m={parsed.MemoryKiB} t={parsed.Iterations} p={parsed.Parallelism} salt={parsed.Salt.Length}B");
}

// Exact-byte API (recommended when you already have UTF-8 / raw secrets)
ReadOnlySpan<byte> bytes = "raw-bytes"u8;
byte[] salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
byte[] digest = ForgeHashApi.DeriveHash(bytes, salt, parameters);
Console.WriteLine($"derive  : {Convert.ToHexString(digest).ToLowerInvariant()}");
