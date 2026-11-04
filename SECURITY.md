# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 0.3.x   | Yes                |
| 0.2.x   | Yes                |
| 0.1.x   | Yes                |

## Reporting a Vulnerability

If you discover a security vulnerability in this package, please report it responsibly:

1. **Do NOT open a public GitHub issue.**
2. Email security concerns to the maintainers via the repository's GitHub Security Advisories tab.
3. Go to the [Security Advisories page](https://github.com/kwtsms/kwtsms-csharp/security/advisories) and click "Report a vulnerability."

We will acknowledge your report within 48 hours and provide a timeline for a fix.

## Security Best Practices for Users

- Never hardcode API credentials in source code.
- Use environment variables or `.env` files (gitignored) for credentials.
- Use `KwtSmsClient.FromEnv()` to load credentials safely.
- Enable IP lockdown on your kwtSMS account for production.
- Use a Transactional Sender ID for OTP messages.
- Implement rate limiting and CAPTCHA before going live.
