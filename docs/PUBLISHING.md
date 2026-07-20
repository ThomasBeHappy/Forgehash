# Publishing NuGet packages

**Experimental packages only.** Do not present these as production-ready cryptography.

| Package | Project | Version | Notes |
|---------|---------|---------|-------|
| `ForgeHash` | `src/ForgeHash.Core` | `1.0.0-experimental` | B3 / `$forgeh$v=1$` |
| `ForgeHashX` | `src/ForgeHash.X.Core` | `0.1.0-experimental` | X sandbox / `$forgehx$v=0$` |
| `ForgeHash.Cli` | `src/ForgeHash.Cli` | `0.1.0-experimental` | `dotnet tool` → `forgeh` |

## Pack locally

```bash
dotnet pack src/ForgeHash.Core/ForgeHash.Core.csproj -c Release -o artifacts/nuget
dotnet pack src/ForgeHash.X.Core/ForgeHash.X.Core.csproj -c Release -o artifacts/nuget
dotnet pack src/ForgeHash.Cli/ForgeHash.Cli.csproj -c Release -o artifacts/nuget
```

Install from the folder:

```bash
dotnet add package ForgeHash --source ./artifacts/nuget --prerelease
dotnet add package ForgeHashX --source ./artifacts/nuget --prerelease

dotnet tool uninstall -g ForgeHash.Cli 2>/dev/null
dotnet tool install -g ForgeHash.Cli --add-source ./artifacts/nuget --prerelease
forgeh --help
```

## Publish to nuget.org

1. Create an API key at https://www.nuget.org/account/apikeys (push new packages / push package versions).
2. Store it as the GitHub Actions secret `NUGET_API_KEY`, **or** push manually:

```bash
dotnet nuget push artifacts/nuget/ForgeHash.1.0.0-experimental.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push artifacts/nuget/ForgeHashX.0.1.0-experimental.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push artifacts/nuget/ForgeHash.Cli.0.1.0-experimental.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
# optional symbols:
dotnet nuget push artifacts/nuget/*.snupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
```

3. Or run the **Publish NuGet** workflow (`.github/workflows/nuget.yml`) via Actions → workflow_dispatch after setting `NUGET_API_KEY`.

## After nuget.org

```bash
dotnet add package ForgeHash --prerelease
dotnet add package ForgeHashX --prerelease
dotnet tool install -g ForgeHash.Cli --prerelease
```
