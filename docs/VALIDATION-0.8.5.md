# Polymita 0.8.5 validation — 2026-09-15

## Executed

- Release compilation against the installed Rhino 8.34.26223.11001 SDK: success without warnings.
- CoreTests: 29 passed.
- StorageTests: 39 passed.
- ToolboxTests: 65 passed.
- Regression085Tests.RunGeometry: 16 passed at canvas zoom 0.5, 1 and 2.
- Installer fixture: copied the exact packaged GHA, preserved both previous assemblies and left exactly one active GHA.
- Assembly version: 0.8.5.0. All three embedded Harmony resources present.
- Six icon families rendered from Brand.cs and visually inspected.

## Native checks: compiled, not executed

Regression085Tests.Run covers multi-port grip-to-wire pairing, unconnected port alignment, native Alt copies, group membership, Undo/Redo, Alt autorepeat/toggling and Escape. GestureTests, OperationsTests, IntegrationTests and VisualRegressionTests also compile. Their Rhino execution is pending. Desktop access timed out before inspection; a separate Rhino host did not reach the test script. This release does not claim an interactive/native gesture test pass.

Run `tests/prepare-runtime.ps1 -Name Polymita.Regression085`, then `tests/run-regression-085.py` with Rhino RunPythonScript. Native suites use temporary definitions and restore the original canvas document.

The ToolboxTests default assertion was stale in the supplied archive: 0.8.4 already defaulted to adaptive polylines. The assertion now checks that existing default, without changing the production setting.

## Release binary SHA-256

`c7fa2826e02166fa7bd4c995cf64f75c012120b9a26e3483d1f76b754f5eaac7`

Recorded command output is in `docs/test-results/`. Source-level legacy aliases are limited to migration/installer compatibility and their tests. Old one-off development scripts and obsolete screenshots/validation reports are omitted from the release package; they remain in the supplied 0.8.4 archive.
