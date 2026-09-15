# Package contents

- `release/Polymita.gha`: compiled plugin, with embedded Harmony dependencies.
- `Install-Polymita.ps1`: installer with backup of earlier known assemblies.
- `src/`: complete C# source.
- `Polymita.csproj`, `build.ps1`: build entry points.
- `lib/`: Harmony binaries used to reproduce the build.
- `tests/`: regression tests and their runners.
- `docs/`: reference documentation, release validation and six current icon families.
- `README.md`, `SOURCE-README.md`, `CAMBIOS-*.md`: usage, build and review notes.
- `COPYING`, `THIRD_PARTY_NOTICES.md`, `docs/LICENSE-*.txt`: licenses and attribution.

Generated build directories and temporary test fixtures are excluded from the ZIP. Rhino/Grasshopper SDK binaries are not redistributed.
