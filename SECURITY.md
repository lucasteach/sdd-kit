# Security Policy

## Reporting a vulnerability

Please use **GitHub private vulnerability reporting** — never a public issue :

- Security tab → *Report a vulnerability*
- or directly : https://github.com/lucasteach/sdd-kit/security/advisories/new

No public contact email by design ; private reporting keeps both of us safe.

## Scope & versions

| Version | Supported |
|---|---|
| 1.0.x (latest) | ✅ |
| < 1.0 | ❌ (history rewrite ; upgrade first) |

The CLI writes only inside the project directory, runs `git` in the current
repo, and `sdd adopt` performs outbound HTTP HEAD/GET on URLs found in your
own docs/code (capped, skippable via `SDD_SKIP_NETWORK=1`).

## Response SLA

- **Acknowledgement : ≤ 48 h**
- Assessment & plan : ≤ 7 days
- Fix release : as soon as feasible, coordinated disclosure — public advisory
  at most 90 days after the report if no fix is available.

## Credits

Reporters may be credited in the `CHANGELOG.md` of the fix release — say so in
your report if you prefer anonymity.
