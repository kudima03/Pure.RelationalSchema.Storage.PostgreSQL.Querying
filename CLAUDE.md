# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All `dotnet` commands must be run from the `./src` directory.

```bash
dotnet restore
dotnet build --no-restore -warnaserror
dotnet format --verify-no-changes             # check code style (CI enforces this)
dotnet format                                 # auto-fix code style
dotnet test --no-build --verbosity normal     # run integration tests (requires Docker for Testcontainers)
dotnet pack --configuration Release -p:Version=<version> --output .
```

CI additionally passes `-p:AssemblyVersion` (pinned to the major) and `-p:FileVersion`; see `.github/workflows/publish-nuget.yml`.

## Architecture

This is the **querying (read)** part of the PostgreSQL storage implementation for the Pure relational schema ecosystem. It has no public abstractions of its own — it provides concrete types that implement interfaces from `Pure.RelationalSchema.Storage` and `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`. Creating tables and inserting rows live in the sibling packages `Pure.RelationalSchema.Storage.PostgreSQL.Creation` and `Pure.RelationalSchema.Storage.PostgreSQL.Insertion`.

**Key relationships:**

- `PostgreSqlStoredSchemaDataSet` is a dictionary from `ITable` → `IStoredTableDataSet`, where each entry is a `PostgreSqlStoredTableDataSet` backed by a `SELECT` of all columns. It implements `IPostgreSqlStoredSchemaDataSet` (adds the `IDbConnection Connection` property), which is **not** defined here: it comes from the `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions` package, so sibling packages can share it without depending on this one.
- `PostgreSqlStoredTableDataSet` implements both `IQueryable<IRow>` and `IAsyncEnumerable<IRow>`. Enumerating it reads `RowsEnumerable` / `RowsAsyncEnumerable`; its `Expression` is `Constant(this)` typed `IQueryable<IRow>` and its `Provider` is the internal `PostgreSqlQueryProvider`.
- **Query translation** (all internal): `PostgreSqlQueryProvider.Execute` inlines captured queries (`CapturedQueries`), then `SchemaSubqueries` visits the tree top-down and asks `SchemaQuery` to run each `Queryable` operator without free parameters whose roots are all `PostgreSqlStoredTableDataSet`s of one schema (same `IDbConnection` reference and same schema name text). `SchemaQuery` rewrites the subtree with `EntityExpression` (each table becomes a `TableRoot` whose element type is an `EntityType` emitted with Reflection.Emit, with `[Table]`/`[Column]` hash names; `row.Cells[column].Value.TextValue` becomes the text of the column property, see `ColumnProperty`), translates it with `PostgreSqlQuery` from `Linq.Expressions.Translation.PostgreSql`, executes it and reads the result by position (`ResultReader`). Any failure (unsupported shape, translator `NotSupportedException`, width mismatch) returns `null` and the visitor descends into the operands. `InMemoryTables` then wraps the remaining tables so `EnumerableQuery` runs the rest with LINQ to Objects: a source that is still `IQueryable` would send the operator back to this provider and recurse.
- Cross-schema queries are combined in memory on purpose (for now), even when both schemas share a connection.
- `SelectAllStatement` and `TrimmedHash` are **internal**. The SQL is composed as an `IString` value using `Pure.Primitives.String.Operations` combinators.
- The caller owns the `IDbConnection`. It is shared with every other dataset using it, so no type here may dispose it (`RowsAsyncEnumerable` must not use `await using` on the connection).

**Naming convention:** the tables must already exist with hash-based names. Schema, table and column identifiers are the first 63 characters (PostgreSQL's identifier limit) of `HexString` over `SchemaHash` / `TableHash` / `ColumnHash` from `Pure.RelationalSchema.HashCodes`. This convention must stay in sync with the Creation package. `TrimmedHash` is deliberately duplicated in each repo rather than shared.

**Testing:** Integration tests use `Testcontainers.PostgreSql` to spin up a real PostgreSQL instance. There are no unit tests with mocked databases. The tests do not use the Creation package. `DatabaseFixture` builds an ordinary `Schema` and creates it with raw SQL (`CREATE SCHEMA` / `CREATE TABLE` using the 63-character hash identifiers and a column type mapping that mirrors Creation's `PostgreSqlColumnTypeName`), so the fixture pins the naming convention. Tests that need rows seed them with raw SQL using the same hash identifiers (`TestSchema` holds the DDL and inserts). `QueryingFixture` creates two schemas for the translation tests, and `RecordingConnection` wraps the connection to assert which statements ran (translated vs. read in memory). CI runs mutation testing via `dotnet-stryker` at `Complete` level with a 77% break threshold and requires ≥90% line coverage (warning at 99%).

**CI style check:** the `Code style check by csharpier` step in `.github/workflows/build-and-test.yml` is commented out on purpose. csharpier wants `) { }` on one line for chained constructors with an empty body, while `dotnet format` (IDE0055 with `csharp_new_line_before_open_brace = all`) wants `{ }` on its own line, so both cannot pass on `PostgreSqlStoredSchemaDataSet.cs` and `PostgreSqlStoredTableDataSet.cs`. `dotnet format` is the authoritative check.

**Multi-targeting:** net8.0, net9.0, net10.0. `IsAotCompatible` is explicitly `false` (the entity types are emitted at runtime and in-memory execution uses `EnumerableQuery`).

**Package validation:** `EnablePackageValidation = true`, with `PackageValidationBaselineVersion` currently `0.1.0-preview.0.3.0`. Breaking API changes against the baseline fail the build.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag name becomes the `PackageVersion`. Packages are published to both GitHub Packages and NuGet.org.

## Code Style

Enforced via `.editorconfig` and `dotnet format --verify-no-changes` in CI:

- No `var` — always use explicit types
- No expression-bodied methods or constructors — properties, indexers, accessors, and lambdas may be expression-bodied
- File-scoped namespace declarations
- Private fields prefixed with `_` (underscore camelCase)
- No non-private instance fields
- `using` directives outside the namespace, System directives sorted first
- Max line length: 90 characters
- Braces on new lines (`csharp_new_line_before_open_brace = all`)
- No implicit object creation (`new()` without type) when the type is apparent

## Commit Messages

Do not mention Claude or AI assistance in commit messages.
