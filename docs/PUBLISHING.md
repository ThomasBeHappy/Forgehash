# Publishing packages

**Experimental packages only.** Do not present these as production-ready cryptography.

All registry publishes prefer **Trusted Publishing** (GitHub OIDC → short-lived credentials). No long-lived API keys in the repo after bootstrap.

| Registry | Packages | Workflow | Trigger |
|----------|----------|----------|---------|
| [nuget.org](https://www.nuget.org/) | `ForgeHash`, `ForgeHashX`, `ForgeHash.Cli` | [`nuget.yml`](../.github/workflows/nuget.yml) | `workflow_dispatch` (push=true) or GitHub Release |
| [PyPI](https://pypi.org/) | `forgeh`, `forgehx` | [`pypi-forgeh.yml`](../.github/workflows/pypi-forgeh.yml) / [`pypi-forgehx.yml`](../.github/workflows/pypi-forgehx.yml) | same |
| [npm](https://www.npmjs.com/) | `forgeh`, `forgehx` | [`npm.yml`](../.github/workflows/npm.yml) | same |
| [crates.io](https://crates.io/) | `forgeh`, `forgehx` | [`crates.yml`](../.github/workflows/crates.yml) | same |

C++ / PHP ports are not published to a language registry (they wrap the Rust C ABI).

Shared GitHub identity for every trusted-publisher policy:

- **Owner / org / user:** `ThomasBeHappy`
- **Repository:** `Forgehash` *(note the lowercase `h`)*

---

## NuGet

| Package | Project | Version | Notes |
|---------|---------|---------|-------|
| `ForgeHash` | `src/ForgeHash.Core` | `1.0.0-experimental` | B3 / `$forgeh$v=1$` |
| `ForgeHashX` | `src/ForgeHash.X.Core` | `0.1.0-experimental` | X sandbox / `$forgehx$v=0$` |
| `ForgeHash.Cli` | `src/ForgeHash.Cli` | `0.1.0-experimental` | `dotnet tool` → `forgeh` |

### One-time: Trusted Publishing on nuget.org

1. [nuget.org](https://www.nuget.org/) → username → **Trusted Publishing** → add a policy:
   - **Repository Owner:** `ThomasBeHappy`
   - **Repository:** `Forgehash`
   - **Workflow File:** `nuget.yml`
   - **Environment:** leave empty
2. GitHub → **Settings → Secrets → Actions** → `NUGET_USER` = your **nuget.org profile name** (not email, not an API key)

### Publish

Actions → **Publish NuGet** → Run workflow → **push** = true  
(or publish a GitHub Release)

### Install

```bash
dotnet add package ForgeHash --prerelease
dotnet add package ForgeHashX --prerelease
dotnet tool install -g ForgeHash.Cli --prerelease
```

---

## PyPI

| Package | Path | Version |
|---------|------|---------|
| `forgeh` | `langs/python/forgeh` | `1.0.0a0` |
| `forgehx` | `langs/python/forgehx` | `0.1.0a0` |

### One-time: Trusted Publishing on PyPI

PyPI allows **only one pending publisher per workflow filename**. That is why
`forgeh` and `forgehx` use **separate** workflows.

1. [pypi.org/manage/account/publishing/](https://pypi.org/manage/account/publishing/)  
   Remove any old pending publisher that still points at `pypi.yml` (or a wrong project name).  
   Also delete any wrongly created empty project under [Your projects](https://pypi.org/manage/projects/).
2. **Add a pending publisher** for each package:

| PyPI project name | Workflow name |
|-------------------|---------------|
| `forgeh` | `pypi-forgeh.yml` |
| `forgehx` | `pypi-forgehx.yml` |

Shared fields for both:

- **Owner:** `ThomasBeHappy`
- **Repository name:** `Forgehash`
- **Environment name:** leave empty

3. No GitHub secrets required

Docs: https://docs.pypi.org/trusted-publishers/

### Publish

Actions → **Publish PyPI (forgeh)** → push = true  
Actions → **Publish PyPI (forgehx)** → push = true

### Install

```bash
pip install forgeh --pre
pip install forgehx --pre
```

### Pack locally

```bash
cd langs/python/forgeh && python -m pip install -U build && python -m build
cd langs/python/forgehx && python -m pip install -U build && python -m build
```

---

## npm

| Package | Path | Version |
|---------|------|---------|
| `forgeh` | `langs/nodejs/forgeh` | `1.0.0-experimental` |
| `forgehx` | `langs/nodejs/forgehx` | `0.1.0-experimental` |

### One-time: Trusted Publishing on npm

Trusted Publisher is configured on an **existing** package. Bootstrap once:

**Option A — temporary token**

1. Create an npm automation / granular token with publish rights
2. GitHub secret `NPM_TOKEN` = that token
3. Actions → **Publish npm** → push = true
4. For each package → **Settings → Trusted Publisher** → GitHub Actions:
   - **Organization or user:** `ThomasBeHappy`
   - **Repository:** `Forgehash`
   - **Workflow filename:** `npm.yml`
   - **Environment:** leave empty
5. Delete `NPM_TOKEN`

**Option B — claim locally**

```bash
cd langs/nodejs/forgeh && npm publish --access public
cd langs/nodejs/forgehx && npm publish --access public
```

Then add Trusted Publisher as above (no `NPM_TOKEN` needed afterward).

Docs: https://docs.npmjs.com/trusted-publishers/

Workflow uses Node 24 + latest npm (≥ 11.5.1 required for OIDC).

### Publish

Actions → **Publish npm** → Run workflow → **push** = true

### Install

```bash
npm install forgeh@experimental
npm install forgehx@experimental
```

---

## crates.io

| Crate | Path | Version |
|-------|------|---------|
| `forgeh` | `langs/rust/forgeh` | `1.0.0-experimental` |
| `forgehx` | `langs/rust/forgehx` | `0.1.0-experimental` |

### One-time: first publish + Trusted Publishing

crates.io **cannot** create a new crate via OIDC. First version must use an API token.

1. [crates.io](https://crates.io/) → Account → API Tokens → create a token with publish scope
2. Either:
   - Local: `cargo login` then  
     `cargo publish --manifest-path langs/rust/forgeh/Cargo.toml`  
     `cargo publish --manifest-path langs/rust/forgehx/Cargo.toml`
   - Or GitHub secret `CARGO_REGISTRY_TOKEN` + Actions → **Publish crates.io** → push = true
3. For **each** crate → Settings → **Trusted Publishing**:
   - **Owner:** `ThomasBeHappy`
   - **Repository:** `Forgehash`
   - **Workflow:** `crates.yml`
   - **Environment:** leave empty
4. Delete `CARGO_REGISTRY_TOKEN` (and revoke the API token if you no longer need it)

Docs: https://crates.io/docs/trusted-publishing

### Publish (after bootstrap)

Actions → **Publish crates.io** → Run workflow → **push** = true

### Install

```bash
cargo add forgeh --precise 1.0.0-experimental
cargo add forgehx --precise 0.1.0-experimental
```

---

## Version bumps

Bump the version in the package manifest before re-publishing (registries reject duplicate versions):

| Registry | Files |
|----------|-------|
| NuGet | `*.csproj` `<Version>` / `<PackageVersion>` |
| PyPI | `langs/python/*/pyproject.toml` → `version` |
| npm | `langs/nodejs/*/package.json` → `version` |
| crates.io | `langs/rust/*/Cargo.toml` → `version` |

Keep experimental / alpha markers until something is intentionally stable.

---

## Name collisions

`forgeh` / `forgehx` may already be taken on a registry. If publish fails with “name already exists” / “owned by another user”, pick a new name (e.g. scoped npm `@thomasbehappy/forgeh`) and update the manifest + docs before retrying.
