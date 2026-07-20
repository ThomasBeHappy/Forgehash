# ForgeHashX (NuGet)

**Experimental.** Not for production password storage. Not compatible with ForgeHash-B3.

| | |
|---|---|
| Package | [`ForgeHashX`](https://www.nuget.org/packages/ForgeHashX/) `0.1.0-experimental` |
| Encoded id | `forgehx` |
| Version | `v=0` (sandbox) |
| Primitive | Custom ForgeX sponge (no BLAKE3) |

```bash
dotnet add package ForgeHashX --prerelease
```

```csharp
using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

string encoded = ForgeHashXApi.HashPassword(password, ForgeHashXParameters.Toy);
bool ok = ForgeHashXApi.VerifyPassword(password, encoded);
```

Docs: https://github.com/ThomasBeHappy/Forgehash — see `docs/forgehx/` and the site **Developers** hub.
