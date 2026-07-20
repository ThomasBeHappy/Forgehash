# ForgeHash website

Static documentation for GitHub Pages. The workflow at `.github/workflows/pages.yml` publishes **this folder**.

## Enable on GitHub

1. Repo **Settings → Pages**
2. Under **Build and deployment → Source**, choose **GitHub Actions** (not “Deploy from a branch” / `/docs`)
3. Push to `main` (or run the **Deploy docs** workflow manually)

If Source is set to the `/docs` folder, GitHub will publish the markdown under `docs/` instead of this site.

## Audience hubs

- `developers.html` — install, languages, vectors, implementer path
- `researchers.html` — specs, campaigns, benches, X sandbox, security

Top nav groups pages under **Developers** / **Researchers**. Homepage CTAs point at those hubs.

## Preview locally

```bash
npx --yes serve website
```

(from the repo root)

