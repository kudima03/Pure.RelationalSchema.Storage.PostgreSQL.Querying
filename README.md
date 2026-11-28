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

The `Provider` of a `PostgreSqlStoredTableDataSet` translates the LINQ composed over it. When a query is enumerated (or a terminal operator such as `Count` or `First` runs), the whole query is translated into one `SELECT` and executed. Its sources must be stored tables of **one connection** — the same `IDbConnection` instance; their schemas may differ — and, beside them, in-memory sequences (`AsQueryable()` of an array or a list, an `EnumerableQuery`), which the translator writes as `VALUES` or refuses (see [In-memory sequences](#in-memory-sequences)).

Nothing runs in memory: a query that reads several connections, reads other sources, combines rows of different tables in a set operation, or uses an operator, member or result shape that cannot be translated throws `NotSupportedException` (with the translator's exception as `InnerException` when it is the translator that refused). So a join of two tables, of one schema or of two, is one `SELECT ... JOIN ...`, while a join of tables of two connections throws; call `AsEnumerable()` on each part to combine them in memory yourself.

Rows are read as cells of text, so the provider translates cell reads of the form `row.Cells[column].Value.TextValue`, where `column` is a constant or captured `IColumn` of a queried table, into the SQL text of the column, matching what enumeration returns. A NULL cell is empty text, as it is when enumerated (`COALESCE(..., '')`), so NULL cells compare, join, group and order as empty text does in LINQ to Objects:

| Column type | Text in SQL |
|---|---|
| `string` | the value |
| `int`, `ushort`, `long`, `uint`, `ulong`, `uuid` | `CAST(... AS text)` |
| `bool` | `True` / `False` |
| `datetime` | `MM/dd/yyyy HH:mm:ss`, then the fraction of the second when it has one (`.FFFFFFF`: `.5`, `.000001`), composed from `EXTRACT` parts and the ticks of the value |
| `date` | `MM/dd/yyyy`, composed from `EXTRACT` parts |
| `time` | `HH:mm:ss`, then the fraction of the second when it has one (`.FFFFFFF`), composed from `EXTRACT` parts and the ticks of the value |
| `double`, `float` | the shortest round-trip text, as .NET writes it: `CAST(... AS text)` with the exponent in upper case, and in plain form where .NET uses it and PostgreSQL does not (`1e15` to `1e17` for `double`, `1e6` to `1e9` for `float`) |
| `determined_hash` | the bytes of the column, a `bytea`, decoded as UTF-8 text (`CONVERT_FROM(..., 'UTF8')`), so that a comparison, a join with or union to a text column, `Length`, `Substring`, case conversion, `Replace` or `Trim` over it are those of PostgreSQL over the text enumeration reads; PostgreSQL fails on bytes that are no UTF-8 where .NET replaces them |

`row.Cells.Count` and `row.Cells.Keys` are translated as the columns of the row's table, in every lambda that reads a row: those of the query operators, of the `Enumerable` operators over a group or the rows of a window, and of the window functions. The count of the cells of a row that may be missing (the row of a left join) is NULL where it is, which read into a number throws `InvalidOperationException`, where the missing row is `null` in memory. Cells of a column of any other type cannot be translated (the exception names the column and its type), nor can any other use of a row (a cell's `IString` itself, ...).

A cell parsed to its type is read as the column itself, with no text in between: `T.Parse(row.Cells[column].Value.TextValue, ...)` — `T` the column's type or another number type the column converts to (`bool`, `int`, `long`, `ushort`, `uint`, `ulong`, `decimal`, `double`, `Guid`, `DateOnly`, `TimeOnly`, `DateTime`; a `ushort` or a `uint` only from a column of that type and a `ulong` only from an unsigned column, as a conversion to them wraps in C# where a cast in SQL does not), with the invariant culture, number styles that allow a leading sign and read the text as decimal and date styles that assume no time zone — is the column, never NULL to the translator, so a join or filter on it compares the column with `=`, which PostgreSQL hashes, estimates and indexes; `string.IsNullOrEmpty(text) ? null : (T?)T.Parse(text)` is the nullable column, and `string.IsNullOrEmpty(text)` of such a column tests it for NULL. A `double` column is read so as a `double` only, and a `float`, `string` or `determined_hash` column is read as text whatever is parsed from it. A parse with other styles stays a parse of the cell text: with `DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal` the text is read as the instant it says in UTC, with the kind `Utc` as in memory, where the column itself has the kind `Unspecified`; `AssumeUniversal` alone or `AssumeLocal`, which go through the time zone of the client, and number styles no SQL cast honours — `AllowHexSpecifier` or `AllowBinarySpecifier`, which read another number, or styles without a leading sign, which reject a text the cast accepts — throw `NotSupportedException`. A `datetime` or `time` column read so has its whole precision, where its cell text keeps seconds or minutes only, and an empty cell parsed bare is NULL, which read into a non-nullable value throws `InvalidOperationException` (the parse of the empty text fails in memory too); the least or the greatest of an array that lists such a parse (`new[] { long.Parse(text), 5L }.Max()`) is NULL with it, where `GREATEST` and `LEAST` alone would skip it.

A translated query returns rows of its tables (through `Where`, `OrderBy`, `Skip`, `Take`, `Distinct`, `DistinctBy`, `MinBy`, `MaxBy`, `Cast`, set operations and `UnionBy` of the same table, the element operators and the `Select`, `Join`, `GroupJoin` and `SelectMany` projections that return a row, also one held by an anonymous type, a value tuple or a record built earlier in the query, and the row of a left join — `GroupJoin` ... `SelectMany(x => x.Inner.DefaultIfEmpty(), ...)` — which is `null` where none matches, also when only the second side of a set operation or the step of a recursion reads it so), scalars — the signed and unsigned numbers, `char`, `DateTime`, `DateOnly`, `TimeOnly` and `TimeSpan` values among them, read from `timestamp`, `date`, `time` and `interval` results — and constructed objects — anonymous types, value tuples, records, objects built with initializers, the keys of a `GroupBy` and the results of its result selector — whose constructor arguments and assigned members are rows, scalars or such objects, also when a `GroupJoin` or a set operation (`Union`, `Concat`, ...) builds them; a set operation or a `UnionBy` over objects holding rows of different tables throws, as one over the rows does. Any other result shape throws `NotSupportedException`. Results keep the semantics of enumeration — `FirstOrDefault`, `SingleOrDefault` and `LastOrDefault` return the default they are given, with or without a predicate and whatever its shape (the provider returns it itself where no row is read), `MinBy` of no rows is `null`, an aggregate of no rows or a NULL read into a non-nullable value throws `InvalidOperationException` — except that SQL compares and orders text by the database collation. The order of the rows is kept as enumeration keeps it: a later `OrderBy` sorts stably, `Distinct` yields the first occurrences in the order of its source, `GroupBy` the groups in the order of their first elements and `Concat` an ordered side in its order.

Several aggregates of stored tables are read as one row in one statement from a one-element in-memory root whose selector holds the aggregates: `new[] { 0 }.AsQueryable().Select(_ => new { Orders = orders.LongCount(), Revenue = orders.Sum(...) })`, created through the provider of one of the tables (`orders.Provider.CreateQuery<T>(expression)`), runs as `SELECT (SELECT COUNT(*) ...), (SELECT SUM(...) ...)` without `FROM`. The tables must be stored tables of one connection.

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

`IStoredTableDataSet` is both `IQueryable<IRow>` and `IAsyncEnumerable<IRow>`, so with `System.Linq.Async` (or .NET 10's `System.Linq.AsyncEnumerable`) in scope, compose over it as `IQueryable<IRow>` to pick the translated operators. A table used inside a lambda of a query — captured as a variable of any of its types (`IQueryable<IRow>`, `IStoredTableDataSet`, `IEnumerable<IRow>`), read through the indexer of a schema data set, or returned by a method or an invoked delegate — is a source of the same statement, and so is any query over stored tables held the same way, also as an `IEnumerable<T>` or by a constant of the tree. A lambda kept in a variable and handed to an operator inside the tree (the selector of a marker call, say) is read as if written in place. A call of a query operator of its own (a method that returns `source.Provider.CreateQuery(Call(method, ...))`, two such operators that call each other, or one that refuses this provider with `NotSupportedException` when called) is left in the tree, its arguments inlined, where it is refused with `NotSupportedException` like any call the translator does not know; a query whose tree reads itself without holding a table is refused the same way, rather than evaluated without end.

Enumerating a table, synchronously or asynchronously, reads every row before returning the first, so queries can run on the connection inside the loop. Asynchronous enumeration over a connection that is not a `DbConnection` (a decorator, for instance) reads synchronously.

### In-memory sequences

An in-memory sequence of scalars or of tuples of scalars beside the stored tables is left to the translator, which writes it as a `VALUES` list: the inner sequence of `Join`, `GroupJoin` (a left join through `DefaultIfEmpty` included) or `SelectMany`, the second sequence of `Concat`, `Union`, `Intersect` or `Except`, a subquery (`labels.AsQueryable().Any(x => x.Id == ...)`, `Contains`, `Count`, `Sum`, ...) and a root of several elements created through the provider of one of the tables (`tables.Provider.CreateQuery<T>(ids.AsQueryable().Select(id => ...).Expression)`), as well as `SelectMany` from the one-element root to such a sequence. A query of them still needs a stored table, and they compare, join and combine with the columns of the cells parsed to their types (see above): `orders.Join(labels, o => long.Parse(o.Cells[id].Value.TextValue), l => l.Id, (o, l) => new { l.Label, ... })` is one `SELECT ... INNER JOIN (VALUES (...), (...)) AS t1 ("Item1", "Item2") ON ...`. What the translator refuses — an empty sequence, a sequence of rows or objects, a `TimeOnly` or `TimeSpan` finer than a microsecond, a sequence computed from the outer element of `SelectMany` — throws `NotSupportedException` with its exception as `InnerException`, and so does a tuple of a sequence read whole, rather than member by member, which the provider cannot read.

### PostgreSQL markers

The translator's PostgreSQL markers for what LINQ cannot express — window functions with `PostgreSqlWindows.Over(source, partitionBy, orderBy, frame, (row, window) => ...)` and `PostgreSqlFrame`, and `WITH RECURSIVE` with `PostgreSqlRecursion.WithRecursive(seed, level => step[, maxDepth])` — run over stored tables like any query operator. They throw `NotSupportedException` when called, so a query of them is the tree a provider or a tree rewriter builds, `Expression.Call` of the marker over the expression of the source, created with `table.Provider.CreateQuery<T>(...)`; they may also stand anywhere inside a query, under `Where`, `OrderBy`, `Skip`, `Take`, `Select`, aggregates and element operators, as a captured query read in a subquery, or over a join, a grouping or an in-memory sequence. The provider keeps the call when it inlines captured queries (it never calls a marker), rewrites the cells read in every lambda of the marker — the partition, the order, the selector, the value of `Lag` and `Lead`, the aggregates over `window.Rows`, the seed and the step — into column properties, and reads the results: the element of `Over` is what its selector builds from the row of its source (a stored row included), the window functions being scalars, and the element of `WithRecursive` is that of its seed, rows of a table included. A row deeper than `maxDepth` fails the statement with PostgreSQL's error (a `PostgresException` from Npgsql), also for a query that reads only some of the rows. What the translator refuses — a window read in a subquery of the selector, a `DISTINCT` aggregate over the rows of a frame, a range of offsets over several sort keys, a step that reads the level twice, in a subquery or on the optional side of an outer join, or groups — throws `NotSupportedException` before any statement runs. `row.Cells.Count` and `row.Cells.Keys` of the level a step reads are those of the table of the seed.

```csharp
// The rows of each customer numbered from the latest order: one statement,
// SELECT ..., ROW_NUMBER() OVER (PARTITION BY ... ORDER BY ... DESC) ... FROM ...
Expression<Func<IRow, object?>> customer = o => o.Cells[customerIdColumn].Value.TextValue;
Expression<Func<IEnumerable<IRow>, IOrderedEnumerable<IRow>>> latest = rows =>
    rows.OrderByDescending(o => DateTime.Parse(o.Cells[placedColumn].Value.TextValue, CultureInfo.InvariantCulture));
Expression<Func<IRow, PostgreSqlWindow<IRow>, (string, long)>> numbered = (o, w) =>
    new ValueTuple<string, long>(o.Cells[idColumn].Value.TextValue, w.RowNumber());
MethodInfo over = new Func<
    IQueryable<IRow>,
    Expression<Func<IRow, object?>>?,
    Expression<Func<IEnumerable<IRow>, IOrderedEnumerable<IRow>>>?,
    PostgreSqlFrame?,
    Expression<Func<IRow, PostgreSqlWindow<IRow>, (string, long)>>,
    IQueryable<(string, long)>
>(PostgreSqlWindows.Over).Method;
(string, long)[] ranked = orders
    .Provider.CreateQuery<(string, long)>(
        Expression.Call(
            over,
            orders.Expression,
            Expression.Quote(customer),
            Expression.Quote(latest),
            Expression.Constant(null, typeof(PostgreSqlFrame)),
            Expression.Quote(numbered)
        )
    )
    .ToArray();
```

## Dependencies

- [`Pure.RelationalSchema.Storage`](https://github.com/kudima03/Pure.RelationalSchema.Storage/tree/0.1.0-preview.8.0.0) — storage abstractions (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`) that this package implements
- [`Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Abstractions/tree/0.1.0-preview.0.1.0) — `IPostgreSqlStoredSchemaDataSet`, the interface implemented by `PostgreSqlStoredSchemaDataSet`
- [`Linq.Expressions.Translation.PostgreSql`](https://github.com/kudima03/Linq.Expressions.Translation.PostgreSql/tree/0.1.0-preview.0.10.1) — translates LINQ expression trees into PostgreSQL `SELECT` statements
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
- .NET 11

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
