## Build & Test

```bash
# Build
dotnet build --configuration Release

# Pack (creates .nupkg in ./nupkg/)
dotnet pack --configuration Release --output ./nupkg

# Build with explicit version (as done in CI)
dotnet build --configuration Release -p:Version=0.2.0 -p:ContinuousIntegrationBuild=true

# Test
dotnet test --configuration Release
```

Tests live in `Vakaros.Vkx.Parser.NET.Tests` (xUnit). Most build payloads with `BinaryWriter`; `TestData/*.vkx` is a real VKX 1.4 recording embedded as a resource. Add a test for every parser fix.

### Package validation

`dotnet pack` validates the public API against the last released package (`PackageValidationBaselineVersion`). An unintended break fails the build. For an intentional break, regenerate `Vakaros.Vkx.Parser.NET/CompatibilitySuppressions.xml` with `dotnet pack -p:ApiCompatGenerateSuppressionFile=true`, label the PR `breaking change`, and after each release bump the baseline to the new version and delete the suppression file.

---

## Architecture

This is a **pure, zero-dependency** .NET library. No NuGet packages are referenced.

```
VkxParser (static)
    └── Parse(Stream | byte[] | filePath) → VkxSession
            └── Records: IReadOnlyList<VkxRecord>
                    ├── PositionRecord
                    ├── WindRecord
                    ├── RaceTimerEventRecord
                    └── ... (see Models/)
```

### Key Files

- **`VkxParser.cs`** — static parser class. Entry point for all parsing. Reads each row's full fixed-size payload into a stack buffer and decodes it with `BinaryPrimitives` (little-endian). Every overload has a `CancellationToken` variant.
- **`VkxSession.cs`** — result object returned by the parser. Groups records by type once at construction and exposes typed `IReadOnlyList<T>` properties for each record type.
- **`UnitConversions.cs`** — the only place unit conversion constants live.
- **`Models/`** — one file per record type. All model types are `record` types inheriting from `VkxRecord`.
- **`vkx_format.md`** — the VKX 1.4 binary format specification. Refer to this when adding support for new record types.

### Format Overview

VKX files are a sequence of fixed-size rows. Each row starts with a 1-byte key identifying the record type. The parser uses a lookup table (`PayloadSizes`) to know how many bytes to read for each key, and a `switch` expression (`ParseRecord`) to deserialize each type.

Unknown keys throw a `FormatException` (or end parsing as partial in a newer format version). Internal Vakaros message types (0x01, 0x07, 0x0E, 0x20, 0x21) are silently skipped.

---

## Versioning & Publishing

The library is in **beta (0.x)**. A `breaking change` bumps the **minor** version (0.2.0 → 0.3.0); fixes and non-breaking features bump the patch. After 1.0.0, breaking changes bump the major version. The version is passed at build time — never hardcoded in the `.csproj`.

- **Pull request / push to `main`** → `ci.yml` builds, tests and packs as `<baseline>-ci.<run number>` (e.g. `0.1.0-ci.12`), no publish. Package validation rejects an assembly version below the baseline, so never use a placeholder like `0.0.0`.
- **Release** → run `publish.yml` manually from `main` with the version (e.g. `0.2.0`). It checks the version is SemVer and untagged, builds, tests, packs, pushes to nuget.org via NuGet trusted publishing (OIDC), then creates tag `v<version>` and a GitHub Release with generated notes. 0.x and `-suffix` versions are marked pre-release.
- Third-party actions are pinned to commit SHAs with a version comment; Dependabot proposes updates.

---

## Labels

Flat **type + scope + meta** scheme (shared with SailSight). Release notes are grouped by these labels via `.github/release.yml`.

- **Type — exactly one:** `bug`, `security`, `feature`, `performance`, `refactor`, `documentation`, `chore`.
- **Scope — every one the change touches:** `library` (`Vakaros.Vkx.Parser.NET/**`), `tests` (`Vakaros.Vkx.Parser.NET.Tests/**`), `infra` (`.github/**`, packaging), `format` (VKX spec compliance: record layouts, units, versions, `vkx_format.md`).
- **Meta — when it applies:** `breaking change` (public API break), `dependencies`, `blocked`, `needs info`, `upstream` (waiting on Vakaros to clarify the official spec). `good first issue` and `help wanted` are for issues only.

---

## Conventions

- **No external dependencies** — the parser must stay dependency-free. Do not add any NuGet references.
- **All model types are `record`** — immutable, value-equality semantics.
- **Units: raw, SI, imperial** — each measured value is stored once as an `internal` `Raw<Name>` property in the unit the spec records it in. Public properties are computed from it with an explicit unit suffix in two systems: SI (`…MetresPerSecond`, `…Radians`, `…Metres`, `…Celsius`) and imperial/nautical (`…Knots`, `…Degrees`, `…Feet`, `…Fahrenheit`). All conversions live in `UnitConversions`. Never expose a unit-less measured property.
- **`FormatVersion`** — the VKX format version byte from the first page header is exposed on `VkxSession.FormatVersion`. A later page header with a different version is a `FormatException`.
- **Error contract** — corrupt data throws `FormatException`; versions older than 1.4 throw `VkxUnsupportedVersionException`; truncation or unknown keys in a newer version set `VkxSession.IsPartial`. Never let `EndOfStreamException` or `ArgumentOutOfRangeException` escape.
- **Internal records** — read and discard internal message payloads (keys 0x01, 0x07, 0x0E, 0x20, 0x21). They return `null` from `ParseRecord` and are not added to the records list.
