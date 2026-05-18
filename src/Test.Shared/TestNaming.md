# OpenNFS Test Naming

OpenNFS uses Touchstone suite descriptors in `Test.Shared`.

- `Test.Automated` runs the full shared catalog.
- `Test.Xunit` and `Test.Nunit` run the fast adapter-smoke subset built from the unit-tagged cases.

## Naming rules

- Suite identifiers should match the long-term suite families documented in `README.md` whenever possible.
- Case identifiers should be short, stable, and descriptive.
- Display names should explain observable behavior rather than implementation details.
- Category tags should come from `Infrastructure/TestCategories.cs`.

## Current baseline suites

- `RpcXdrSuites`
- `FailureSuites`
- `SecuritySuites`
- `NfsV41Suites`
- `NfsV42Suites`
