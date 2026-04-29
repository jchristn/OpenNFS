# OpenNFS Test Naming

OpenNFS uses Touchstone suite descriptors in `Test.Shared` and exposes the same suite inventory through the automated runner, the xUnit adapter, and the NUnit adapter.

## Naming rules

- Suite identifiers should match the long-term suite families defined in `OPENNFS.md` whenever possible.
- Case identifiers should be short, stable, and descriptive.
- Display names should explain observable behavior rather than implementation details.
- Category tags should come from `Infrastructure/TestCategories.cs`.

## Current baseline suites

- `RpcXdrSuites`

