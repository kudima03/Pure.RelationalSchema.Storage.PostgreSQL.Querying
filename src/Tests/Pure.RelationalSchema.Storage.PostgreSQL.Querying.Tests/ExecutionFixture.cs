using System.Data;
using Npgsql;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Testcontainers.PostgreSql;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

using Column = Column.Column;
using Schema = Schema.Schema;
using Table = Table.Table;

/// <summary>
/// Tables whose columns hold NULL: <c>Values</c> has a column of every type whose
/// cells are translated, with edge values, duplicates and NULLs; <c>Parents</c> and
/// <c>Children</c> share keys that are NULL, empty, repeated or unmatched; and
/// <c>Entries</c> of a second schema on the same connection has such keys too.
/// <c>Extras</c> has a column of every type whose cells are not translated (#28).
/// Only the rows whose id starts with 9 hold NULL cells.
/// </summary>
public sealed record ExecutionFixture : IDisposable
{
    private readonly PostgreSqlContainer _postgres;

    public ExecutionFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();

        Connection = new NpgsqlConnection(_postgres.GetConnectionString());

        Connection.Open();

        ValueId = new Column(new String("Id"), new LongColumnType());
        Text = new Column(new String("Text"), new StringColumnType());
        Flag = new Column(new String("Flag"), new BoolColumnType());
        Small = new Column(new String("Small"), new UShortColumnType());
        Number = new Column(new String("Number"), new IntColumnType());
        Big = new Column(new String("Big"), new LongColumnType());
        Unsigned = new Column(new String("Unsigned"), new UIntColumnType());
        Huge = new Column(new String("Huge"), new ULongColumnType());
        Code = new Column(new String("Code"), new UuidColumnType());
        Moment = new Column(new String("Moment"), new DateTimeColumnType());
        ParentId = new Column(new String("Id"), new LongColumnType());
        ParentKey = new Column(new String("Key"), new StringColumnType());
        ParentNumber = new Column(new String("Number"), new LongColumnType());
        ChildId = new Column(new String("Id"), new LongColumnType());
        ChildKey = new Column(new String("Key"), new StringColumnType());
        ChildNumber = new Column(new String("Number"), new LongColumnType());
        ChildLabel = new Column(new String("Label"), new StringColumnType());
        EntryId = new Column(new String("Id"), new LongColumnType());
        EntryKey = new Column(new String("Key"), new StringColumnType());
        EntryAmount = new Column(new String("Amount"), new IntColumnType());
        ExtraId = new Column(new String("Id"), new LongColumnType());
        Day = new Column(new String("Day"), new DateColumnType());
        Clock = new Column(new String("Clock"), new TimeColumnType());
        Measure = new Column(new String("Measure"), new DoubleColumnType());
        Ratio = new Column(new String("Ratio"), new FloatColumnType());
        Digest = new Column(new String("Digest"), new DeterminedHashColumnType());

        Values = new Table(
            new String("Values"),
            [ValueId, Text, Flag, Small, Number, Big, Unsigned, Huge, Code, Moment],
            []
        );
        Parents = new Table(
            new String("Parents"),
            [ParentId, ParentKey, ParentNumber],
            []
        );
        Children = new Table(
            new String("Children"),
            [ChildId, ChildKey, ChildNumber, ChildLabel],
            []
        );
        Entries = new Table(new String("Entries"), [EntryId, EntryKey, EntryAmount], []);
        Extras = new Table(
            new String("Extras"),
            [ExtraId, Day, Clock, Measure, Ratio, Digest],
            []
        );

        Store = new Schema(new String("Store"), [Values, Parents, Children, Extras], []);
        Ledger = new Schema(new String("Ledger"), [Entries], []);

        TestSchema.CreateNullable(Connection, Store);
        TestSchema.CreateNullable(Connection, Ledger);

        TestSchema.Insert(
            Connection,
            Store,
            Values,
            [
                "1",
                "'plain'",
                "true",
                "0",
                "0",
                "0",
                "0",
                "0",
                "'00000000-0000-0000-0000-000000000000'",
                "'2000-01-01 00:00:00'",
            ],
            [
                "2",
                "''",
                "false",
                "65535",
                "-2147483648",
                "-9223372036854775808",
                "4294967295",
                "18446744073709551615",
                "'ffffffff-ffff-ffff-ffff-ffffffffffff'",
                "'0001-01-01 00:00:00'",
            ],
            [
                "3",
                "E'\\u00dcnic\\u00f8de \\U0001F600'",
                "true",
                "1",
                "2147483647",
                "9223372036854775807",
                "1",
                "9223372036854775808",
                "'0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0'",
                "'9999-12-31 23:59:59.999999'",
            ],
            [
                "4",
                "' padded '",
                "false",
                "7",
                "-1",
                "-1",
                "7",
                "7",
                "'A0B1C2D3-E4F5-A6B7-C8D9-E0F1A2B3C4D5'",
                "'0999-02-03 04:05:06.5'",
            ],
            [
                "91",
                "NULL",
                "NULL",
                "NULL",
                "NULL",
                "NULL",
                "NULL",
                "NULL",
                "NULL",
                "NULL",
            ],
            [
                "6",
                "'plain'",
                "true",
                "0",
                "0",
                "0",
                "0",
                "0",
                "'00000000-0000-0000-0000-000000000000'",
                "'2000-01-01 00:00:00'",
            ],
            [
                "92",
                "'plain'",
                "NULL",
                "7",
                "NULL",
                "-1",
                "NULL",
                "7",
                "NULL",
                "'1970-01-01 00:00:00.000001'",
            ]
        );
        TestSchema.Insert(
            Connection,
            Store,
            Parents,
            ["1", "'a'", "1"],
            ["2", "'b'", "2"],
            ["4", "''", "0"],
            ["5", "'a'", "1"],
            ["91", "NULL", "NULL"]
        );
        TestSchema.Insert(
            Connection,
            Store,
            Children,
            ["1", "'a'", "1", "'x'"],
            ["2", "'a'", "1", "'y'"],
            ["4", "''", "0", "'w'"],
            ["5", "'c'", "3", "'v'"],
            ["91", "NULL", "NULL", "'z'"],
            ["92", "NULL", "0", "NULL"]
        );
        TestSchema.Insert(
            Connection,
            Store,
            Extras,
            ["1", "'2024-02-29'", "'03:04:05'", "0.1", "0.1", "decode('616263', 'hex')"]
        );
        TestSchema.Insert(
            Connection,
            Ledger,
            Entries,
            ["1", "'a'", "10"],
            ["4", "''", "5"],
            ["91", "NULL", "20"],
            ["92", "'b'", "NULL"]
        );
    }

    public IDbConnection Connection { get; }

    public ISchema Store { get; }

    public ISchema Ledger { get; }

    public ITable Values { get; }

    public ITable Parents { get; }

    public ITable Children { get; }

    public ITable Entries { get; }

    public ITable Extras { get; }

    public IColumn ValueId { get; }

    public IColumn Text { get; }

    public IColumn Flag { get; }

    public IColumn Small { get; }

    public IColumn Number { get; }

    public IColumn Big { get; }

    public IColumn Unsigned { get; }

    public IColumn Huge { get; }

    public IColumn Code { get; }

    public IColumn Moment { get; }

    public IColumn ParentId { get; }

    public IColumn ParentKey { get; }

    public IColumn ParentNumber { get; }

    public IColumn ChildId { get; }

    public IColumn ChildKey { get; }

    public IColumn ChildNumber { get; }

    public IColumn ChildLabel { get; }

    public IColumn EntryId { get; }

    public IColumn EntryKey { get; }

    public IColumn EntryAmount { get; }

    public IColumn ExtraId { get; }

    public IColumn Day { get; }

    public IColumn Clock { get; }

    public IColumn Measure { get; }

    public IColumn Ratio { get; }

    public IColumn Digest { get; }

    public void Dispose()
    {
        Connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
