# ForgeHash website

Static documentation for GitHub Pages. The workflow at `.github/workflows/pages.yml` publishes this folder.

## Preview locally

Open `index.html` in a browser, or:

```bash
npx --yes serve website
```

(from the repo root)

## Repo links

`assets/site.js` defaults to `https://github.com/OWNER/ForgeHash`. On `https://<user>.github.io/ForgeHash/` the owner is inferred automatically. For a different repo name or org, set before the script loads:

```html
<script>window.FORGEH = { repo: "https://github.com/you/ForgeHash" };</script>
```

When `OWNER` is still present (local file open), `data-repo` links fall back to `../…` paths in the monorepo.
