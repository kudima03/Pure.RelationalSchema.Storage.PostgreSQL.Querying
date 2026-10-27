# Changelog

All notable changes to `Pure.RelationalSchema.Storage.PostgreSQL.Querying` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Cells of `date`, `time`, `double`, `float` and `determined_hash` columns are translated to the text enumeration gives them (`MM/dd/yyyy`, `HH:mm`, the shortest round-trip text of a number, the bytes as UTF-8 text); the exception for a column of an unknown type names the column and its type.
- Results holding `DateTime`, `DateOnly`, `TimeOnly` and `TimeSpan` values (and their nullable forms) are read from `timestamp`, `date`, `time` and `interval` results.
- `DistinctBy` is translated and its elements are read; the elements of `ExceptBy`, `IntersectBy`, `UnionBy`, `MinBy` and `MaxBy` are read too, though the translator still rejects those operators.
- One row of several aggregates over stored tables is read in one statement without `FROM` from a one-element in-memory root (`new[] { 0 }.AsQueryable().Select(_ => new { Orders = orders.LongCount(), ... })`).
- The results of `GroupJoin` and of set operations (`Union`, `Concat`, ...) that build objects are read.

### Fixed

- A NULL cell is read as empty text in a translated query, as it is when enumerated, so NULL cells compare, join, group and order like empty text (`COALESCE(..., '')`) instead of as SQL `NULL` (a NULL `bool` cell was `False`).

### Changed

- Updated the `Linq.Expressions.Translation.PostgreSql` dependency from 0.1.0-preview.0.4.0 to 0.1.0-preview.0.5.1.

## [0.1.0-preview.0.6.0] - 2026-10-26

### Added

- Queries over tables of several schemas of one connection (joins, correlated subqueries, set operations of scalars) are translated to one SQL statement instead of being split into one statement per schema and combined in memory.
- Cells of `datetime` columns are translated to their invariant text (`MM/dd/yyyy HH:mm:ss`).
- `row.Cells.Count` and `row.Cells.Keys` are translated as the columns of the row's table.
- Rows are read from projections: `Select`, `Join` and `SelectMany` results that are rows, and constructed objects (anonymous types, value tuples, ...) holding rows.
- `Cast<IRow>` and `OfType<IRow>` are translated.

### Changed

- The package is Native AOT compatible (`IsAotCompatible`): the entity types the translator maps are built from reflection objects instead of Reflection.Emit, and results are no longer executed through `EnumerableQuery`.
- A set operation over rows of different tables throws `NotSupportedException` instead of reaching PostgreSQL.
- A query that cannot run as one SQL statement — over several connections or other sources, or using an operator, member or result shape that cannot be translated — throws `NotSupportedException` (with the translator's exception as `InnerException` when the translator refused) instead of running partly in memory with LINQ to Objects.
- `IQueryProvider.Execute<TResult>` with a sequence result type returns rows and untyped sequences; other sequences are read by enumerating the query.
- Updated the `Linq.Expressions.Translation.PostgreSql` dependency from 0.1.0-preview.0.2.1 to 0.1.0-preview.0.4.0.

## [0.1.0-preview.0.5.0] - 2026-10-23

### Changed

- Updated the `Linq.Expressions.Translation.PostgreSql` dependency from 0.1.0-preview.0.2.0 to 0.1.0-preview.0.2.1.
- Updated the `Pure.Primitives.String.Operations` dependency from 1.6.0 to 1.7.0.

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
