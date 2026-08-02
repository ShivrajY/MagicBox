# Security policy

MagicBox handles untrusted web responses, URLs, credentials, browser sessions,
and optional proxies. Please report security issues responsibly.

## Report a vulnerability

Please use [GitHub private vulnerability reporting](https://github.com/ShivrajY/MagicBox/security/advisories/new)
when available. Do not publish credentials, exploit details, session state, or
private response data in a public issue. If private reporting is unavailable,
contact the repository owner through GitHub before opening a public report.

Include the affected commit or version, reproduction steps, impact, and any
safe mitigation. Do not test against websites or systems you do not own or
have permission to assess.

## Security boundaries

The project currently includes controls for response-size limits, redirect
validation, private-network blocking, URI-scheme restrictions, bounded
pagination, encrypted local secrets, session-state protection, and diagnostic
redaction. These controls are defense-in-depth and do not grant authorization
to scrape a target.

Until a stable release is published, the default branch is the primary
security-maintenance target. Security fixes may change public-preview APIs.
