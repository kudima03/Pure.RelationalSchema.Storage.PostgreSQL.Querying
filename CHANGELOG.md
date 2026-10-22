# Changelog

All notable changes to `Pure.RelationalSchema.Storage.PostgreSQL.Querying` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview.0.4.0] - 2026-10-22

### Changed

- Updated the `Pure.Primitives.Choices` dependency from 1.2.2 to 1.3.0.
- Updated the `Pure.Primitives.String.Operations` dependency from 1.5.1 to 1.6.0.

## [0.1.0-preview.0.3.0] - 2026-10-22

### Changed

- Updated the `Pure.Primitives` dependency from 3.6.5 to 3.7.0.

## [0.1.0-preview.0.2.0] - 2026-10-16

### Added

- LINQ queries over stored table datasets of a single schema are now translated to SQL and executed in PostgreSQL instead of being filtered in memory.

### Changed

- `PostgreSqlStoredTableDataSet` now exposes a translating query `Expression` and `Provider`. Query shapes that cannot be translated, and queries spanning several schemas, still run in memory with LINQ to Objects.
- Added a dependency on `Linq.Expressions.Translation.PostgreSql` 0.1.0-preview.0.2.0.

## [0.1.0-preview.0.1.0] - 2026-10-15

### Added

- Initial release: querying (`PostgreSqlStoredSchemaDataSet`, `PostgreSqlStoredTableDataSet`) extracted from `Pure.RelationalSchema.Storage.PostgreSQL` 0.1.0-preview.8.1.2. `IPostgreSqlStoredSchemaDataSet` is provided by the new `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions` package.

### Fixed

- Asynchronous row enumeration no longer disposes the caller-supplied connection.
