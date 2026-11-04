# Contributing to KwtSMS C#

## Development Setup

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
2. Clone the repository:
   ```bash
   git clone https://github.com/kwtsms/kwtsms-csharp.git
   cd kwtsms-csharp
   ```
3. Restore and build:
   ```bash
   dotnet restore
   dotnet build
   ```
4. Run tests:
   ```bash
   dotnet test --filter "Category!=Integration"
   ```

## Running Integration Tests

Integration tests hit the live kwtSMS API with `test_mode=true` (not delivered; delete the queued test messages from the Sending Queue to release their held credits):

```bash
export CSHARP_USERNAME=csharp_username
export CSHARP_PASSWORD=csharp_password
dotnet test --filter "Category=Integration"
```

## Branch Naming

- `feature/short-description` for new features
- `fix/short-description` for bug fixes
- `docs/short-description` for documentation

## Pull Request Checklist

- [ ] All existing tests pass
- [ ] New tests added for new functionality
- [ ] No hardcoded credentials or sensitive data
- [ ] Code builds without warnings
- [ ] Commit messages are clear and descriptive

## Code Style

- Follow standard C# conventions (PascalCase for public members, camelCase for private)
- Use nullable reference types
- Add XML doc comments on all public API members
- Keep dependencies minimal
