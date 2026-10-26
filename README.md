# Pure.RelationalSchema.Storage.PostgreSQL.Querying

PostgreSQL row querying for the **Pure** relational schema ecosystem — `IQueryable` / `IAsyncEnumerable` stored datasets over hash-named tables, read through `IDbConnection`.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/build-and-test.yml)
[![publish NuGet](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PostgreSQL.Querying)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PostgreSQL.Querying)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PostgreSQL.Querying` reads rows from a live PostgreSQL database through the storage abstractions defined in `Pure.RelationalSchema.Storage`:

- **Row access** — `PostgreSqlStoredTableDataSet` exposes the rows of a table as both `IQueryable<IRow>` and `IAsyncEnumerable<IRow>`, backed by a `SELECT` of all columns.
- **Query translation** — LINQ composed over the stored tables of one connection runs as one PostgreSQL `SELECT`, translated by [`Linq.Expressions.Translation.PostgreSql`](https://www.nuget.org/packages/Linq.Expressions.Translation.PostgreSql). A query that cannot be translated, such as one across connections, throws `NotSupportedException` instead of running in memory.
- **Schema-wide access** — `PostgreSqlStoredSchemaDataSet` maps every `ITable` of an `ISchema` to its `PostgreSqlStoredTableDataSet`. It implements `IPostgreSqlStoredSchemaDataSet`, which is defined in the separate `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions` package.
- **Native AOT** — the package is `IsAotCompatible`: the entity types the translator maps are built from reflection objects, not emitted, and no query is executed in memory.

This package covers reads only. Creating the tables and inserting rows is handled by the related packages listed below.

The tables must already exist with the hash-based names produced by `Pure.RelationalSchema.Storage.PostgreSQL.Creation` (any other creator that produces the same names works too). Schema, table and column identifiers are the first 63 hexadecimal characters of the hashes from `Pure.RelationalSchema.HashCodes`, not the model names. The caller owns the `IDbConnection`: it must be opened before use, and this package never disposes it.

## Public API

| Type | Kind | Description |
|------|------|-------------|
| `PostgreSqlStoredSchemaDataSet` | record | Maps each `ITable` in an `ISchema` to a `PostgreSqlStoredTableDataSet`; implements `IPostgreSqlStoredSchemaDataSet` (from `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`) |
| `PostgreSqlStoredTableDataSet` | record | `IStoredTableDataSet` backed by a PostgreSQL table; supports sync and async enumeration. Public members: `SchemaName`, `Connection`, `TableSchema` |

`IPostgreSqlStoredSchemaDataSet` (an `IStoredSchemaDataSet` with an `IDbConnection Connection` property) lives in `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`, so other packages can depend on it without depending on this one. All SQL statement builders, the row enumerables and the query provider are internal.

## Query translation

The `Provider` of a `PostgreSqlStoredTableDataSet` translates the LINQ composed over it. When a query is enumerated (or a terminal operator such as `Count` or `First` runs), the whole query is translated into one `SELECT` and executed. Its sources must all be stored tables of **one connection** — the same `IDbConnection` instance; their schemas may differ.

Nothing runs in memory: a query that reads several connections, reads other sources, combines rows of different tables in a set operation, or uses an operator, member or result shape that cannot be translated throws `NotSupportedException` (with the translator's exception as `InnerException` when it is the translator that refused). So a join of two tables, of one schema or of two, is one `SELECT ... JOIN ...`, while a join of tables of two connections throws; call `AsEnumerable()` on each part to combine them in memory yourself.

Rows are read as cells of text, so the provider translates cell reads of the form `row.Cells[column].Value.TextValue`, where `column` is a constant or captured `IColumn` of a queried table, into the SQL text of the column, matching what enumeration returns:

| Column type | Text in SQL |
|---|---|
| `string` | the value |
| `int`, `ushort`, `long`, `uint`, `ulong`, `uuid` | `CAST(... AS text)` |
| `bool` | `True` / `False` |
| `datetime` | `MM/dd/yyyy HH:mm:ss`, composed from `EXTRACT` parts |

`row.Cells.Count` and `row.Cells.Keys` are translated as the columns of the row's table. Cells of the other column types (`date`, `time`, `double`, `float`, `determined_hash`) cannot be translated, nor can any other use of a row (a cell's `IString` itself, ...).

A translated query returns rows of its tables (through `Where`, `OrderBy`, `Skip`, `Take`, `Distinct`, `Cast`, set operations of the same table, the element operators and the `Select`, `Join` and `SelectMany` projections that return a row), scalars, and constructed objects — anonymous types, value tuples, ... — whose constructor arguments are rows, scalars or such objects; any other result shape throws `NotSupportedException`. Results keep the semantics of enumeration, except that SQL compares and orders text by the database collation.

```csharp
IQueryable<IRow> customers = shop[customersTable];
IQueryable<IRow> orders = shop[ordersTable];

// One statement: SELECT ... FROM ... AS t0 INNER JOIN ... AS t1 ON ... WHERE ...
var totals = customers
    .Where(c => c.Cells[activeColumn].Value.TextValue == "True")
    .Join(
        orders,
        c => c.Cells[idColumn].Value.TextValue,
        o => o.Cells[customerIdColumn].Value.TextValue,
        (c, o) => new
        {
            Name = c.Cells[nameColumn].Value.TextValue,
            Total = o.Cells[totalColumn].Value.TextValue,
        }
    )
    .ToArray();
```

`IStoredTableDataSet` is both `IQueryable<IRow>` and `IAsyncEnumerable<IRow>`, so with `System.Linq.Async` (or .NET 10's `System.Linq.AsyncEnumerable`) in scope, compose over it as `IQueryable<IRow>` to pick the translated operators.

## Dependencies

- [`Pure.RelationalSchema.Storage`](https://github.com/kudima03/Pure.RelationalSchema.Storage/tree/0.1.0-preview.8.0.0) — storage abstractions (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`) that this package implements
- [`Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Abstractions/tree/0.1.0-preview.0.1.0) — `IPostgreSqlStoredSchemaDataSet`, the interface implemented by `PostgreSqlStoredSchemaDataSet`
- [`Linq.Expressions.Translation.PostgreSql`](https://github.com/kudima03/Linq.Expressions.Translation.PostgreSql/tree/0.1.0-preview.0.2.1) — translates LINQ expression trees into PostgreSQL `SELECT` statements
- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.3.0) — hash codes used to derive PostgreSQL object names
- [`Pure.Primitives.Abstractions`](https://github.com/kudima03/Pure.Primitives.Abstractions/tree/4.3.0) — base interfaces for primitive types (`IString`, etc.)
- [`Pure.Primitives`](https://github.com/kudima03/Pure.Primitives/tree/3.7.0) — concrete primitive type implementations
- [`Pure.Primitives.String.Operations`](https://github.com/kudima03/Pure.Primitives.String.Operations/tree/1.7.0) — string composition utilities used to build SQL statements
- [`Pure.Primitives.Choices`](https://github.com/kudima03/Pure.Primitives.Choices/tree/1.3.0) — choice types
- [`Pure.Collections.Generic`](https://github.com/kudima03/Pure.Collections.Generic/tree/0.1.0-preview.3.0.0) — generic collection implementations (e.g. `Dictionary<TKey1, TKey2, TValue>` with dual-key lookup)

## Target Frameworks

- .NET 8
- .NET 9
- .NET 10

## Installation

```
dotnet add package Pure.RelationalSchema.Storage.PostgreSQL.Querying
```

The example below also uses `Pure.RelationalSchema.Storage.PostgreSQL.Creation` to create the tables. Any creator that produces the same hash-based names works; Creation is not a dependency of this package.

## Usage

```csharp
using System.Data;
using Npgsql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Creation;
using Pure.RelationalSchema.Storage.PostgreSQL.Querying;
using Column = Pure.RelationalSchema.Column.Column;
using Schema = Pure.RelationalSchema.Schema.Schema;
using String = Pure.Primitives.String.String;
using Table = Pure.RelationalSchema.Table.Table;

// The caller owns the connection and opens it.
IDbConnection connection = new NpgsqlConnection(connectionString);
connection.Open();

// Describe a schema with one table.
IColumn idColumn = new Column(new String("Id"), new IntColumnType());
IColumn nameColumn = new Column(new String("Name"), new StringColumnType());
ITable usersTable = new Table(new String("Users"), [idColumn, nameColumn], []);
ISchema schema = new Schema(new String("Shop"), [usersTable], []);

// Create the tables with hash-based names (DDL runs lazily, on first access).
ISchema createdSchema = new PostgreSqlCreatedSchema(schema, connection);
_ = createdSchema.Tables.ToArray();

// Query them.
IPostgreSqlStoredSchemaDataSet dataSet =
    new PostgreSqlStoredSchemaDataSet(createdSchema, connection);

IStoredTableDataSet users = dataSet[usersTable];

// Synchronous: IQueryable<IRow>
foreach (IRow row in users)
{
    string name = row.Cells[nameColumn].Value.TextValue;
    Console.WriteLine(name);
}

// Asynchronous: IAsyncEnumerable<IRow>
await foreach (IRow row in users)
{
    string id = row.Cells[idColumn].Value.TextValue;
    Console.WriteLine(id);
}
```

Every cell value is an `IString`; read its text through `TextValue`.

## Related packages

- `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions` — the shared `IPostgreSqlStoredSchemaDataSet` interface
- `Pure.RelationalSchema.Storage.PostgreSQL.Creation` — creates the schema and tables this package reads from
- `Pure.RelationalSchema.Storage.PostgreSQL.Insertion` — inserts rows into those tables
