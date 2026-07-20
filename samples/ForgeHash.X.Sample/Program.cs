using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

Console.WriteLine("ForgeHash-X sample — EXPERIMENTAL sandbox, not for production.");
Console.WriteLine("Not compatible with ForgeHash-B3 ($forgeh$).");
Console.WriteLine();

var parameters = ForgeHashXParameters.Toy;

string password = args.Length > 0 ? args[0] : "correct horse battery staple";
string encoded = ForgeHashXApi.HashPassword(password, parameters);

Console.WriteLine($"encoded : {encoded}");
Console.WriteLine($"verify  : {ForgeHashXApi.VerifyPassword(password, encoded)}");

ParsedForgeHashX parsed = ForgeHashXEncoding.Parse(encoded);
Console.WriteLine($"parsed  : m={parsed.Parameters.MemoryKiB} t={parsed.Parameters.Iterations} p={parsed.Parameters.Parallelism} salt={parsed.Salt.Length}B");

ReadOnlySpan<byte> bytes = "raw-bytes"u8;
byte[] salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
byte[] digest = ForgeHashXApi.DeriveHash(bytes, salt, parameters);
byte[] seed = ForgeHashXApi.ComputeSeed(bytes, salt, parameters);
Console.WriteLine($"seed    : {Convert.ToHexString(seed).ToLowerInvariant()}");
Console.WriteLine($"derive  : {Convert.ToHexString(digest).ToLowerInvariant()}");
