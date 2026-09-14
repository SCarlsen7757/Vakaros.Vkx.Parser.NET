## Build & Test

```bash
# Build
dotnet build --configuration Release

# Pack (creates .nupkg in ./nupkg/)
dotnet pack --configuration Release --output ./nupkg

# Build with explicit version (as done in CI)
dotnet build --configuration Release /p:Version=1.0.0
```

No automated test suite exists yet. Manual testing against `.vkx` sample files is the current approach.

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

- **`VkxParser.cs`** — static parser class. Entry point for all parsing. Reads the binary stream record-by-record using a `BinaryReader`. All values are little-endian.
- **`VkxSession.cs`** — result object returned by the parser. Exposes typed `IEnumerable<T>` convenience properties for each record type.
- **`Models/`** — one file per record type. All model types are `record` types inheriting from `VkxRecord`.
- **`vkx_format.md`** — the VKX 1.4 binary format specification. Refer to this when adding support for new record types.

### Format Overview

VKX files are a sequence of fixed-size rows. Each row starts with a 1-byte key identifying the record type. The parser uses a lookup table (`PayloadSizes`) to know how many bytes to read for each key, and a `switch` expression (`ParseRecord`) to deserialize each type.

Unknown keys throw a `FormatException`. Internal Vakaros message types (0x01, 0x07, 0x0E, 0x20, 0x21) are silently skipped.

---

## NuGet Publish Workflow

Versioning is handled by **GitVersion** (ContinuousDelivery mode). Version is injected at build time — never hardcoded in the `.csproj`.

- **Pull request** → `ci.yml` builds and packs (no publish).
- **Push to `main`** → `publish.yml` builds, packs, and pushes to nuget.org using the `NUGET_API_KEY` repository secret.

---

## Conventions

- **No external dependencies** — the parser must stay dependency-free. Do not add any NuGet references.
- **All model types are `record`** — immutable, value-equality semantics.
- **Units: raw, SI, imperial** — each measured value is stored once as an `internal` `Raw<Name>` property in the unit the spec records it in. Public properties are computed from it with an explicit unit suffix in two systems: SI (`…MetresPerSecond`, `…Radians`, `…Metres`, `…Celsius`) and imperial/nautical (`…Knots`, `…Degrees`, `…Feet`, `…Fahrenheit`). All conversions live in `UnitConversions`. Never expose a unit-less measured property.
- **`FormatVersion`** — the VKX format version byte from the first page header is exposed on `VkxSession.FormatVersion`. A later page header with a different version is a `FormatException`.
- **Error contract** — corrupt data throws `FormatException`; versions older than 1.4 throw `VkxUnsupportedVersionException`; truncation or unknown keys in a newer version set `VkxSession.IsPartial`. Never let `EndOfStreamException` or `ArgumentOutOfRangeException` escape.
- **Internal records** — read and discard internal message payloads (keys 0x01, 0x07, 0x0E, 0x20, 0x21). They return `null` from `ParseRecord` and are not added to the records list.
