# Publishing NuGet packages

**Experimental packages only.** Do not present these as production-ready cryptography.

| Package | Project | Version | Notes |
|---------|---------|---------|-------|
| `ForgeHash` | `src/ForgeHash.Core` | `1.0.0-experimental` | B3 / `$forgeh$v=1$` |
| `ForgeHashX` | `src/ForgeHash.X.Core` | `0.1.0-experimental` | X sandbox / `$forgehx$v=0$` |
| `ForgeHash.Cli` | `src/ForgeHash.Cli` | `0.1.0-experimental` | `dotnet tool` → `forgeh` |

Publishing to nuget.org uses **[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)** (GitHub OIDC → short-lived API key). No long-lived NuGet API key is stored in the repo.

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
dotnet tool install -g ForgeHash.Cli --add-source ./artifacts/nuget --version 0.1.0-experimental
forgeh --help
```

## One-time: Trusted Publishing on nuget.org

1. Log into [nuget.org](https://www.nuget.org/) (if you don’t see **Trusted Publishing** yet, the feature may still be rolling out).
2. Username → **Trusted Publishing** → add a policy:
   - **Repository Owner:** `ThomasBeHappy`
   - **Repository:** `Forgehash`
   - **Workflow File:** `nuget.yml` *(filename only — not `.github/workflows/…`)*
   - **Environment:** leave empty (unless you later add `environment:` to the workflow)
   - **Owner:** your nuget.org user (or org that will own the packages)
3. In the GitHub repo **Settings → Secrets and variables → Actions**, add:
   - `NUGET_USER` = your **nuget.org profile name** (not email, not an API key)

## Publish via GitHub Actions

1. Actions → **Publish NuGet** → **Run workflow**
2. Enable **push** = true
3. The job will:
   - pack the three packages
   - exchange a GitHub OIDC token via [`NuGet/login@v1`](https://github.com/NuGet/login)
   - `dotnet nuget push` with the temporary key

Also runs automatically when you publish a GitHub **Release**.

If the policy shows a temporary / 7-day pending state (common for first publish), complete one successful push to fully activate it.

## After nuget.org

```bash
dotnet add package ForgeHash --prerelease
dotnet add package ForgeHashX --prerelease
dotnet tool install -g ForgeHash.Cli --prerelease
```

## Manual push (not preferred)

Only if Trusted Publishing is unavailable for your account. Prefer OIDC.

```bash
dotnet nuget push artifacts/nuget/ForgeHash.1.0.0-experimental.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push artifacts/nuget/ForgeHashX.0.1.0-experimental.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push artifacts/nuget/ForgeHash.Cli.0.1.0-experimental.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
```
