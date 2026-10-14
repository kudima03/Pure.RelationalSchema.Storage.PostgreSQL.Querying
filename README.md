# Pure.RelationalSchema.Storage.PostgreSQL.Querying

PostgreSQL row querying for the **Pure** relational schema ecosystem — `IQueryable` / `IAsyncEnumerable` stored datasets over hash-named tables, read through `IDbConnection`.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/build-and-test.yml)
[![publish NuGet](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PostgreSQL.Querying)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PostgreSQL.Querying)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PostgreSQL.Querying` reads rows from a live PostgreSQL database through the storage abstractions defined in `Pure.RelationalSchema.Storage`:

- **Row access** — `PostgreSqlStoredTableDataSet` exposes the rows of a table as both `IQueryable<IRow>` and `IAsyncEnumerable<IRow>`, backed by a `SELECT` of all columns.
- **Schema-wide access** — `PostgreSqlStoredSchemaDataSet` maps every `ITable` of an `ISchema` to its `PostgreSqlStoredTableDataSet`. It implements `IPostgreSqlStoredSchemaDataSet`, which is defined in the separate `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions` package.

This package covers reads only. Creating the tables and inserting rows is handled by the related packages listed below.

The tables must already exist with the hash-based names produced by `Pure.RelationalSchema.Storage.PostgreSQL.Creation` (any other creator that produces the same names works too). Schema, table and column identifiers are the first 63 hexadecimal characters of the hashes from `Pure.RelationalSchema.HashCodes`, not the model names. The caller owns the `IDbConnection`: it must be opened before use, and this package never disposes it.

## Public API

| Type | Kind | Description |
|------|------|-------------|
| `PostgreSqlStoredSchemaDataSet` | record | Maps each `ITable` in an `ISchema` to a `PostgreSqlStoredTableDataSet`; implements `IPostgreSqlStoredSchemaDataSet` (from `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`) |
| `PostgreSqlStoredTableDataSet` | record | `IStoredTableDataSet` backed by a PostgreSQL table; supports sync and async enumeration. Public members: `SchemaName`, `Connection`, `TableSchema` |

`IPostgreSqlStoredSchemaDataSet` (an `IStoredSchemaDataSet` with an `IDbConnection Connection` property) lives in `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`, so other packages can depend on it without depending on this one. All SQL statement builders and the row enumerables are internal.

## Dependencies

- [`Pure.RelationalSchema.Storage`](https://github.com/kudima03/Pure.RelationalSchema.Storage/tree/0.1.0-preview.8.0.0) — storage abstractions (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`) that this package implements
- [`Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Abstractions/tree/0.1.0-preview.0.1.0) — `IPostgreSqlStoredSchemaDataSet`, the interface implemented by `PostgreSqlStoredSchemaDataSet`
- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.3.0) — hash codes used to derive PostgreSQL object names
- [`Pure.Primitives.Abstractions`](https://github.com/kudima03/Pure.Primitives.Abstractions/tree/4.3.0) — base interfaces for primitive types (`IString`, etc.)
- [`Pure.Primitives`](https://github.com/kudima03/Pure.Primitives/tree/3.6.5) — concrete primitive type implementations
- [`Pure.Primitives.String.Operations`](https://github.com/kudima03/Pure.Primitives.String.Operations/tree/1.5.1) — string composition utilities used to build SQL statements
- [`Pure.Primitives.Choices`](https://github.com/kudima03/Pure.Primitives.Choices/tree/1.2.2) — choice types
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
