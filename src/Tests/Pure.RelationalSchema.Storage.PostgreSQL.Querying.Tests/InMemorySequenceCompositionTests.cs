using System.Collections.Immutable;
using Pure.RelationalSchema.Abstractions.Column;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// In-memory sequences written as <c>VALUES</c> composed with stored rows: joined on
/// composite, nullable and converted keys, left-joined either way, combined by set
/// operations, ordered and paged, aggregated, tuples of every arity and nesting,
/// and sequences captured every way a query can hold them; each returns in one
/// statement what LINQ to Objects returns over the enumerated rows.
/// </summary>
public sealed record InMemorySequenceCompositionTests : IClassFixture<ExecutionFixture>
{
    private static readonly (long Id, string? Label)[] StaticLabels =
    [
        (1, "one"),
        (2, null),
        (4, "four"),
        (91, "ninety-one"),
    ];

    private readonly ExecutionFixture _fixture;

    public InMemorySequenceCompositionTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private IColumn ParentKey => _fixture.ParentKey;

    private (long Id, string? Label)[] PropertyLabels { get; } =
    [(2, "two"), (3, null), (500, "none")];

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void JoinsOnACompositeAnonymousKeyHoldingNulls()
    {
        (string? Text, bool? Flag, string Name)[] keys =
        [
            (null, null, "both null"),
            ("plain", true, "plain"),
            ("", false, "empty"),
            ("plain", null, "flag null"),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                keys,
                x => new
                {
                    Text = string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                        ? null
                        : x.Cells[Text].Value.TextValue,
                    Flag = string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : (bool?)bool.Parse(x.Cells[Flag].Value.TextValue),
                },
                x => new { x.Text, x.Flag },
                (row, key) =>
                    new ValueTuple<string, string>(
                        row.Cells[ValueId].Value.TextValue,
                        key.Name
                    )
            )
        );
    }

    [Fact]
    public void JoinsOnACompositeTupleKeyHoldingNulls()
    {
        (string? Text, bool? Flag, string Name)[] keys =
        [
            (null, null, "both null"),
            ("plain", true, "plain"),
            ("plain", null, "flag null"),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                keys,
                x => new ValueTuple<string?, bool?>(
                    string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                        ? null
                        : x.Cells[Text].Value.TextValue,
                    string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                        ? null
                        : bool.Parse(x.Cells[Flag].Value.TextValue)
                ),
                x => new ValueTuple<string?, bool?>(x.Text, x.Flag),
                (row, key) =>
                    new ValueTuple<string, string>(
                        row.Cells[ValueId].Value.TextValue,
                        key.Name
                    )
            )
        );
    }

    [Fact]
    public void JoinsNullableTextToInMemoryStringsHoldingNull()
    {
        string?[] texts = [null, "plain", ""];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                texts,
                x =>
                    string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                        ? null
                        : x.Cells[Text].Value.TextValue,
                x => x,
                (row, text) =>
                    new ValueTuple<string, string?>(
                        row.Cells[ValueId].Value.TextValue,
                        text
                    )
            )
        );
    }

    [Fact]
    public void JoinsALongColumnToInMemoryIntegersConverted()
    {
        int[] ids = [1, 4, 91, -1];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, int>(row.Cells[ValueId].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void JoinsALongColumnToInMemoryDecimals()
    {
        decimal[] ids = [1m, 4.0m, 91.00m, 2.5m];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, decimal>(
                        row.Cells[ValueId].Value.TextValue,
                        id
                    )
            )
        );
    }

    [Fact]
    public void JoinsALongColumnToInMemoryDoubles()
    {
        double[] ids = [1, 4.0000000001, 91, 2.5];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, double>(row.Cells[ValueId].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void JoinsAnIntegerColumnToInMemoryShorts()
    {
        short[] numbers = [0, -1, short.MinValue, 7];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                numbers,
                x =>
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : int.Parse(x.Cells[Number].Value.TextValue),
                x => (int?)x,
                (row, number) =>
                    new ValueTuple<string, short>(
                        row.Cells[ValueId].Value.TextValue,
                        number
                    )
            )
        );
    }

    [Fact]
    public void LeftJoinsAnInMemoryRootToRowsThroughDefaultIfEmpty()
    {
        (long Id, string? Label)[] labels = [(1, "one"), (2, null), (3, "x"), (91, "z")];

        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<ValueTuple<long, string?, string?>>(
                labels
                    .AsQueryable()
                    .GroupJoin(
                        db.Children,
                        x => x.Id,
                        x => long.Parse(x.Cells[ChildId].Value.TextValue),
                        (label, rows) => new { Label = label, Rows = rows }
                    )
                    .SelectMany(
                        x => x.Rows.DefaultIfEmpty(),
                        (x, row) =>
                            new ValueTuple<long, string?, string?>(
                                x.Label.Id,
                                x.Label.Label,
                                row == null ? null : row.Cells[ChildLabel].Value.TextValue
                            )
                    )
                    .Expression
            )
        );
    }

    [Fact]
    public void LeftJoinsRowsToInMemoryScalarsReadingTheirDefault()
    {
        long[] ids = [1, 4, 4, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.GroupJoin(
                    ids,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x,
                    (row, matches) => new { Row = row, Matches = matches }
                )
                .SelectMany(
                    x => x.Matches.DefaultIfEmpty(),
                    (x, id) =>
                        new ValueTuple<string, long>(
                            x.Row.Cells[ValueId].Value.TextValue,
                            id
                        )
                )
        );
    }

    [Fact]
    public void LeftJoinsRowsToInMemoryTuplesReadingTheDefaultOfEveryType()
    {
        (
            long Id,
            Guid Code,
            DateTime Moment,
            DateOnly Day,
            TimeOnly Clock,
            decimal Rate,
            double Measure
        )[] items =
        [
            (
                1,
                Guid.Parse("a0b1c2d3-e4f5-a6b7-c8d9-e0f1a2b3c4d5"),
                new DateTime(2024, 2, 29, 1, 2, 3),
                new DateOnly(2024, 2, 29),
                new TimeOnly(1, 2, 3),
                1.5m,
                0.25
            ),
        ];
        (long Id, string? Text)[] padding = [(1, "x"), (2, "y")];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => padding.AsQueryable().Any(y => y.Text != null))
                .GroupJoin(
                    items,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, matches) => new { Row = row, Matches = matches }
                )
                .SelectMany(
                    x => x.Matches.DefaultIfEmpty(),
                    (x, item) =>
                        new ValueTuple<
                            string,
                            Guid,
                            DateTime,
                            DateOnly,
                            TimeOnly,
                            decimal,
                            double
                        >(
                            x.Row.Cells[ValueId].Value.TextValue,
                            item.Code,
                            item.Moment,
                            item.Day,
                            item.Clock,
                            item.Rate,
                            item.Measure
                        )
                )
        );
    }

    [Fact]
    public void LeftJoinsRowsToInMemoryTuplesReadingTheDefaultOfOtherTypes()
    {
        (
            long Id,
            TimeSpan Span,
            bool Flag,
            short Small,
            float Ratio,
            string Name
        )[] items =
        [
            (1, TimeSpan.FromMinutes(3), true, -2, 0.5f, "one"),
            (4, TimeSpan.FromDays(-1), true, 7, -1f, "four"),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.GroupJoin(
                    items,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, matches) => new { Row = row, Matches = matches }
                )
                .SelectMany(
                    x => x.Matches.DefaultIfEmpty(),
                    (x, item) =>
                        new ValueTuple<string, TimeSpan, bool, short, float, string?>(
                            x.Row.Cells[ValueId].Value.TextValue,
                            item.Span,
                            item.Flag,
                            item.Small,
                            item.Ratio,
                            item.Name
                        )
                )
        );
    }

    [Fact]
    public void LeftJoinsRowsToInMemoryScalarsWithAnExplicitDefault()
    {
        long[] ids = [1, 4, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.GroupJoin(
                    ids,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x,
                    (row, matches) => new { Row = row, Matches = matches }
                )
                .SelectMany(
                    x => x.Matches.DefaultIfEmpty(-1),
                    (x, id) =>
                        new ValueTuple<string, long>(
                            x.Row.Cells[ValueId].Value.TextValue,
                            id
                        )
                )
        );
    }

    [Fact]
    public void AggregatesTheGroupOfAnInMemorySequenceJoinedToEachRow()
    {
        (long Id, string? Label)[] labels =
        [
            (1, "one"),
            (1, null),
            (1, "uno"),
            (4, "four"),
            (91, null),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.GroupJoin(
                labels,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, group) =>
                    new ValueTuple<string, long, long?, bool, bool>(
                        row.Cells[ValueId].Value.TextValue,
                        group.Sum(x => x.Id),
                        group.Max(x => (long?)x.Id),
                        group.Any(x => x.Label == null),
                        group.All(x => x.Label != null)
                    )
            )
        );
    }

    [Fact]
    public void JoinsRowsToAnInMemorySequenceOfTuplesWithEveryTypeAndNulls()
    {
        (
            long Id,
            string? Text,
            bool? Flag,
            Guid? Code,
            DateTime? Moment,
            decimal? Rate,
            double? Measure
        )[] items =
        [
            (1, null, null, null, null, null, null),
            (
                4,
                "it's",
                true,
                Guid.Parse("a0b1c2d3-e4f5-a6b7-c8d9-e0f1a2b3c4d5"),
                new DateTime(2024, 2, 29, 23, 59, 59, 500),
                -1.5m,
                double.NaN
            ),
            (91, "", false, Guid.Empty, DateTime.MinValue, 0m, -1e300),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                items,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, item) =>
                    new ValueTuple<
                        string,
                        string?,
                        bool?,
                        Guid?,
                        DateTime?,
                        decimal?,
                        double?
                    >(
                        row.Cells[ValueId].Value.TextValue,
                        item.Text,
                        item.Flag,
                        item.Code,
                        item.Moment,
                        item.Rate,
                        item.Measure
                    )
            )
        );
    }

    [Fact]
    public void ReadsTheEighthAndLaterItemsOfInMemoryTuples()
    {
        ValueTuple<
            long,
            int,
            int,
            int,
            int,
            int,
            int,
            ValueTuple<string?, long>
        >[] items =
        [
            new ValueTuple<long, int, int, int, int, int, int, ValueTuple<string?, long>>(
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                new ValueTuple<string?, long>("eighth", 9)
            ),
            new ValueTuple<long, int, int, int, int, int, int, ValueTuple<string?, long>>(
                4,
                -2,
                -3,
                -4,
                -5,
                -6,
                -7,
                new ValueTuple<string?, long>(null, -9)
            ),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                items,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Item1,
                (row, item) =>
                    new ValueTuple<string, int, string?, long>(
                        row.Cells[ValueId].Value.TextValue,
                        item.Item7,
                        item.Rest.Item1,
                        item.Rest.Item2
                    )
            )
        );
    }

    [Fact]
    public void ReadsNestedInMemoryTuples()
    {
        ((long Id, string? Name) Key, (bool Flag, decimal Rate) Value)[] items =
        [
            ((1, "one"), (true, 0.5m)),
            ((4, null), (false, -2m)),
            ((500, "none"), (true, 1m)),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                items,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Key.Id,
                (row, item) =>
                    new ValueTuple<string, string?, bool, decimal>(
                        row.Cells[ValueId].Value.TextValue,
                        item.Key.Name,
                        item.Value.Flag,
                        item.Value.Rate
                    )
            )
        );
    }

    [Fact]
    public void ReadsTuplesOfNullsOnTheFirstRow()
    {
        (long? Id, string? Name, DateOnly? Day)[] items =
        [
            (null, null, null),
            (1, "one", new DateOnly(2024, 2, 29)),
            (null, "", DateOnly.MinValue),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(
                    x => items,
                    (row, item) =>
                        new ValueTuple<long?, string?, DateOnly?>(
                            item.Id,
                            item.Name,
                            item.Day
                        )
                )
        );
    }

    [Fact]
    public void FiltersTheItemsOfInMemoryTuplesJoinedToRows()
    {
        (long Id, string Label)[] labels =
        [
            (1, "keep it"),
            (1, "drop"),
            (4, "keep"),
            (91, "Keep"),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                    labels,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, label) => new { Row = row, Label = label }
                )
                .Where(x => x.Label.Label.StartsWith("keep", StringComparison.Ordinal))
                .Select(x => new ValueTuple<string, string>(
                    x.Row.Cells[ValueId].Value.TextValue,
                    x.Label.Label
                ))
        );
    }

    [Fact]
    public void ConcatenatesTextOfInMemoryStringsAndCells()
    {
        string[] suffixes = ["'", "\\", "", "😀"];

        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(
                x => suffixes,
                (row, suffix) => row.Cells[ChildKey].Value.TextValue + suffix
            )
        );
    }

    [Fact]
    public void TrimsInMemoryStringsAsDotNetDoes()
    {
        string[] texts = ["  a  ", "\tb\n", " c ", "d"];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => texts, (row, text) => text.Trim())
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    public void CombinesTextWithInMemoryStringsDifferingInCaseAndSpaces(string operation)
    {
        string[] texts =
        [
            "plain",
            "PLAIN",
            "Plain",
            "plain ",
            " plain",
            " padded ",
            "plain",
        ];

        Enumeration.AssertSameElements(db =>
            Combined(
                operation,
                db.Values.Select(x => x.Cells[Text].Value.TextValue),
                texts.AsQueryable()
            )
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    [InlineData("Concat")]
    public void CombinesAnInMemoryFirstSideWithRowText(string operation)
    {
        string?[] texts = ["plain", null, "other", "other", ""];

        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<string?>(
                Combined(
                    operation,
                    texts.AsQueryable(),
                    db.Values.Select(x =>
                        string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                            ? null
                            : x.Cells[Text].Value.TextValue
                    )
                ).Expression
            )
        );
    }

    [Fact]
    public void UnionsRowValuesWithTwoInMemorySequences()
    {
        long[] first = [1, 500, 500];
        long[] second = [500, 600, 2];

        Enumeration.AssertSameElements(db =>
            db.Children.Select(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                .Union(first.AsQueryable())
                .Union(second.AsQueryable())
        );
    }

    [Fact]
    public void IntersectsWithAnInMemorySequenceOfDuplicates()
    {
        string[] keys = ["a", "a", "a", "", "zz"];

        Enumeration.AssertSameElements(db =>
            db.Children.Select(x => x.Cells[ChildKey].Value.TextValue)
                .Intersect(keys.AsQueryable())
        );
    }

    [Fact]
    public void ExceptsAnInMemorySequenceFromTheKeysOfTwoTables()
    {
        string[] keys = ["b", "zz"];

        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => x.Cells[ParentKey].Value.TextValue)
                .Concat(db.Children.Select(x => x.Cells[ChildKey].Value.TextValue))
                .Except(keys.AsQueryable())
        );
    }

    [Fact]
    public void CountsTheDistinctElementsOfRowsAndAnInMemorySequence()
    {
        long[] ids = [1, 1, 500, 501, 501];

        Enumeration.AssertSameValue(db =>
            db.Children.Select(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                .Concat(ids.AsQueryable())
                .Distinct()
                .Count()
        );
    }

    [Theory]
    [InlineData("Sum")]
    [InlineData("Min")]
    [InlineData("Max")]
    [InlineData("LongCount")]
    public void AggregatesTheConcatenationOfRowsAndAnInMemorySequence(string aggregate)
    {
        long[] ids = [-7, 1000, 3];

        Enumeration.AssertSameValue(db =>
            Aggregated(
                aggregate,
                db.Children.Select(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                    .Concat(ids.AsQueryable())
            )
        );
    }

    [Fact]
    public void AveragesTheConcatenationOfRowsAndAnInMemorySequence()
    {
        long[] ids = [-7, 1000, 3];

        Enumeration.AssertSameValue(db =>
            db.Children.Select(x => long.Parse(x.Cells[ChildId].Value.TextValue))
                .Concat(ids.AsQueryable())
                .Average()
        );
    }

    [Fact]
    public void PagesTheOrderedConcatenationOfRowsAndAnInMemorySequence()
    {
        long[] ids = [500, -3, 4, 4, 0];

        Enumeration.AssertSameOrder(db =>
            db.Values.Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Concat(ids.AsQueryable())
                .OrderByDescending(x => x)
                .Skip(2)
                .Take(5)
        );
    }

    [Fact]
    public void TakesTheFirstElementOfTheOrderedUnionWithAnInMemorySequence()
    {
        long[] ids = [-3, 500];

        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Union(ids.AsQueryable())
                .OrderBy(x => x)
                .First()
        );
    }

    [Fact]
    public void TakesTheLastElementOfTheOrderedUnionWithAnInMemorySequence()
    {
        long[] ids = [-3, 500];

        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Union(ids.AsQueryable())
                .OrderBy(x => x)
                .Last()
        );
    }

    [Fact]
    public void OrdersNullableRowValuesAndAnInMemorySequenceWithNulls()
    {
        long?[] numbers = [null, 5, -5, null];

        Enumeration.AssertSameOrder(db =>
            db.Values.Select(x =>
                    string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                        ? null
                        : (long?)long.Parse(x.Cells[Big].Value.TextValue)
                )
                .Concat(numbers.AsQueryable())
                .OrderBy(x => x)
        );
    }

    [Fact]
    public void OrdersNullableRowValuesAndAnInMemorySequenceDescending()
    {
        long?[] numbers = [null, 5, -5, null];

        Enumeration.AssertSameOrder(db =>
            db.Values.Select(x =>
                    string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                        ? null
                        : (long?)long.Parse(x.Cells[Big].Value.TextValue)
                )
                .Concat(numbers.AsQueryable())
                .OrderByDescending(x => x)
        );
    }

    [Fact]
    public void OrdersTextAndAnInMemorySequenceOrdinally()
    {
        string[] texts = ["B", "a", "é", "Z", "", "😀", "ａ", "a ", "é", "é"];

        Enumeration.AssertSameOrder(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue)
                .Union(texts.AsQueryable())
                .OrderBy(x => x, StringComparer.Ordinal)
        );
    }

    [Fact(
        Skip = "PostgreSQL orders text by the collation of the database, .NET by the "
            + "current culture."
    )]
    public void OrdersTextAndAnInMemorySequenceAsTheCurrentCultureDoes()
    {
        string[] texts = ["a", "B", "b", "A", " a", "a-b", "ab", "é", "e", "Z", "", "😀"];

        Enumeration.AssertSameOrder(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue)
                .Union(texts.AsQueryable())
                .OrderBy(x => x)
        );
    }

    [Fact]
    public void OrdersInMemoryDoublesWithTheSpecialValues()
    {
        double[] numbers =
        [
            2,
            double.NaN,
            double.NegativeInfinity,
            double.PositiveInfinity,
            -1,
            double.NaN,
        ];

        Enumeration.AssertSameOrder(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => numbers, (row, number) => number)
                .OrderBy(x => x)
        );
    }

    [Fact]
    public void OrdersJoinedInMemoryTuplesByTwoKeys()
    {
        (long Id, string Label, int Rank)[] labels =
        [
            (1, "b", 2),
            (1, "a", 2),
            (4, "c", 1),
            (91, "d", 3),
            (4, "a", 1),
        ];

        Enumeration.AssertSameOrder(db =>
            db.Values.Join(
                    labels,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, label) => label
                )
                .OrderByDescending(x => x.Rank)
                .ThenBy(x => x.Label, StringComparer.Ordinal)
                .Select(x => new ValueTuple<long, string>(x.Id, x.Label))
        );
    }

    [Fact]
    public void JoinsALargeInMemorySequence()
    {
        (long Id, string Name)[] items =
        [
            .. Enumerable
                .Range(-5000, 10000)
                .Select(x =>
                    (
                        (long)x,
                        x.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )
                ),
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                items,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, item) =>
                    new ValueTuple<string, string>(
                        row.Cells[ValueId].Value.TextValue,
                        item.Name
                    )
            )
        );
    }

    [Fact]
    public void CountsTheConcatenationWithALargeInMemorySequence()
    {
        long[] ids = [.. Enumerable.Range(0, 10000).Select(x => (long)x)];

        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => long.Parse(x.Cells[ValueId].Value.TextValue))
                .Union(ids.AsQueryable())
                .Count()
        );
    }

    [Fact]
    public void JoinsAnInMemorySequenceOfOneElement()
    {
        (long Id, string Name)[] items = [(4, "four")];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                items,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, item) =>
                    new ValueTuple<string, string>(
                        row.Cells[Text].Value.TextValue,
                        item.Name
                    )
            )
        );
    }

    [Fact]
    public void JoinsASequenceOfAStaticField()
    {
        AssertJoinedLabels(db =>
            db.Values.Join(
                StaticLabels,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) =>
                    new ValueTuple<string, string?>(
                        row.Cells[ValueId].Value.TextValue,
                        label.Label
                    )
            )
        );
    }

    [Fact]
    public void JoinsASequenceOfAnInstancePropertyInsideTheQuery()
    {
        AssertJoinedLabels(db =>
            db.Values.SelectMany(
                    x => PropertyLabels,
                    (row, label) => new { Row = row, Label = label }
                )
                .Where(x =>
                    long.Parse(x.Row.Cells[ValueId].Value.TextValue) == x.Label.Id
                )
                .Select(x => new ValueTuple<string, string?>(
                    x.Row.Cells[ValueId].Value.TextValue,
                    x.Label.Label
                ))
        );
    }

    [Fact]
    public void JoinsASequenceReturnedByAMethodCallInsideTheQuery()
    {
        AssertJoinedLabels(db =>
            db.Values.SelectMany(
                    x => Labels(3),
                    (row, label) => new { Row = row, Label = label }
                )
                .Where(x =>
                    long.Parse(x.Row.Cells[ValueId].Value.TextValue) == x.Label.Id
                )
                .Select(x => new ValueTuple<string, string?>(
                    x.Row.Cells[ValueId].Value.TextValue,
                    x.Label.Label
                ))
        );
    }

    [Fact]
    public void JoinsAListAsQueryable()
    {
        List<(long Id, string? Label)> labels = [(1, "one"), (2, null), (13, "thirteen")];

        AssertJoinedLabels(db =>
            db.Values.Join(
                labels.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x.Id,
                (row, label) =>
                    new ValueTuple<string, string?>(
                        row.Cells[ValueId].Value.TextValue,
                        label.Label
                    )
            )
        );
    }

    [Fact]
    public void JoinsAHashSet()
    {
        HashSet<long> ids = [1, 4, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[Text].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void JoinsAnImmutableArray()
    {
        ImmutableArray<long> ids = [1, 4, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[Text].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void JoinsALazySequence()
    {
        IEnumerable<long> ids = Enumerable.Range(0, 5).Select(x => ((long)x * 3) + 1);

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[Text].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void JoinsAnInMemoryQueryOfTheTableProvider()
    {
        long[] ids = [1, 4, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                db.Values.Provider.CreateQuery<long>(ids.AsQueryable().Expression),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[Text].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void JoinsAFilteredInMemoryQuery()
    {
        long[] ids = [1, 2, 3, 4, 91, 500];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids.AsQueryable().Where(x => x % 2 == 0).Select(x => x + 1),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[Text].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void JoinsAnInMemoryQueryCapturedInAVariable()
    {
        IQueryable<long> ids = new long[] { 2, 4, 6, 8 }
            .AsQueryable()
            .Where(x => x > 2);

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void JoinsAnInMemoryQueryUsedTwice()
    {
        IQueryable<long> ids = new long[] { 1, 2, 4, 500 }.AsQueryable();

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                    ids,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x,
                    (row, id) => id
                )
                .Where(x => ids.Count(y => y > x) > 1)
        );
    }

    [Theory]
    [InlineData("Union")]
    [InlineData("Intersect")]
    [InlineData("Except")]
    [InlineData("Concat")]
    public void CombinesRowTextWithAnInMemoryQueryOfOneElement(string operation)
    {
        string[] keys = ["b"];

        Enumeration.AssertSameElements(db =>
            Combined(
                operation,
                db.Parents.Select(x => x.Cells[ParentKey].Value.TextValue),
                keys.AsQueryable()
            )
        );
    }

    [Fact]
    public void JoinsAnInMemoryQueryOfOneElement()
    {
        long[] ids = [4];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids.AsQueryable(),
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[Text].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void CrossJoinsAnInMemoryQueryOfOneElement()
    {
        long[] ids = [4];

        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(
                x => ids.AsQueryable(),
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[ChildId].Value.TextValue, id)
            )
        );
    }

    [Fact]
    public void FiltersByMembershipInAnInMemoryQueryOfOneElement()
    {
        long[] ids = [4];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x =>
                    ids.AsQueryable()
                        .Contains(long.Parse(x.Cells[ValueId].Value.TextValue))
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void CountsAnInMemoryQueryOfOneElementInAProjection()
    {
        long[] ids = [4];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, int>(
                x.Cells[ValueId].Value.TextValue,
                ids.AsQueryable()
                    .Count(y => y > long.Parse(x.Cells[ValueId].Value.TextValue))
            ))
        );
    }

    [Fact]
    public void ReadsTheElementOfAnInMemoryRootOfOneElementBesideAggregates()
    {
        long[] ids = [4];

        Enumeration.AssertSameElements(db =>
            db.Values.Provider.CreateQuery<ValueTuple<long, int>>(
                ids.AsQueryable()
                    .Select(id => new ValueTuple<long, int>(
                        id,
                        db.Values.Count(x =>
                            long.Parse(x.Cells[ValueId].Value.TextValue) < id
                        )
                    ))
                    .Expression
            )
        );
    }

    [Fact]
    public void JoinsAnArrayOfOneElement()
    {
        long[] ids = [4];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                ids,
                x => long.Parse(x.Cells[ValueId].Value.TextValue),
                x => x,
                (row, id) =>
                    new ValueTuple<string, long>(row.Cells[Text].Value.TextValue, id)
            )
        );
    }

    private static long Aggregated(string aggregate, IQueryable<long> source)
    {
        return aggregate switch
        {
            "Sum" => source.Sum(),
            "Min" => source.Min(),
            "Max" => source.Max(),
            _ => source.LongCount(),
        };
    }

    private static (long Id, string? Label)[] Labels(int count)
    {
        return [.. StaticLabels.Take(count)];
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

    private void AssertJoinedLabels(
        Func<ExecutionTables, IQueryable<ValueTuple<string, string?>>> query
    )
    {
        Enumeration.AssertSameElements(query);
    }
}
