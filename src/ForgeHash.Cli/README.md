# ForgeHash.Cli

Experimental `forgeh` .NET tool — hash / verify / benchmark / vector for ForgeHash-B3 and ForgeHash-X.

```bash
dotnet tool install -g ForgeHash.Cli --prerelease
forgeh --help
echo -n 'password' | forgeh hash --algo b3 --password-stdin
```

**Not for production password storage.**
