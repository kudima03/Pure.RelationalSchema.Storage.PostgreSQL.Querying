using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// In-memory sequences of scalars and tuples beside stored tables — the inner
/// sequence of a join, a side of a set operation, a subquery or a root of several
/// elements — are left to the translator, which writes them as <c>VALUES</c>: the
/// query returns in one statement what it returns over the enumerated rows, NULL
/// cells and every column type included, and the shapes the translator refuses
/// still throw <see cref="NotSupportedException"/> before any statement runs.
/// </summary>
public sealed record InMemorySequenceTests : IClassFixture<ExecutionFixture>
{
    private static readonly (long Id, string? Label)[] Labels =
    [
        (1, "one"),
        (2, null),
        (3, "Ünicøde 😀"),
        (91, ""),
        (91, "again"),
        (500, "unmatched"),
    ];

    private static readonly long[] Ids = [1, 4, 91, 500];

    private static readonly long?[] NullableIds = [1, null, 92, 7, 7];

    private readonly ExecutionFixture _fixture;

    public InMemorySequenceTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Small => _fixture.Small;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private IColumn Unsigned => _fixture.Unsigned;

    private IColumn Huge => _fixture.Huge;

    private IColumn Code => _fixture.Code;

    private IColumn Moment => _fixture.Moment;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Day => _fixture.Day;

    private IColumn Clock => _fixture.Clock;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void JoinsRowsToAnInMemorySequenceOfTuples()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                Labels.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) =>
                    new ValueTuple<string, string, string?>(
                        row.Cells[ValueId].Value.TextValue,
                        row.Cells[Text].Value.TextValue,
                        label.Label
                    )
            )
        );
    }

    [Fact]
    public void JoinsRowsToAnInMemoryArrayOfTuples()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                Labels,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) => new ValueTuple<long, string?>(label.Id, label.Label)
            )
        );
    }

    [Fact]
    public void ReadsTheRowsJoinedToAnInMemorySequence()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                Ids.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) => row
            )
        );
    }

    [Fact]
    public void JoinsANullableIntegerColumnToInMemoryIntegers()
    {
        int?[] numbers = [int.MinValue, int.MaxValue, 0, -1, null, 5];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                numbers.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : int.Parse(x.Cells[Number].Value.TextValue),
                x => x,
                (row, number) =>
                    new ValueTuple<string, int?>(row.Cells[ValueId].Value.TextValue, number)
            )
        );
    }

    [Fact]
    public void JoinsALongColumnToInMemoryEdgesOfLong()
    {
        long[] numbers = [long.MinValue, long.MaxValue, -1, 3];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                numbers.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                        ? null
                        : long.Parse(x.Cells[Big].Value.TextValue),
                x => (long?)x,
                (row, number) =>
                    new ValueTuple<string, long>(row.Cells[ValueId].Value.TextValue, number)
            )
        );
    }

    [Fact]
    public void JoinsAUuidColumnToInMemoryUuids()
    {
        Guid[] codes =
        [
            Guid.Empty,
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Guid.Parse("a0b1c2d3-e4f5-a6b7-c8d9-e0f1a2b3c4d5"),
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                codes.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                        ? null
                        : Guid.Parse(x.Cells[Code].Value.TextValue),
                x => (Guid?)x,
                (row, code) =>
                    new ValueTuple<string, Guid>(row.Cells[ValueId].Value.TextValue, code)
            )
        );
    }

    [Fact]
    public void JoinsADatetimeColumnToInMemoryDatetimes()
    {
        DateTime[] moments =
        [
            new DateTime(2000, 1, 1),
            DateTime.MinValue,
            new DateTime(9999, 12, 31, 23, 59, 59).AddTicks(9999990),
            new DateTime(1970, 1, 1).AddTicks(10),
            new DateTime(2024, 2, 29, 23, 59, 59),
            new DateTime(2001, 1, 1),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                moments.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                        ? null
                        :
                            DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                x => (DateTime?)x,
                (row, moment) =>
                    new ValueTuple<string, DateTime>(
                        row.Cells[ValueId].Value.TextValue,
                        moment
                    )
            )
        );
    }

    [Fact]
    public void JoinsABooleanColumnToInMemoryBooleans()
    {
        (bool Flag, string Name)[] flags = [(true, "yes"), (false, "no")];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                flags.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : bool.Parse(x.Cells[Flag].Value.TextValue),
                x => (bool?)x.Flag,
                (row, flag) =>
                    new ValueTuple<string, string>(
                        row.Cells[ValueId].Value.TextValue,
                        flag.Name
                    )
            )
        );
    }

    [Fact]
    public void JoinsATextColumnToInMemoryStrings()
    {
        string[] texts = ["plain", "", "Ünicøde 😀", " padded ", "PLAIN", "absent"];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                texts.AsQueryable(),
                x => x.Cells[Text].Value.TextValue,
                x => x,
                (row, text) =>
                    new ValueTuple<string, string>(row.Cells[ValueId].Value.TextValue, text)
            )
        );
    }

    [Fact]
    public void JoinsDateAndTimeColumnsToInMemoryDatesAndTimes()
    {
        (DateOnly Day, TimeOnly Clock)[] moments =
        [
            (new DateOnly(2024, 2, 29), new TimeOnly(3, 4, 5)),
            (new DateOnly(999, 12, 31), new TimeOnly(23, 59, 59, 500)),
            (new DateOnly(2024, 1, 2), TimeOnly.MaxValue.Add(TimeSpan.FromTicks(-9))),
            (new DateOnly(2024, 1, 3), new TimeOnly(12, 0)),
        ];

        Enumeration.AssertSameElements(db =>
            db.Extras.Join(
                moments.AsQueryable(),
                x => new ValueTuple<DateOnly?, TimeOnly?>(
                    string.IsNullOrEmpty(x.Cells[Day].Value.TextValue)
                        ? null
                        :
                            DateOnly.Parse(
                                x.Cells[Day].Value.TextValue,
                                CultureInfo.InvariantCulture
                            ),
                    string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue)
                        ? null
                        :
                            TimeOnly.Parse(
                                x.Cells[Clock].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                ),
                x => new ValueTuple<DateOnly?, TimeOnly?>(x.Day, x.Clock),
                (row, moment) =>
                    new ValueTuple<string, DateOnly, TimeOnly>(
                        row.Cells[ExtraId].Value.TextValue,
                        moment.Day,
                        moment.Clock
                    )
            )
        );
    }

    [Fact]
    public void JoinsADecimalKeyToInMemoryDecimals()
    {
        (decimal Key, decimal Rate)[] rates =
        [
            (1m, 0.5m),
            (2.0m, 1.25m),
            (91m, 79228162514264337593543950335m),
            (4.5m, -0.0000000000000000000000000001m),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                rates.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Key,
                (row, rate) =>
                    new ValueTuple<string, decimal>(
                        row.Cells[ValueId].Value.TextValue,
                        rate.Rate * 2
                    )
            )
        );
    }

    [Fact]
    public void LeftJoinsRowsToAnInMemorySequenceByGroupJoin()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.GroupJoin(
                Labels.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, labels) =>
                    new ValueTuple<string, int, int>(
                        row.Cells[ValueId].Value.TextValue,
                        labels.Count(),
                        labels.Count(x => x.Label != null)
                    )
            )
        );
    }

    [Fact]
    public void LeftJoinsRowsToAnInMemorySequenceThroughDefaultIfEmpty()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.GroupJoin(
                    Labels.AsQueryable(),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, labels) => new { Row = row, Labels = labels }
                )
                .SelectMany(
                    x => x.Labels.DefaultIfEmpty(),
                    (x, label) =>
                        new ValueTuple<string, long, string?>(
                            x.Row.Cells[ValueId].Value.TextValue,
                            label.Id,
                            label.Label
                        )
                )
        );
    }

    [Fact]
    public void CrossJoinsRowsWithAnInMemorySequence()
    {
        long[] factors = [1, -1, 0];

        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(
                x => factors,
                (row, factor) =>
                    new ValueTuple<string, string?, long>(
                        row.Cells[ChildId].Value.TextValue,
                        row.Cells[ChildLabel].Value.TextValue,
                        long.Parse(row.Cells[ChildId].Value.TextValue) * factor
                    )
            )
        );
    }

    [Fact]
    public void CrossJoinsRowsWithAnInMemoryQueryOfTuples()
    {
        IQueryable<(long Id, string? Label)> labels = Labels.AsQueryable();

        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(
                    x => labels,
                    (row, label) => new { Row = row, Label = label }
                )
                .Where(x => x.Label.Label != null)
                .Select(x => new ValueTuple<string, string?>(
                    x.Row.Cells[ChildKey].Value.TextValue,
                    x.Label.Label
                ))
        );
    }

    [Fact]
    public void ConcatenatesRowValuesAndAnInMemorySequence()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.OrderBy(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Concat(Ids.AsQueryable())
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    [InlineData("Concat")]
    public void CombinesNullableRowValuesWithAnInMemorySequence(string operation)
    {
        Enumeration.AssertSameElements(db =>
            Combined(
                operation,
                db.Values.Select(x =>
                    string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                        ? null
                        : (long?)long.Parse(x.Cells[ValueId].Value.TextValue)
                ),
                NullableIds.AsQueryable()
            )
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    [InlineData("Concat")]
    public void CombinesRowTextWithInMemoryStrings(string operation)
    {
        string?[] texts = ["plain", "", null, "Ünicøde 😀", "other"];

        Enumeration.AssertSameElements(db =>
            Combined(
                operation,
                db.Values.Select(x =>
                    string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                        ? null
                        : x.Cells[Text].Value.TextValue
                ),
                texts.AsQueryable()
            )
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    [InlineData("Concat")]
    public void CombinesTuplesOfRowsWithInMemoryTuples(string operation)
    {
        Enumeration.AssertSameElements(db =>
            Combined(
                operation,
                db.Values.Select(x => new ValueTuple<long, string?>(
                    long.Parse(x.Cells[ValueId].Value.TextValue),
                    x.Cells[ValueId].Value.TextValue == "2"
                        ? null
                        : x.Cells[Text].Value.TextValue
                )),
                new (long, string?)[] { (1, "plain"), (2, null), (3, "x"), (8, "plain") }
                    .AsQueryable()
            )
        );
    }

    [Fact]
    public void FiltersRowsByAnInMemorySubquery()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(row =>
                    Labels
                        .AsQueryable()
                        .Any(x =>
                            x.Id == long.Parse(row.Cells[ValueId].Value.TextValue)
                            && x.Label != null
                        )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersRowsByMembershipInAnInMemoryQuery()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(row =>
                    NullableIds
                        .AsQueryable()
                        .Contains(
                            string.IsNullOrEmpty(row.Cells[Number].Value.TextValue)
                                ? null
                                : long.Parse(row.Cells[ValueId].Value.TextValue)
                        )
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void ProjectsAggregatesOfAnInMemorySubquery()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Select(row => new ValueTuple<string, int, long>(
                row.Cells[ValueId].Value.TextValue,
                Labels
                    .AsQueryable()
                    .Count(x => x.Id == long.Parse(row.Cells[ValueId].Value.TextValue)),
                Labels
                    .AsQueryable()
                    .Where(x => x.Id <= long.Parse(row.Cells[ValueId].Value.TextValue))
                    .Sum(x => x.Id)
            ))
        );
    }

    [Fact]
    public void ReadsAnInMemoryRootOfSeveralElementsBesideAggregatesOfRows()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.Provider.CreateQuery<ValueTuple<long, int, bool>>(
                Ids.AsQueryable()
                    .OrderByDescending(x => x)
                    .Select(id => new ValueTuple<long, int, bool>(
                        id,
                        db.Values.Count(x => long.Parse(x.Cells[ValueId].Value.TextValue) < id),
                        db.Children.Any(x =>
                            long.Parse(x.Cells[ChildId].Value.TextValue) == id
                        )
                    ))
                    .Expression
            )
        );
    }

    [Fact]
    public void JoinsAnInMemoryRootOfSeveralElementsToRows()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<ValueTuple<long, string?, string>>(
                Labels
                    .AsQueryable()
                    .Join(
                        db.Values,
                        x => x.Id,
                        x => long.Parse(x.Cells[ValueId].Value.TextValue),
                        (label, row) =>
                            new ValueTuple<long, string?, string>(
                                label.Id,
                                label.Label,
                                row.Cells[Text].Value.TextValue
                            )
                    )
                    .Expression
            )
        );
    }

    [Fact]
    public void FlattensTheOneElementRootIntoAnInMemorySequenceFilteredByRows()
    {
        long[] one = [0];

        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<long>(
                one.AsQueryable()
                    .SelectMany(x => Ids, (x, id) => id)
                    .Where(id =>
                        db.Values.Any(x => long.Parse(x.Cells[ValueId].Value.TextValue) == id)
                    )
                    .Expression
            )
        );
    }

    // VALUES holds an unsigned number as the narrowest PostgreSQL type that holds
    // every value of its type.
    [Fact]
    public void JoinsAnUnsignedShortKeyToInMemoryUnsignedShorts()
    {
        ushort[] numbers = [0, 65535, 7];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                numbers.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Small].Value.TextValue)
                        ? null
                        : ushort.Parse(x.Cells[Small].Value.TextValue),
                x => (ushort?)x,
                (row, number) =>
                    new ValueTuple<string, ushort>(row.Cells[ValueId].Value.TextValue, number)
            )
        );
    }

    [Fact]
    public void JoinsAnUnsignedIntegerKeyToInMemoryUnsignedIntegers()
    {
        uint[] numbers = [0, 4294967295, 7];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                numbers.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Unsigned].Value.TextValue)
                        ? null
                        : uint.Parse(x.Cells[Unsigned].Value.TextValue),
                x => (uint?)x,
                (row, number) =>
                    new ValueTuple<string, uint>(row.Cells[ValueId].Value.TextValue, number)
            )
        );
    }

    [Fact]
    public void JoinsAnUnsignedLongKeyToInMemoryUnsignedLongs()
    {
        ulong[] numbers = [0, ulong.MaxValue, 9223372036854775808, 8];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                numbers.AsQueryable(),
                x =>
                    string.IsNullOrEmpty(x.Cells[Huge].Value.TextValue)
                        ? null
                        : ulong.Parse(x.Cells[Huge].Value.TextValue),
                x => (ulong?)x,
                (row, number) =>
                    new ValueTuple<string, ulong>(row.Cells[ValueId].Value.TextValue, number)
            )
        );
    }

    [Fact]
    public void RefusesAnEmptyInMemorySequence()
    {
        long[] none = [];

        Enumeration.AssertNotSupported(db =>
            db.Values.Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Concat(none.AsQueryable())
        );
    }

    [Fact]
    public void RefusesAnInMemorySequenceOfRows()
    {
        IRow[] rows = [.. ExecutionTables.Enumerated(_fixture).Parents];

        Enumeration.AssertNotSupported(db =>
            db.Values.Join(
                rows.AsQueryable(),
                x => x.Cells[ValueId].Value.TextValue,
                x => x.Cells[ValueId].Value.TextValue,
                (row, other) => row.Cells[Text].Value.TextValue
            )
        );
    }

    [Fact]
    public void RefusesAnInMemorySequenceOfObjects()
    {
        var labels = new[] { new { Id = 1L, Label = "one" }, new { Id = 2L, Label = "two" } };

        Enumeration.AssertNotSupported(db =>
            db.Values.Join(
                labels.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) => label.Label
            )
        );
    }

    [Fact]
    public void RefusesASequenceComputedFromTheOuterRow()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.SelectMany(
                x => new[]
                {
                    long.Parse(x.Cells[ValueId].Value.TextValue),
                    long.Parse(x.Cells[ValueId].Value.TextValue) + 1,
                },
                (row, id) => id
            )
        );
    }

    [Fact]
    public void RefusesAnInMemoryTupleReadWhole()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Join(
                Labels.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) => label
            )
        );
    }

    [Fact]
    public void RefusesAnInMemorySequenceReadWithoutStoredTables()
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            ExecutionTables
                .Stored(_fixture, _fixture.Connection)
                .Values.Provider.CreateQuery<long>(
                    Ids.AsQueryable().Select(x => x + 1).Expression
                )
                .ToList()
        );

        Assert.Equal(
            "The query operator Select reads sources other than stored tables, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
    }

    [Fact]
    public void RefusesAnInMemorySequenceBesideTablesOfTwoConnections()
    {
        using RecordingConnection one = new RecordingConnection(_fixture.Connection);
        using RecordingConnection other = new RecordingConnection(_fixture.Connection);
        IQueryable<IRow> children = ExecutionTables.Stored(_fixture, other).Children;

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            ExecutionTables
                .Stored(_fixture, one)
                .Values.Join(
                    Ids.AsQueryable(),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x,
                    (row, id) => id
                )
                .Where(id =>
                    children.Any(x => long.Parse(x.Cells[ChildId].Value.TextValue) == id)
                )
                .ToList()
        );

        Assert.Equal(
            "The query operator Where reads tables of more than one connection, so it "
                + "cannot be translated to SQL.",
            exception.Message
        );
        Assert.Empty(one.Statements);
        Assert.Empty(other.Statements);
    }

    private static IQueryable<T> Combined<T>(
        string operation,
        IQueryable<T> first,
        IQueryable<T> second
    )
    {
        return operation switch
        {
            "Union" => first.Union(second),
            "Intersect" => first.Intersect(second),
            "Except" => first.Except(second),
            _ => first.Concat(second),
        };
    }
}
