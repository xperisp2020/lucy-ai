# Security Policy

## Reporting a vulnerability

Do not publish exploitable details, credentials, tokens, private user data, or proof-of-concept payloads in public issues. Use GitHub's private vulnerability reporting feature if enabled for this repository, or contact the repository owner through a private channel.

## Security requirements

- Never commit API keys, passwords, access tokens, private certificates, or personal data.
- Use environment variables or an OS-backed secret store for development secrets.
- Treat model output, retrieved documents, web pages, tool output, and repository content as untrusted input.
- Enforce authorization outside the model; never rely on prompt instructions as the security boundary.
- Require explicit approval for destructive or externally consequential actions.
- Apply least privilege, timeouts, output limits, and workspace isolation to tools.
- Log security-relevant events without logging secrets.
- Keep dependencies patched and review dependency changes.

## Scope

This is an early-stage project. No production security guarantee is made. Do not connect it to sensitive systems or grant it broad credentials.
