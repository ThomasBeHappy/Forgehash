# Security Policy

ForgeHash is experimental cryptographic software. It is not suitable for production password storage.

Readable summary: `website/security.html` (GitHub Pages).

## Reporting

Please report security-relevant findings privately when possible, including:

- algorithmic weaknesses
- memory-hardness bypasses or cheap time-memory trade-offs
- collisions or preimage shortcuts
- implementation vulnerabilities
- side-channel problems
- parser denial-of-service issues
- secret-memory exposure

Open a private advisory or contact the repository maintainer. Do not include real production passwords or private keys in reports.

## Scope notes

- There is no bug bounty or financial reward unless a separate program is announced.
- This project does not claim cryptographic certification or production readiness.
- Issues that only affect research tooling (CLI prompts, visualizer output) should still be reported if they leak secrets.

## Supported versions

Only the latest published source on the default branch is considered current for experimental review.

## AI assistance

AI tools assisted some development and drafting. Design, specs, and most research writing are human-authored. See [`AI.md`](AI.md).
