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
- **Query translation** (all internal): `PostgreSqlQueryProvider` inlines captured queries (`CapturedQueries`), then hands the top-level `Queryable` operator to `TranslatedQuery`, which requires every root to be a `PostgreSqlStoredTableDataSet` of one `IDbConnection` (same reference; the schemas may differ) and rejects set operations over rows of different tables (`RowSetOperations`). `TranslatedQuery` rewrites the tree with `EntityExpression` (each table becomes a `TableRoot` whose element type is an `EntityType` with `[Table]`/`[Column]` hash names; `row.Cells[column].Value.TextValue` becomes the text of the column property, see `ColumnProperty`; `row.Cells.Count` and `row.Cells.Keys` become constants of the row's table), translates it with `PostgreSqlQuery` from `Linq.Expressions.Translation.PostgreSql`, executes it and reads the rows by position (`ResultReader`). `QueryElement` works out what an element of a query stands for — a row of which table, or a constructor call whose arguments are read in turn — by substituting the lambda parameters of `Select`/`Join`/`SelectMany` with the elements of their sources (`Substitution`); `EntityExpression` uses the same bindings for `row.Cells.Count`/`Keys`. The provider then shapes the values itself: a sequence is returned as it is (`PostgreSqlQueryable<T>` types it through `Enumerate<T>`), an element operator applies `Enumerable.First`/`Single`/... to the one row read, an aggregate returns its one value. Nothing goes through `EnumerableQuery`. A bare table expression is read with its `SELECT` of all columns.
- **Entity types without Reflection.Emit:** `EntityType` is a `TypeDelegator` over `ElementRow` (a never-read `IRow`, so `Cast<IRow>` is accepted by the translator) whose properties are hand-written reflection objects (`EntityProperty`, its `EntityGetter` and `EntityParameter`). The translator's `TableMapping` reads them through `GetProperties`, `GetCustomAttribute<T>` and `NullabilityInfoContext`, so those members must stay overridden, and the attribute arrays must really be `Attribute[]` (`Attribute.GetCustomAttributes` casts them; a target-typed collection expression returned as `object[]` is not).
- **No in-memory fallback:** any query that cannot run as one SQL statement throws `NotSupportedException` (other roots, several connections, set operations over rows of different tables, a result shape `ResultReader` cannot read, a translator exception — kept as `InnerException` — or a width mismatch). Tables of several schemas on one connection are joined in one statement. The tests of semantics the translation does not keep are skipped in `QuerySemanticsTests.cs` with the reason as `Skip` (`xUnit1004` is suppressed there, otherwise `dotnet format` strips the `Skip`).
- `SelectAllStatement` and `TrimmedHash` are **internal**. The SQL is composed as an `IString` value using `Pure.Primitives.String.Operations` combinators.
- The caller owns the `IDbConnection`. It is shared with every other dataset using it, so no type here may dispose it (`RowsAsyncEnumerable` must not use `await using` on the connection).

**Naming convention:** the tables must already exist with hash-based names. Schema, table and column identifiers are the first 63 characters (PostgreSQL's identifier limit) of `HexString` over `SchemaHash` / `TableHash` / `ColumnHash` from `Pure.RelationalSchema.HashCodes`. This convention must stay in sync with the Creation package. `TrimmedHash` is deliberately duplicated in each repo rather than shared.

**Testing:** Integration tests use `Testcontainers.PostgreSql` to spin up a real PostgreSQL instance. There are no unit tests with mocked databases. The tests do not use the Creation package. `DatabaseFixture` builds an ordinary `Schema` and creates it with raw SQL (`CREATE SCHEMA` / `CREATE TABLE` using the 63-character hash identifiers and a column type mapping that mirrors Creation's `PostgreSqlColumnTypeName`), so the fixture pins the naming convention. Tests that need rows seed them with raw SQL using the same hash identifiers (`TestSchema` holds the DDL and inserts). `QueryingFixture` creates two schemas for the translation tests, and `RecordingConnection` wraps the connection to assert how many statements ran and what they contain. CI runs mutation testing via `dotnet-stryker` at `Complete` level with a 77% break threshold and requires ≥90% line coverage (warning at 99%).

**CI style check:** the `Code style check by csharpier` step in `.github/workflows/build-and-test.yml` is commented out on purpose. csharpier wants `) { }` on one line for chained constructors with an empty body, while `dotnet format` (IDE0055 with `csharp_new_line_before_open_brace = all`) wants `{ }` on its own line, so both cannot pass on `PostgreSqlStoredSchemaDataSet.cs` and `PostgreSqlStoredTableDataSet.cs`. `dotnet format` is the authoritative check.

**Multi-targeting:** net8.0, net9.0, net10.0. `IsAotCompatible` is `true` and the build must stay free of `IL2xxx`/`IL3xxx` warnings: no Reflection.Emit, `MakeGenericType`, `Array.CreateInstance`, `EnumerableQuery` or reflection over runtime-chosen types (readers use the `ConstructorInfo` of the projection's `NewExpression`, `ColumnProperty` looks `ToString`/`DateTime` members up on constant types). The one `[RequiresUnreferencedCode]` call, `PostgreSqlQuery.TextValue`, is suppressed in `TranslatedQuery.Sql` because the entity types are reflection objects trimming cannot remove. The ILLink analyzers come from a pack that `dotnet restore` pulls in, so restore again after changing the flag.

**Formatting:** `dotnet format` formats every project of the solution, and with multi-targeting it can leave `<<<<<<< TODO: Unmerged change` conflict blocks in a file when a fix differs per target; check for them after a run.

**Package validation:** `EnablePackageValidation = true`, with `PackageValidationBaselineVersion` currently `0.1.0-preview.0.6.0`. Breaking API changes against the baseline fail the build.

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
