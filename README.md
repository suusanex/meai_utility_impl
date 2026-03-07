# meai_utility_impl
Microsoft.Extensions.AI準拠のI/Fを持ち、自作ソフトウェアで共通使用出来るような実装側を入れた、便利ライブラリ

## Semantic Versioning Policy

- This repository follows Semantic Versioning (`MAJOR.MINOR.PATCH`) per NuGet package.
- Breaking public API changes require a MAJOR version bump.
- New backward-compatible features require a MINOR version bump.
- Bug fixes and internal-only changes require a PATCH version bump.

### Release Checklist

1. Run `dotnet restore`.
2. Run `dotnet build -warnaserror` for `net8.0` and `net10.0`.
3. Run `dotnet test` for `net8.0` and `net10.0`.
4. Confirm no secrets are present in tracked files.
5. Verify package versions and release notes before publishing.
