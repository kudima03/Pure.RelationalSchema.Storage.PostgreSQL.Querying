using System.Globalization;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

// The tests of recursions the translation does not run yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A node reached at a level.
/// </summary>
internal sealed record Hop(long Id, long Depth);

/// <summary>
/// A key reached at a level.
/// </summary>
internal sealed record KeyHop(string Key, long Depth);

/// <summary>
/// A node and the nullable key it links to.
/// </summary>
internal sealed record LinkHop(long Id, long? Link, long Depth);

/// <summary>
/// A node whose level and path are a record of their own.
/// </summary>
internal sealed record NestedHop(long Id, HopTrail Trail);

/// <summary>
/// The level and path of a <see cref="NestedHop"/>.
/// </summary>
internal sealed record HopTrail(long Depth, string Path);

/// <summary>
/// A stored row reached at a level.
/// </summary>
internal sealed record RowHop(IRow Row, long Depth);

/// <summary>
/// Members whose names differ by case only.
/// </summary>
internal sealed record CaseHop(long Id, long ID);

/// <summary>
/// The values of a row of <c>Values</c>, NULLs included, carried through levels.
/// </summary>
internal sealed record ValueHop(
    long Id,
    long Depth,
    string? Text,
    bool? Flag,
    ushort? Small,
    int? Number,
    long? Big,
    uint? Unsigned,
    ulong? Huge,
    Guid? Code,
    DateTime? Moment
);

/// <summary>
/// The values of a row of <c>Extras</c>, NULLs included, carried through levels.
/// </summary>
internal sealed record ExtraHop(
    long Id,
    long Depth,
    DateOnly? Day,
    TimeOnly? Clock,
    double? Measure,
    double? Ratio,
    string Digest
);

/// <summary>
/// A node holding a value of each type a seed can write as a constant.
/// </summary>
internal sealed record ConstantHop(
    long Id,
    long Depth,
    string? Label,
    long? Number,
    Guid? Code,
    DateTime? Moment,
    DateOnly? Day,
    TimeOnly? Clock,
    bool? Flag,
    double? Measure,
    decimal? Amount
);

/// <summary>
/// A node holding a number of each width and a duration.
/// </summary>
internal sealed record NumberHop(
    long Id,
    int Whole,
    short Short,
    byte Byte,
    ushort Unsigned,
    decimal Amount,
    double Measure,
    float Ratio,
    TimeSpan Span
);

/// <summary>
/// A node holding the extreme dates and times .NET has.
/// </summary>
internal sealed record ExtremeHop(
    long Id,
    long Depth,
    DateTime Moment,
    TimeOnly Clock,
    DateOnly Day
);

/// <summary>
/// A node built by an object initializer.
/// </summary>
internal sealed class HopInit
{
    public long Id { get; set; }

    public long Depth { get; set; }

    public string Path { get; set; } = "";
}

/// <summary>
/// A node built by a constructor and an initializer, which may assign the member a
/// constructor parameter initializes.
/// </summary>
internal sealed class HopMixed(long id, string path)
{
    public long Id { get; } = id;

    public string Path { get; set; } = path;

    public long Depth { get; set; }
}

/// <summary>
/// The seeds and elements of <c>PostgreSqlRecursion.WithRecursive</c> over stored
/// tables: empty seeds, steps that yield nothing, seeds of rows, scalars, value
/// tuples, tuples, anonymous types, records, nested records and initializers,
/// NULLs and constants of every column type carried through the levels, rows of a
/// second and a third schema, and seeds that are paged, distinct, grouped, joined
/// or combined. Each walk returns, in one statement, what running its step level by
/// level over the enumerated rows returns (<see cref="RecursionReference"/>).
/// </summary>
public sealed record RecursionShapeTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public RecursionShapeTests(ExecutionFixture fixture)
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

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private IColumn EntryKey => _fixture.EntryKey;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Day => _fixture.Day;

    private IColumn Clock => _fixture.Clock;

    private IColumn Measure => _fixture.Measure;

    private IColumn Ratio => _fixture.Ratio;

    private IColumn Digest => _fixture.Digest;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReturnsNothingForAnEmptySeedOfRows()
    {
        AssertEmptyWalk(
            db => db.Parents.Where(x => x.Cells[ParentId].Value.TextValue == "999"),
            db =>
                level =>
                    db.Parents.Join(
                        level,
                        x => x.Cells[ParentNumber].Value.TextValue,
                        x => x.Cells[ParentId].Value.TextValue,
                        (parent, before) => parent
                    ),
            x => x
        );
    }

    [Fact]
    public void ReturnsNothingForAnEmptySeedWhoseStepWouldNeverEnd()
    {
        AssertEmptyWalk(
            db => Nodes(db).Where(x => x.Id > 1000),
            db => level => level,
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void ReturnsTheSeedForAStepThatFiltersEverything()
    {
        AssertWalk(
            Nodes,
            db => level => level.Where(x => x.Id < 0),
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void ReturnsTheSeedForAStepWithAFalsePredicate()
    {
        AssertWalk(
            Nodes,
            db => level => level.Where(x => false).Select(x => new Hop(x.Id, 1)),
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void ReturnsTheSeedForAStepJoiningNoStoredRow()
    {
        AssertWalk(
            Nodes,
            db =>
                level =>
                    db.Parents.Join(
                        level,
                        x => long.Parse(x.Cells[ParentId].Value.TextValue),
                        x => x.Id + 1000,
                        (parent, node) => new Hop(node.Id, node.Depth + 1)
                    ),
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void WalksASeedOfScalars()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => long.Parse(x.Cells[ValueId].Value.TextValue)),
            db => level => level.Where(x => x < 40).Select(x => x * 3),
            x => x
        );
    }

    [Fact]
    public void WalksASeedOfText()
    {
        AssertWalk(
            db => db.Parents.Select(x => x.Cells[ParentKey].Value.TextValue),
            db => level => level.Where(x => x.Length < 3).Select(x => x + "+"),
            x => x
        );
    }

    [Fact]
    public void WalksASeedOfNullableScalars()
    {
        AssertWalk(
            db =>
                db.Parents.Select(x =>
                    string.IsNullOrEmpty(x.Cells[ParentNumber].Value.TextValue)
                        ? null
                        : (long?)long.Parse(x.Cells[ParentNumber].Value.TextValue)
                ),
            db => level => level.Where(x => x < 3).Select(x => x + 1),
            x => x
        );
    }

    [Fact]
    public void WalksASeedOfValueTuples()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new ValueTuple<long, long, string>(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        x.Cells[Text].Value.TextValue
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Item2 < 2)
                        .Select(x => new ValueTuple<long, long, string>(
                            x.Item1 + 10,
                            x.Item2 + 1,
                            x.Item3 + "/"
                        )),
            x => x
        );
    }

    [Fact]
    public void WalksASeedOfTuples()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new Tuple<long, long>(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Item2 < 3)
                        .Select(x => new Tuple<long, long>(x.Item1 * 2, x.Item2 + 1)),
            x => new ValueTuple<long, long>(x.Item1, x.Item2)
        );
    }

    [Fact]
    public void WalksASeedOfAnonymousObjects()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new
                    {
                        Id = long.Parse(x.Cells[ValueId].Value.TextValue),
                        Depth = 0L,
                        Path = x.Cells[Text].Value.TextValue,
                    }),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 3)
                        .Select(x => new
                        {
                            Id = x.Id + 1,
                            Depth = x.Depth + 1,
                            Path = x.Path + ">" + x.Id,
                        }),
            x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path)
        );
    }

    [Fact]
    public void WalksASeedOfInitializersAssigningInAnotherOrder()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new HopInit
                    {
                        Path = x.Cells[Text].Value.TextValue,
                        Depth = 0,
                        Id = long.Parse(x.Cells[ValueId].Value.TextValue),
                    }),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new HopInit
                        {
                            Id = x.Id + 100,
                            Depth = x.Depth + 1,
                            Path = x.Path + "*",
                        }),
            x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path)
        );
    }

    [Fact]
    public void WalksASeedWhoseInitializerReplacesAConstructorArgument()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new HopMixed(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        "replaced"
                    )
                    {
                        Path = x.Cells[Text].Value.TextValue,
                        Depth = 0,
                    }),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new HopMixed(x.Id * 10, x.Path + "#")
                        {
                            Depth = x.Depth + 1,
                        }),
            x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path)
        );
    }

    [Fact]
    public void WalksASeedOfNestedRecords()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new NestedHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        new HopTrail(0, x.Cells[Text].Value.TextValue)
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Trail.Depth < 2)
                        .Select(x => new NestedHop(
                            x.Id + 1,
                            new HopTrail(x.Trail.Depth + 1, x.Trail.Path + "^")
                        )),
            x => new ValueTuple<long, long, string>(x.Id, x.Trail.Depth, x.Trail.Path)
        );
    }

    [Fact(
        Skip = "PENDING: translator matches the step's columns to the seed's "
            + "case-insensitively (Translator.Stepped), so members Id and ID throw "
            + "ArgumentException 'An item with the same key has already been added. "
            + "Key: ID'"
    )]
    public void WalksASeedOfMembersWhoseNamesDifferByCase()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new CaseHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.ID < 2)
                        .Select(x => new CaseHop(x.Id + 1, x.ID + 1)),
            x => new ValueTuple<long, long>(x.Id, x.ID)
        );
    }

    [Fact]
    public void WalksASeedOfStoredRowsAndValues()
    {
        AssertWalk(
            db =>
                db.Parents.Where(x => x.Cells[ParentId].Value.TextValue != "91")
                    .Select(x => new RowHop(x, 0)),
            db =>
                level =>
                    db.Parents.Join(
                        level.Where(x => x.Depth < 3),
                        x => x.Cells[ParentNumber].Value.TextValue,
                        x => x.Row.Cells[ParentId].Value.TextValue,
                        (parent, hop) => new RowHop(parent, hop.Depth + 1)
                    ),
            x => new ValueTuple<IRow, long>(x.Row, x.Depth)
        );
    }

    [Fact]
    public void ReadsTheCellsOfTheStoredRowsOfAWalk()
    {
        AssertWalk(
            db =>
                db.Parents.Where(x => x.Cells[ParentId].Value.TextValue != "91")
                    .Select(x => new RowHop(x, 0)),
            db =>
                level =>
                    db.Parents.Join(
                        level.Where(x => x.Depth < 3),
                        x => x.Cells[ParentNumber].Value.TextValue,
                        x => x.Row.Cells[ParentId].Value.TextValue,
                        (parent, hop) => new RowHop(parent, hop.Depth + 1)
                    ),
            x => new ValueTuple<string, string, long>(
                x.Row.Cells[ParentKey].Value.TextValue,
                x.Row.Cells[ParentNumber].Value.TextValue,
                x.Depth
            )
        );
    }

    [Fact]
    public void WalksStoredRowsIntoTheRowsOfTheTableInAnotherSchema()
    {
        AssertWalk(
            db => db.Parents,
            db =>
                level =>
                    db.ArchivedParents.Join(
                        level,
                        x => x.Cells[ParentId].Value.TextValue,
                        x => x.Cells[ParentNumber].Value.TextValue,
                        (archived, parent) => archived
                    ),
            x => x
        );
    }

    [Fact]
    public void WalksRowsOfTheTableInAnotherSchemaIntoStoredRows()
    {
        AssertWalk(
            db => db.ArchivedParents,
            db =>
                level =>
                    db.Parents.Join(
                        level,
                        x => long.Parse(x.Cells[ParentId].Value.TextValue),
                        x => long.Parse(x.Cells[ParentNumber].Value.TextValue) + 1,
                        (parent, archived) => parent
                    ),
            x => x
        );
    }

    [Fact]
    public void WalksKeysThroughTheTableOfASecondSchema()
    {
        AssertWalk(
            db =>
                db.Parents.Select(x => new KeyHop(x.Cells[ParentKey].Value.TextValue, 0)),
            db =>
                level =>
                    db.Entries.Join(
                        level.Where(x => x.Depth < 2),
                        x => x.Cells[EntryKey].Value.TextValue,
                        x => x.Key,
                        (entry, hop) =>
                            new KeyHop(
                                entry.Cells[EntryKey].Value.TextValue + "!",
                                hop.Depth + 1
                            )
                    ),
            x => new ValueTuple<string, long>(x.Key, x.Depth)
        );
    }

    [Fact]
    public void NeverJoinsANullLinkOfALevel()
    {
        AssertWalk(
            db =>
                db.Parents.Select(x => new LinkHop(
                    long.Parse(x.Cells[ParentId].Value.TextValue),
                    string.IsNullOrEmpty(x.Cells[ParentNumber].Value.TextValue)
                        ? null
                        : long.Parse(x.Cells[ParentNumber].Value.TextValue),
                    0
                )),
            db =>
                level =>
                    db.Children.Join(
                        level.Where(x => x.Depth < 3),
                        x =>
                            string.IsNullOrEmpty(x.Cells[ChildNumber].Value.TextValue)
                                ? null
                                : long.Parse(x.Cells[ChildNumber].Value.TextValue),
                        x => x.Link,
                        (child, hop) =>
                            new LinkHop(
                                long.Parse(child.Cells[ChildId].Value.TextValue),
                                string.IsNullOrEmpty(
                                    child.Cells[ChildNumber].Value.TextValue
                                )
                                    ? null
                                    : long.Parse(
                                        child.Cells[ChildNumber].Value.TextValue
                                    ),
                                hop.Depth + 1
                            )
                    ),
            x => new ValueTuple<long, long?, long>(x.Id, x.Link, x.Depth)
        );
    }

    [Fact]
    public void ComparesTheNullLinksOfALevelAsDotNetDoes()
    {
        AssertWalk(
            db =>
                db.Parents.Select(x => new LinkHop(
                    long.Parse(x.Cells[ParentId].Value.TextValue),
                    string.IsNullOrEmpty(x.Cells[ParentNumber].Value.TextValue)
                        ? null
                        : long.Parse(x.Cells[ParentNumber].Value.TextValue),
                    0
                )),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 3 && x.Link != 1)
                        .Select(x => new LinkHop(
                            x.Id,
                            x.Link == null ? 2 : null,
                            x.Depth + 1
                        )),
            x => new ValueTuple<long, long?, long>(x.Id, x.Link, x.Depth)
        );
    }

    // The seed's links are never NULL, the step's may be: the level the step reads
    // holds NULLs from the second level on.
    [Fact]
    public void ComparesLinksTheStepMakesNullAsDotNetDoes()
    {
        AssertWalk(
            db =>
                db.Children.Select(x => new LinkHop(
                    long.Parse(x.Cells[ChildId].Value.TextValue),
                    long.Parse(x.Cells[ChildId].Value.TextValue),
                    0
                )),
            db =>
                level =>
                    db.Children.Join(
                        level.Where(x => x.Depth < 3 && x.Link != 0),
                        x => long.Parse(x.Cells[ChildId].Value.TextValue),
                        x => x.Id,
                        (child, hop) =>
                            new LinkHop(
                                hop.Id,
                                string.IsNullOrEmpty(
                                    child.Cells[ChildNumber].Value.TextValue
                                )
                                    ? null
                                    : long.Parse(
                                        child.Cells[ChildNumber].Value.TextValue
                                    ),
                                hop.Depth + 1
                            )
                    ),
            x => new ValueTuple<long, long?, long>(x.Id, x.Link, x.Depth)
        );
    }

    [Fact]
    public void CarriesTheNullableValuesOfEveryColumnTypeThroughLevels()
    {
        AssertWalk(
            Values,
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new ValueHop(
                            x.Id,
                            x.Depth + 1,
                            x.Text,
                            x.Flag,
                            x.Small,
                            x.Number,
                            x.Big,
                            x.Unsigned,
                            x.Huge,
                            x.Code,
                            x.Moment
                        )),
            x => new ValueTuple<long, long, string?, bool?, ushort?, int?, long?>(
                x.Id,
                x.Depth,
                x.Text,
                x.Flag,
                x.Small,
                x.Number,
                x.Big
            )
        );
    }

    [Fact]
    public void CarriesNullableUnsignedValuesAndUuidsThroughLevels()
    {
        AssertWalk(
            Values,
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new ValueHop(
                            x.Id,
                            x.Depth + 1,
                            x.Text,
                            x.Flag,
                            x.Small,
                            x.Number,
                            x.Big,
                            x.Unsigned,
                            x.Huge,
                            x.Code,
                            x.Moment
                        )),
            x => new ValueTuple<long, long, uint?, ulong?, Guid?>(
                x.Id,
                x.Depth,
                x.Unsigned,
                x.Huge,
                x.Code
            )
        );
    }

    [Fact]
    public void CarriesNullableDatetimesThroughLevels()
    {
        AssertWalk(
            Values,
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new ValueHop(
                            x.Id,
                            x.Depth + 1,
                            x.Text,
                            x.Flag,
                            x.Small,
                            x.Number,
                            x.Big,
                            x.Unsigned,
                            x.Huge,
                            x.Code,
                            x.Moment
                        )),
            x => new ValueTuple<long, long, DateTime?>(x.Id, x.Depth, x.Moment)
        );
    }

    [Fact]
    public void ComputesValuesOfEveryColumnTypeInAStep()
    {
        AssertWalk(
            Values,
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new ValueHop(
                            x.Id,
                            x.Depth + 1,
                            x.Text + "|",
                            x.Flag == null ? null : !x.Flag,
                            x.Small,
                            x.Number / 2,
                            x.Big / 3,
                            x.Unsigned,
                            x.Huge,
                            x.Code,
                            x.Moment
                        )),
            x => new ValueTuple<long, long, string?, bool?, int?, long?>(
                x.Id,
                x.Depth,
                x.Text,
                x.Flag,
                x.Number,
                x.Big
            )
        );
    }

    [Fact]
    public void FiltersALevelByValuesOfEveryColumnType()
    {
        AssertWalk(
            Values,
            db =>
                level =>
                    level
                        .Where(x =>
                            x.Depth < 3
                            && x.Flag != false
                            && x.Number != -1
                            && x.Code != Guid.Empty
                            && x.Moment != new DateTime(2000, 1, 1)
                            && x.Text != "plain"
                        )
                        .Select(x => new ValueHop(
                            x.Id,
                            x.Depth + 1,
                            x.Text,
                            x.Flag,
                            x.Small,
                            x.Number,
                            x.Big,
                            x.Unsigned,
                            x.Huge,
                            x.Code,
                            x.Moment
                        )),
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void CarriesTheNullableValuesOfTheOtherColumnTypesThroughLevels()
    {
        AssertWalk(
            Extras,
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new ExtraHop(
                            x.Id,
                            x.Depth + 1,
                            x.Day,
                            x.Clock,
                            x.Measure,
                            x.Ratio,
                            x.Digest
                        )),
            x => new ValueTuple<
                long,
                long,
                DateOnly?,
                TimeOnly?,
                double?,
                double?,
                string
            >(x.Id, x.Depth, x.Day, x.Clock, x.Measure, x.Ratio, x.Digest)
        );
    }

    [Fact]
    public void TypesTheNullConstantsOfASeedByTheValuesOfTheStep()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                    .Select(x => new ConstantHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null
                    )),
            db =>
                level =>
                    db.Values.Join(
                        level.Where(x => x.Depth < 1),
                        x => long.Parse(x.Cells[ValueId].Value.TextValue),
                        x => x.Id,
                        (row, hop) =>
                            new ConstantHop(
                                hop.Id,
                                hop.Depth + 1,
                                row.Cells[Text].Value.TextValue,
                                long.Parse(row.Cells[Big].Value.TextValue),
                                Guid.Parse(row.Cells[Code].Value.TextValue),
                                new DateTime(2024, 2, 29, 23, 59, 58),
                                new DateOnly(2024, 1, 2),
                                new TimeOnly(3, 4, 5),
                                bool.Parse(row.Cells[Flag].Value.TextValue),
                                0.25,
                                1.5m
                            )
                    ),
            x => new ValueTuple<long, long, string?, long?, Guid?, DateTime?, DateOnly?>(
                x.Id,
                x.Depth,
                x.Label,
                x.Number,
                x.Code,
                x.Moment,
                x.Day
            )
        );
    }

    [Fact]
    public void ReadsTheNullConstantsOfASeedOfEveryType()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
                    .Select(x => new ConstantHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 1)
                        .Select(x => new ConstantHop(
                            x.Id,
                            x.Depth + 1,
                            "set",
                            7,
                            Guid.Empty,
                            new DateTime(2024, 2, 29, 23, 59, 58),
                            new DateOnly(2024, 1, 2),
                            new TimeOnly(3, 4, 5),
                            true,
                            0.25,
                            1.5m
                        )),
            x => new ValueTuple<long, long, TimeOnly?, bool?, double?, decimal?>(
                x.Id,
                x.Depth,
                x.Clock,
                x.Flag,
                x.Measure,
                x.Amount
            )
        );
    }

    [Fact]
    public void TypesTheConstantsOfASeedOfEveryType()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new ConstantHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        "start",
                        7,
                        new Guid("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0"),
                        new DateTime(2024, 2, 29, 23, 59, 58, 500),
                        new DateOnly(2024, 1, 2),
                        new TimeOnly(3, 4, 5),
                        true,
                        0.5,
                        1.25m
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new ConstantHop(
                            x.Id,
                            x.Depth + 1,
                            x.Label + "!",
                            x.Number + 1,
                            x.Code,
                            x.Moment,
                            x.Day,
                            x.Clock,
                            !x.Flag,
                            x.Measure * 2,
                            x.Amount + 1
                        )),
            x => new ValueTuple<
                long,
                long,
                string?,
                long?,
                Guid?,
                DateTime?,
                DateOnly?,
                ValueTuple<TimeOnly?, bool?, double?, decimal?>
            >(
                x.Id,
                x.Depth,
                x.Label,
                x.Number,
                x.Code,
                x.Moment,
                x.Day,
                new ValueTuple<TimeOnly?, bool?, double?, decimal?>(
                    x.Clock,
                    x.Flag,
                    x.Measure,
                    x.Amount
                )
            )
        );
    }

    [Fact]
    public void TypesTheNumberAndDurationConstantsOfASeed()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new NumberHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        0,
                        0,
                        0,
                        0m,
                        0,
                        0f,
                        TimeSpan.FromMinutes(90)
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Whole < 3)
                        .Select(x => new NumberHop(
                            x.Id,
                            x.Whole + 1,
                            (short)(x.Short + 2),
                            (byte)(x.Byte + 3),
                            (ushort)(x.Unsigned + 4),
                            x.Amount + 0.5m,
                            x.Measure + 1.25,
                            x.Ratio,
                            x.Span + TimeSpan.FromSeconds(1.5)
                        )),
            x => new ValueTuple<
                long,
                int,
                short,
                byte,
                ushort,
                decimal,
                double,
                ValueTuple<float, TimeSpan>
            >(
                x.Id,
                x.Whole,
                x.Short,
                x.Byte,
                x.Unsigned,
                x.Amount,
                x.Measure,
                new ValueTuple<float, TimeSpan>(x.Ratio, x.Span)
            )
        );
    }

    // PostgreSQL types the column by the seed, real, and fails the statement where
    // the step yields another type for it.
    [Fact(
        Skip = "PENDING: translator renders the float constant of x.Ratio + 0.5f as "
            + "the numeric 0.5, so real + numeric is double precision and PostgreSQL "
            + "fails: recursive query column has type real in non-recursive term but "
            + "type double precision overall"
    )]
    public void AddsAFloatConstantToAFloatColumnOfALevel()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new NumberHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        0,
                        0,
                        0,
                        0m,
                        0,
                        0f,
                        TimeSpan.FromMinutes(90)
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Whole < 3)
                        .Select(x => new NumberHop(
                            x.Id,
                            x.Whole + 1,
                            (short)(x.Short + 2),
                            (byte)(x.Byte + 3),
                            (ushort)(x.Unsigned + 4),
                            x.Amount + 0.5m,
                            x.Measure + 1.25,
                            x.Ratio + 0.5f,
                            x.Span + TimeSpan.FromSeconds(1.5)
                        )),
            x => new ValueTuple<
                long,
                int,
                short,
                byte,
                ushort,
                decimal,
                double,
                ValueTuple<float, TimeSpan>
            >(
                x.Id,
                x.Whole,
                x.Short,
                x.Byte,
                x.Unsigned,
                x.Amount,
                x.Measure,
                new ValueTuple<float, TimeSpan>(x.Ratio, x.Span)
            )
        );
    }

    // PostgreSQL keeps microseconds: a tick more is refused, never rounded.
    [Fact]
    public void RefusesTheSubMicrosecondDateTimeConstantsOfASeed()
    {
        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 3)
                    .Select(x => new ExtremeHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        new DateTime(9999, 12, 31, 23, 59, 59, 999).AddTicks(9999),
                        new TimeOnly(23, 59, 59),
                        new DateOnly(9999, 12, 31)
                    )),
                level =>
                    level
                        .Where(x => x.Depth < 1)
                        .Select(x => new ExtremeHop(
                            x.Id,
                            x.Depth + 1,
                            x.Moment,
                            x.Clock,
                            x.Day
                        ))
            )
        );

        Assert.Equal(
            "PostgreSQL timestamps are limited to microsecond precision.",
            exception.InnerException?.Message
        );
    }

    [Fact]
    public void CarriesTheMicrosecondDateTimeConstantsOfASeed()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 3)
                    .Select(x => new ExtremeHop(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        0,
                        new DateTime(9999, 12, 31, 23, 59, 59, 999, 999),
                        new TimeOnly(23, 59, 59, 999, 999),
                        DateOnly.MinValue
                    )),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 1)
                        .Select(x => new ExtremeHop(
                            x.Id,
                            x.Depth + 1,
                            x.Moment,
                            x.Clock,
                            x.Day
                        )),
            x => new ValueTuple<long, long, DateTime, TimeOnly, DateOnly>(
                x.Id,
                x.Depth,
                x.Moment,
                x.Clock,
                x.Day
            )
        );
    }

    [Fact]
    public void WalksAStepWhoseInitializerReplacesAConstructorArgument()
    {
        AssertWalk(
            db =>
                db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 5)
                    .Select(x => new HopMixed(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        x.Cells[Text].Value.TextValue
                    )
                    {
                        Depth = 0,
                    }),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new HopMixed(x.Id + 1, "replaced")
                        {
                            Path = x.Path + "%",
                            Depth = x.Depth + 1,
                        }),
            x => new ValueTuple<long, long, string>(x.Id, x.Depth, x.Path)
        );
    }

    [Fact]
    public void WalksAnInMemorySeedOfDoubles()
    {
        double[] seed = [0.5, -1.25, 1e15, 0.1];

        AssertInMemoryWalk(
            seed,
            db =>
                level =>
                    One(db)
                        .SelectMany(
                            row => level.Where(x => x < 100 && x > -100),
                            (row, x) => x * 3
                        ),
            x => x
        );
    }

    [Fact(
        Skip = "PENDING: translator renders the float constant of x * 3 (3f) as the "
            + "integer 3, so real * integer is double precision and PostgreSQL fails: "
            + "recursive query column has type real in non-recursive term but type "
            + "double precision overall"
    )]
    public void WalksAnInMemorySeedOfFloats()
    {
        float[] seed = [0.5f, -1.25f, 3f];

        AssertInMemoryWalk(
            seed,
            db =>
                level =>
                    One(db)
                        .SelectMany(
                            row => level.Where(x => x < 100 && x > -100),
                            (row, x) => x * 3
                        ),
            x => x
        );
    }

    [Fact]
    public void WalksAnInMemorySeedOfDecimals()
    {
        decimal[] seed = [0.5m, -1.25m, 7m];

        AssertInMemoryWalk(
            seed,
            db =>
                level =>
                    One(db)
                        .SelectMany(
                            row => level.Where(x => x < 100 && x > -100),
                            (row, x) => x * 3
                        ),
            x => x
        );
    }

    [Fact]
    public void WalksAnInMemorySeedOfText()
    {
        string[] seed = ["", "a", "Ünicøde"];

        AssertInMemoryWalk(
            seed,
            db =>
                level =>
                    One(db)
                        .SelectMany(
                            row => level.Where(x => !x.EndsWith("!!")),
                            (row, x) => x + "!"
                        ),
            x => x
        );
    }

    [Fact]
    public void WalksAnInMemorySeedOfDatetimes()
    {
        DateTime[] seed =
        [
            new DateTime(2000, 1, 1),
            new DateTime(2024, 2, 29, 23, 59, 59, 999, 999),
            new DateTime(1, 1, 1),
        ];

        AssertInMemoryWalk(
            seed,
            db =>
                level =>
                    One(db)
                        .SelectMany(
                            row => level.Where(x => x.Year < 2030),
                            (row, x) => x.AddYears(10)
                        ),
            x => x
        );
    }

    [Fact]
    public void WalksAnInMemorySeedOfTuples()
    {
        (long Id, string Path)[] seed = [(1, "a"), (2, ""), (3, "Ünicøde")];

        AssertInMemoryWalk(
            seed,
            db =>
                level =>
                    One(db)
                        .SelectMany(
                            row => level.Where(x => x.Id < 20),
                            (row, x) =>
                                new ValueTuple<long, string>(x.Id * 3, x.Path + "/")
                        ),
            x => new ValueTuple<long, string>(x.Id, x.Path)
        );
    }

    [Fact]
    public void WalksAPagedSeed()
    {
        AssertWalk(
            db => Nodes(db).OrderByDescending(x => x.Id).Take(3),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new Hop(x.Id + 1, x.Depth + 1)),
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void WalksADistinctSeed()
    {
        AssertWalk(
            db =>
                db.Values.Select(x => new KeyHop(x.Cells[Text].Value.TextValue, 0))
                    .Distinct(),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new KeyHop(x.Key + "~", x.Depth + 1)),
            x => new ValueTuple<string, long>(x.Key, x.Depth)
        );
    }

    [Fact]
    public void WalksAGroupedSeed()
    {
        AssertWalk(
            db =>
                db.Values.GroupBy(x => x.Cells[Text].Value.TextValue)
                    .Select(x => new KeyHop(x.Key, x.Count())),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 6)
                        .Select(x => new KeyHop(x.Key, x.Depth + 2)),
            x => new ValueTuple<string, long>(x.Key, x.Depth)
        );
    }

    [Fact]
    public void WalksAJoinedSeed()
    {
        AssertWalk(
            db =>
                db.Parents.Join(
                    db.Children,
                    x => x.Cells[ParentNumber].Value.TextValue,
                    x => x.Cells[ChildNumber].Value.TextValue,
                    (parent, child) =>
                        new Hop(
                            long.Parse(child.Cells[ChildId].Value.TextValue),
                            long.Parse(parent.Cells[ParentId].Value.TextValue)
                        )
                ),
            db =>
                level =>
                    level.Where(x => x.Depth < 6).Select(x => new Hop(x.Id, x.Depth + 3)),
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void WalksASeedOfTwoTablesConcatenated()
    {
        AssertWalk(
            db =>
                db.Parents.Select(x => new Hop(
                        long.Parse(x.Cells[ParentId].Value.TextValue),
                        0
                    ))
                    .Concat(
                        db.Children.Select(x => new Hop(
                            long.Parse(x.Cells[ChildId].Value.TextValue),
                            0
                        ))
                    ),
            db =>
                level =>
                    level
                        .Where(x => x.Depth < 2)
                        .Select(x => new Hop(x.Id * 2, x.Depth + 1)),
            x => new ValueTuple<long, long>(x.Id, x.Depth)
        );
    }

    [Fact]
    public void RefusesAStepThatDoesNotReadTheLevel()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(Nodes(db), level => Nodes(db).Where(x => x.Id < 0))
        );
    }

    [Fact]
    public void RefusesAStepThatConcatenatesTheLevelWithATable()
    {
        _ = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                Nodes(db),
                level =>
                    level
                        .Where(x => x.Depth < 1)
                        .Select(x => new Hop(x.Id, x.Depth + 1))
                        .Concat(Nodes(db).Where(x => x.Id < 0))
            )
        );
    }

    [Fact]
    public void RefusesAWalkOfInMemorySequencesOnly()
    {
        long[] starts = [1, 2];

        NotSupportedException exception = Enumeration.AssertRefused(db =>
            MarkerCalls.WithRecursive(
                db.Values.Provider.CreateQuery<long>(starts.AsQueryable().Expression),
                level => level.Where(x => x < 10).Select(x => x + 1)
            )
        );

        Assert.Equal(
            "The query operator WithRecursive reads sources other than stored tables, "
                + "so it cannot be translated to SQL.",
            exception.Message
        );
    }

    /// <summary>
    /// The ids of the <c>Values</c> table without NULL cells, as level 0.
    /// </summary>
    private IQueryable<Hop> Nodes(ExecutionTables db)
    {
        return db
            .Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) < 90)
            .Select(x => new Hop(long.Parse(x.Cells[ValueId].Value.TextValue), 0));
    }

    /// <summary>
    /// Every row of <c>Values</c>, NULL cells included, read as its types, as level 0.
    /// </summary>
    private IQueryable<ValueHop> Values(ExecutionTables db)
    {
        return db.Values.Select(x => new ValueHop(
            long.Parse(x.Cells[ValueId].Value.TextValue),
            0,
            string.IsNullOrEmpty(x.Cells[Text].Value.TextValue)
                ? null
                : x.Cells[Text].Value.TextValue,
            string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                ? null
                : bool.Parse(x.Cells[Flag].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Small].Value.TextValue)
                ? null
                : ushort.Parse(x.Cells[Small].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                ? null
                : int.Parse(x.Cells[Number].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Big].Value.TextValue)
                ? null
                : long.Parse(x.Cells[Big].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Unsigned].Value.TextValue)
                ? null
                : uint.Parse(x.Cells[Unsigned].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Huge].Value.TextValue)
                ? null
                : ulong.Parse(x.Cells[Huge].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                ? null
                : Guid.Parse(x.Cells[Code].Value.TextValue),
            string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                ? null
                : DateTime.Parse(
                    x.Cells[Moment].Value.TextValue,
                    CultureInfo.InvariantCulture
                )
        ));
    }

    /// <summary>
    /// Every row of <c>Extras</c>, NULL cells included, read as its types, as level 0.
    /// </summary>
    private IQueryable<ExtraHop> Extras(ExecutionTables db)
    {
        return db.Extras.Select(x => new ExtraHop(
            long.Parse(x.Cells[ExtraId].Value.TextValue),
            0,
            string.IsNullOrEmpty(x.Cells[Day].Value.TextValue)
                ? null
                : DateOnly.Parse(
                    x.Cells[Day].Value.TextValue,
                    CultureInfo.InvariantCulture
                ),
            string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue)
                ? null
                : TimeOnly.Parse(
                    x.Cells[Clock].Value.TextValue,
                    CultureInfo.InvariantCulture
                ),
            string.IsNullOrEmpty(x.Cells[Measure].Value.TextValue)
                ? null
                : double.Parse(
                    x.Cells[Measure].Value.TextValue,
                    CultureInfo.InvariantCulture
                ),
            string.IsNullOrEmpty(x.Cells[Ratio].Value.TextValue)
                ? null
                : double.Parse(
                    x.Cells[Ratio].Value.TextValue,
                    CultureInfo.InvariantCulture
                ),
            x.Cells[Digest].Value.TextValue
        ));
    }

    /// <summary>
    /// The row of <c>Values</c> whose id is 1.
    /// </summary>
    private IQueryable<IRow> One(ExecutionTables db)
    {
        return db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1");
    }

    /// <summary>
    /// The walk seeded by an in-memory sequence, which the translator writes as
    /// <c>VALUES</c>, returns what running its step level by level returns.
    /// </summary>
    private void AssertInMemoryWalk<T, TResult>(
        T[] seed,
        Func<ExecutionTables, Expression<Func<IQueryable<T>, IQueryable<T>>>> step,
        Expression<Func<T, TResult>> result
    )
    {
        AssertWalk(
            db => db.Values.Provider.CreateQuery<T>(seed.AsQueryable().Expression),
            step,
            result
        );
    }

    /// <summary>
    /// The walk, translated, returns no element in one statement, as running its
    /// step level by level over the enumerated rows does.
    /// </summary>
    private void AssertEmptyWalk<T, TResult>(
        Func<ExecutionTables, IQueryable<T>> seed,
        Func<ExecutionTables, Expression<Func<IQueryable<T>, IQueryable<T>>>> step,
        Expression<Func<T, TResult>> result
    )
    {
        ExecutionTables enumerated = ExecutionTables.Enumerated(_fixture);
        Assert.Empty(
            RecursionReference.Levels(seed(enumerated), step(enumerated).Compile())
        );
        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );
        ExecutionTables db = ExecutionTables.Stored(_fixture, connection);

        Assert.Empty(
            MarkerCalls.WithRecursive(seed(db), step(db)).Select(result).ToList()
        );
        _ = Assert.Single(connection.Statements);
    }

    /// <summary>
    /// The walk, translated, returns what running its step level by level over the
    /// enumerated rows returns, each element read through the result selector.
    /// </summary>
    private void AssertWalk<T, TResult>(
        Func<ExecutionTables, IQueryable<T>> seed,
        Func<ExecutionTables, Expression<Func<IQueryable<T>, IQueryable<T>>>> step,
        Expression<Func<T, TResult>> result
    )
    {
        Func<T, TResult> read = result.Compile();
        Enumeration.AssertSameElements(
            db => MarkerCalls.WithRecursive(seed(db), step(db)).Select(result),
            db => RecursionReference.Levels(seed(db), step(db).Compile()).Select(read)
        );
    }
}
