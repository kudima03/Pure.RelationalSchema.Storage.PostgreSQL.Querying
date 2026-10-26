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

public sealed record QueryingFixture : IDisposable
{
    private readonly PostgreSqlContainer _postgres;

    public QueryingFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();

        Connection = new NpgsqlConnection(_postgres.GetConnectionString());

        Connection.Open();

        CustomerId = new Column(new String("Id"), new LongColumnType());
        CustomerName = new Column(new String("Name"), new StringColumnType());
        CustomerActive = new Column(new String("Active"), new BoolColumnType());
        CustomerBorn = new Column(new String("Born"), new DateTimeColumnType());
        OrderCustomer = new Column(new String("CustomerId"), new LongColumnType());
        OrderTotal = new Column(new String("Total"), new IntColumnType());
        OrderCode = new Column(new String("Code"), new UuidColumnType());
        OrderUnits = new Column(new String("Units"), new ULongColumnType());
        OrderPriority = new Column(new String("Priority"), new UShortColumnType());
        OrderShelf = new Column(new String("Shelf"), new UIntColumnType());
        NoteCustomer = new Column(new String("CustomerId"), new LongColumnType());
        NoteText = new Column(new String("Text"), new StringColumnType());
        WordId = new Column(new String("Id"), new LongColumnType());
        WordText = new Column(new String("Text"), new StringColumnType());
        EventId = new Column(new String("Id"), new LongColumnType());
        EventAt = new Column(new String("At"), new DateTimeColumnType());

        Customers = new Table(
            new String("Customers"),
            [CustomerId, CustomerName, CustomerActive, CustomerBorn],
            []
        );
        Orders = new Table(
            new String("Orders"),
            [OrderCustomer, OrderTotal, OrderCode, OrderUnits, OrderPriority, OrderShelf],
            []
        );
        Notes = new Table(new String("Notes"), [NoteCustomer, NoteText], []);
        Words = new Table(new String("Words"), [WordId, WordText], []);
        Events = new Table(new String("Events"), [EventId, EventAt], []);

        Shop = new Schema(new String("Shop"), [Customers, Orders, Words, Events], []);
        Crm = new Schema(new String("Crm"), [Notes], []);

        TestSchema.Create(Connection, Shop);
        TestSchema.Create(Connection, Crm);

        TestSchema.Insert(
            Connection,
            Shop,
            Customers,
            ["1", "'Ann'", "true", "'2000-01-01 00:00:00'"],
            ["2", "'Bob'", "false", "'1990-05-05 00:00:00'"],
            ["3", "'Cid'", "true", "'1985-03-03 00:00:00'"]
        );
        TestSchema.Insert(
            Connection,
            Shop,
            Orders,
            ["1", "100", "'00000000-0000-0000-0000-00000000000a'", "5", "1", "10"],
            ["1", "250", "'00000000-0000-0000-0000-00000000000b'", "7", "2", "20"],
            ["3", "40", "'00000000-0000-0000-0000-00000000000c'", "1234", "1", "300"]
        );
        TestSchema.Insert(
            Connection,
            Shop,
            Words,
            ["1", "'\U0001F600x'"],
            ["2", "'a_b'"],
            ["3", "'axb'"],
            ["4", "'a\\b'"],
            ["5", "'50%'"],
            ["6", "'  pad  '"],
            ["7", "''"],
            ["8", "E'\\u00a0nbsp\\u00a0'"],
            ["9", "'MiXeD'"]
        );
        TestSchema.Insert(
            Connection,
            Shop,
            Events,
            ["1", "'0999-01-02 03:04:05'"],
            ["2", "'2024-12-31 23:59:59'"],
            ["3", "'1970-10-11 12:00:00'"],
            ["4", "'2000-01-01 10:20:30.7'"]
        );
        TestSchema.Insert(Connection, Crm, Notes, ["1", "'vip'"], ["3", "'new'"]);

        TestSchema.CreateTable(Connection, "", Notes);
        TestSchema.Insert(Connection, "", Notes, ["2", "'public'"]);
    }

    public IDbConnection Connection { get; }

    public ISchema Shop { get; }

    public ISchema Crm { get; }

    public ITable Customers { get; }

    public ITable Orders { get; }

    public ITable Notes { get; }

    public ITable Words { get; }

    public ITable Events { get; }

    public IColumn CustomerId { get; }

    public IColumn CustomerName { get; }

    public IColumn CustomerActive { get; }

    public IColumn CustomerBorn { get; }

    public IColumn OrderCustomer { get; }

    public IColumn OrderTotal { get; }

    public IColumn OrderCode { get; }

    public IColumn OrderUnits { get; }

    public IColumn OrderPriority { get; }

    public IColumn OrderShelf { get; }

    public IColumn NoteCustomer { get; }

    public IColumn NoteText { get; }

    public IColumn WordId { get; }

    public IColumn WordText { get; }

    public IColumn EventId { get; }

    public IColumn EventAt { get; }

    public void Dispose()
    {
        Connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
