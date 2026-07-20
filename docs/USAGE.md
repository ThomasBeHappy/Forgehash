# Using ForgeHash (.NET)

Experimental software. Do not store production passwords with ForgeHash.

## Install

```bash
# nuget.org — experimental prerelease tags
dotnet add package ForgeHash --prerelease
dotnet add package ForgeHashX --prerelease          # optional sandbox
dotnet tool install -g ForgeHash.Cli --prerelease  # `forgeh` command

# Or pack from this repo
dotnet pack src/ForgeHash.Core/ForgeHash.Core.csproj -c Release -o artifacts/nuget
dotnet pack src/ForgeHash.X.Core/ForgeHash.X.Core.csproj -c Release -o artifacts/nuget
dotnet pack src/ForgeHash.Cli/ForgeHash.Cli.csproj -c Release -o artifacts/nuget
dotnet add package ForgeHash --source artifacts/nuget --prerelease
```

- [`ForgeHash`](https://www.nuget.org/packages/ForgeHash/)
- [`ForgeHashX`](https://www.nuget.org/packages/ForgeHashX/)
- [`ForgeHash.Cli`](https://www.nuget.org/packages/ForgeHash.Cli/)

Other languages (PyPI / npm / crates.io): [`../langs/README.md`](../langs/README.md).  
Publisher ops: [`PUBLISHING.md`](PUBLISHING.md). Site hub: `website/developers.html`.

Or reference the project directly while developing:

```bash
dotnet add reference ../path/to/ForgeHash/src/ForgeHash.Core/ForgeHash.Core.csproj
```

## Minimal login flow

```csharp
using ForgeHash;
using ForgeHashApi = ForgeHash.ForgeHash; // avoids namespace/class name clash

// Hash at registration (uses a fresh random salt)
string encoded = ForgeHashApi.HashPassword(passwordBytes, ForgeHashParameters.Interactive);

// Store only `encoded` in the user record.

// Verify at login
bool ok = ForgeHashApi.VerifyPassword(passwordBytes, encoded);
if (!ok)
{
    // Return a generic auth failure to clients.
}

// After a successful login, upgrade weak hashes
if (ForgeHashApi.NeedsRehash(encoded, ForgeHashParameters.Interactive))
{
    string upgraded = ForgeHashApi.HashPassword(passwordBytes, ForgeHashParameters.Interactive);
    // Persist upgraded hash
}
```

## Profiles

| Profile | When to use |
|---------|-------------|
| `ForgeHashParameters.Development` | Local tests / CI only |
| `ForgeHashParameters.Interactive` | Default research interactive cost |
| `ForgeHashParameters.Sensitive` | Higher-cost experiments |

Never silently default to Development in production-shaped builds.

## Pepper (optional)

```csharp
byte[] pepper = /* 32 bytes from a secrets manager */;
string encoded = ForgeHash.HashPassword(passwordBytes, pepper, parameters);
bool ok = ForgeHash.VerifyPassword(passwordBytes, pepper, encoded);
```

The pepper never appears in the encoded string.

## Exact bytes vs strings

- `ReadOnlySpan<byte>` APIs are exact.
- `string` APIs encode UTF-8 and **do not** Unicode-normalize.
- Immutable strings cannot be wiped; prefer byte buffers for secrets when practical.

## Parsing

```csharp
if (!ForgeHashParser.TryParse(encoded, out ParsedForgeHash? parsed) || parsed is null)
{
    // malformed stored hash
}
else
{
    // parsed.MemoryKiB / Iterations / Parallelism / Salt / Hash
}
```

`VerifyPassword` already returns `false` for malformed input.

## Low-level research API

```csharp
byte[] digest = ForgeHash.DeriveHash(password, salt, parameters);
```

This does **not** generate a salt. You must supply one.

## Sample project

```bash
dotnet run --project samples/ForgeHash.Sample -- "demo-password"
```

## Research hooks

```csharp
byte[] seed = ForgeHash.ComputeSeed(password, salt, parameters);
byte[] digest = ForgeHash.DeriveHash(password, salt, parameters);
byte[] parallel = ForgeHash.DeriveHashParallel(password, salt, parameters); // same digest when p>1
```

Mass uniqueness campaigns (Windows GUI):

```bash
dotnet run --project src/ForgeHash.CollisionLab -c Release
```

Notes: [RESEARCH_REPORT.md](RESEARCH_REPORT.md).

## Next

- Porting? [IMPLEMENTING.md](IMPLEMENTING.md)
- Spec? [../SPECIFICATION.md](../SPECIFICATION.md)
- Vectors? [../implementers/v1/](../implementers/v1/)
- Docs site: https://thomasbehappy.github.io/Forgehash/
