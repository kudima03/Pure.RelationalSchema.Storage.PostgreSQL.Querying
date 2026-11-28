using System.Globalization;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A stored row reached at a level, or none where an outer join found none.
/// </summary>
internal sealed record OptionalRowHop(IRow? Row, long Depth);

/// <summary>
/// Walks of <c>PostgreSqlRecursion.WithRecursive</c> whose levels are stored rows of
/// tables with a column of every type and NULL cells: the cells of the level read
/// as text and parsed to their types in the step, the rows of the walk read whole
/// and cell by cell afterwards (dates, times with fractions, floating-point numbers
/// and determined hashes written as enumeration writes them), and rows missing on
/// the optional side of an outer join, in the seed or in the step. Each returns, in
/// one statement, what running the step level by level over the enumerated rows
/// returns.
/// </summary>
public sealed record RecursionRowTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public RecursionRowTests(ExecutionFixture fixture)
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

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Day => _fixture.Day;

    private IColumn Clock => _fixture.Clock;

    private IColumn Measure => _fixture.Measure;

    private IColumn Ratio => _fixture.Ratio;

    private IColumn Digest => _fixture.Digest;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void WalksRowsOfEveryColumnTypeFilteredByTypedCellsOfTheLevel()
    {
        Enumeration.AssertSameElements(ValueWalk, ValueReference);
    }

    [Fact]
    public void ReadsTheTextOfEveryCellOfTheRowsOfAWalk()
    {
        Enumeration.AssertSameElements(
            db =>
                ValueWalk(db)
                    .Select(x => new ValueTuple<
                        string,
                        string,
                        string,
                        string,
                        string,
                        string
                    >(
                        x.Cells[ValueId].Value.TextValue,
                        x.Cells[Text].Value.TextValue,
                        x.Cells[Flag].Value.TextValue,
                        x.Cells[Small].Value.TextValue,
                        x.Cells[Number].Value.TextValue,
                        x.Cells[Big].Value.TextValue
                    )),
            db =>
                ValueReference(db)
                    .Select(x => new ValueTuple<
                        string,
                        string,
                        string,
                        string,
                        string,
                        string
                    >(
                        x.Cells[ValueId].Value.TextValue,
                        x.Cells[Text].Value.TextValue,
                        x.Cells[Flag].Value.TextValue,
                        x.Cells[Small].Value.TextValue,
                        x.Cells[Number].Value.TextValue,
                        x.Cells[Big].Value.TextValue
                    ))
        );
    }

    [Fact]
    public void ReadsTheTextOfUnsignedUuidAndDatetimeCellsOfTheRowsOfAWalk()
    {
        Enumeration.AssertSameElements(
            db =>
                ValueWalk(db)
                    .Select(x => new ValueTuple<string, string, string, string, string>(
                        x.Cells[ValueId].Value.TextValue,
                        x.Cells[Unsigned].Value.TextValue,
                        x.Cells[Huge].Value.TextValue,
                        x.Cells[Code].Value.TextValue,
                        x.Cells[Moment].Value.TextValue
                    )),
            db =>
                ValueReference(db)
                    .Select(x => new ValueTuple<string, string, string, string, string>(
                        x.Cells[ValueId].Value.TextValue,
                        x.Cells[Unsigned].Value.TextValue,
                        x.Cells[Huge].Value.TextValue,
                        x.Cells[Code].Value.TextValue,
                        x.Cells[Moment].Value.TextValue
                    ))
        );
    }

    [Fact]
    public void ReadsTheTypedCellsOfTheRowsOfAWalk()
    {
        Enumeration.AssertSameElements(
            db =>
                ValueWalk(db)
                    .Select(x => new ValueTuple<long, bool?, int?, Guid?, DateTime?>(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                            ? null
                            : bool.Parse(x.Cells[Flag].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                            ? null
                            : int.Parse(x.Cells[Number].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                            ? null
                            : Guid.Parse(x.Cells[Code].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                            ? null
                            : DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                    )),
            db =>
                ValueReference(db)
                    .Select(x => new ValueTuple<long, bool?, int?, Guid?, DateTime?>(
                        long.Parse(x.Cells[ValueId].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                            ? null
                            : bool.Parse(x.Cells[Flag].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                            ? null
                            : int.Parse(x.Cells[Number].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                            ? null
                            : Guid.Parse(x.Cells[Code].Value.TextValue),
                        string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                            ? null
                            : DateTime.Parse(
                                x.Cells[Moment].Value.TextValue,
                                CultureInfo.InvariantCulture
                            )
                    ))
        );
    }

    [Fact]
    public void WalksRowsWhoseLevelIsFilteredByTheTextOfItsCells()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
                    db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "92"),
                    level =>
                        db.Values.Join(
                            level.Where(x =>
                                x.Cells[Moment].Value.TextValue != ""
                                && x.Cells[Text].Value.TextValue != " padded "
                            ),
                            x => x.Cells[Big].Value.TextValue,
                            x =>
                                x.Cells[Number].Value.TextValue == ""
                                    ? "0"
                                    : "x" + x.Cells[Big].Value.TextValue,
                            (row, before) => row
                        )
                ),
            db =>
                RecursionReference.Levels(
                    db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "92"),
                    level =>
                        db.Values.Join(
                            level.Where(x =>
                                x.Cells[Moment].Value.TextValue != ""
                                && x.Cells[Text].Value.TextValue != " padded "
                            ),
                            x => x.Cells[Big].Value.TextValue,
                            x =>
                                x.Cells[Number].Value.TextValue == ""
                                    ? "0"
                                    : "x" + x.Cells[Big].Value.TextValue,
                            (row, before) => row
                        )
                )
        );
    }

    [Fact]
    public void WalksRowsOfTheOtherColumnTypes()
    {
        Enumeration.AssertSameElements(ExtraWalk, ExtraReference);
    }

    [Fact]
    public void ReadsTheTextOfDateTimeAndFloatingPointCellsOfTheRowsOfAWalk()
    {
        Enumeration.AssertSameElements(
            db =>
                ExtraWalk(db)
                    .Select(x => new ValueTuple<
                        string,
                        string,
                        string,
                        string,
                        string,
                        string
                    >(
                        x.Cells[ExtraId].Value.TextValue,
                        x.Cells[Day].Value.TextValue,
                        x.Cells[Clock].Value.TextValue,
                        x.Cells[Measure].Value.TextValue,
                        x.Cells[Ratio].Value.TextValue,
                        x.Cells[Digest].Value.TextValue
                    )),
            db =>
                ExtraReference(db)
                    .Select(x => new ValueTuple<
                        string,
                        string,
                        string,
                        string,
                        string,
                        string
                    >(
                        x.Cells[ExtraId].Value.TextValue,
                        x.Cells[Day].Value.TextValue,
                        x.Cells[Clock].Value.TextValue,
                        x.Cells[Measure].Value.TextValue,
                        x.Cells[Ratio].Value.TextValue,
                        x.Cells[Digest].Value.TextValue
                    ))
        );
    }

    [Fact]
    public void WalksRowsWhoseLevelIsFilteredByDatesAndTimes()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls.WithRecursive(
                    db.Extras.Where(x => x.Cells[ExtraId].Value.TextValue == "3"),
                    level =>
                        db.Extras.Join(
                            level.Where(x =>
                                DateOnly.Parse(
                                    x.Cells[Day].Value.TextValue,
                                    CultureInfo.InvariantCulture
                                ) < new DateOnly(2024, 1, 7)
                                && TimeOnly.Parse(
                                    x.Cells[Clock].Value.TextValue,
                                    CultureInfo.InvariantCulture
                                ) != new TimeOnly(12, 0)
                            ),
                            x => long.Parse(x.Cells[ExtraId].Value.TextValue),
                            x => long.Parse(x.Cells[ExtraId].Value.TextValue) + 1,
                            (row, before) => row
                        )
                ),
            db =>
                RecursionReference.Levels(
                    db.Extras.Where(x => x.Cells[ExtraId].Value.TextValue == "3"),
                    level =>
                        db.Extras.Join(
                            level.Where(x =>
                                DateOnly.Parse(
                                    x.Cells[Day].Value.TextValue,
                                    CultureInfo.InvariantCulture
                                ) < new DateOnly(2024, 1, 7)
                                && TimeOnly.Parse(
                                    x.Cells[Clock].Value.TextValue,
                                    CultureInfo.InvariantCulture
                                ) != new TimeOnly(12, 0)
                            ),
                            x => long.Parse(x.Cells[ExtraId].Value.TextValue),
                            x => long.Parse(x.Cells[ExtraId].Value.TextValue) + 1,
                            (row, before) => row
                        )
                )
        );
    }

    [Fact]
    public void CarriesTheMissingRowsOfAnOuterJoinInTheSeedThroughLevels()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        OptionalSeed(db),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new OptionalRowHop(x.Row, x.Depth + 1))
                    )
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        OptionalSeed(db),
                        level =>
                            level
                                .Where(x => x.Depth < 2)
                                .Select(x => new OptionalRowHop(x.Row, x.Depth + 1))
                    )
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth))
        );
    }

    [Fact]
    public void TellsTheMissingRowsOfAnOuterJoinInTheSeedFromRows()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        OptionalSeed(db),
                        level =>
                            level
                                .Where(x => x.Depth < 2 && x.Row != null)
                                .Select(x => new OptionalRowHop(x.Row, x.Depth + 1))
                    )
                    .Select(x => new ValueTuple<bool, long>(x.Row == null, x.Depth)),
            db =>
                RecursionReference
                    .Levels(
                        OptionalSeed(db),
                        level =>
                            level
                                .Where(x => x.Depth < 2 && x.Row != null)
                                .Select(x => new OptionalRowHop(x.Row, x.Depth + 1))
                    )
                    .Select(x => new ValueTuple<bool, long>(x.Row == null, x.Depth))
        );
    }

    // The seed has a row for every element, the step finds none for some: those
    // elements of the walk hold no row, as in memory, not a row of empty cells.
    [Fact]
    public void ReadsTheMissingRowsOfAnOuterJoinInTheStep()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(RowSeed(db), OptionalStep(db))
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth)),
            db =>
                RecursionReference
                    .Levels(RowSeed(db), OptionalStep(db).Compile())
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth))
        );
    }

    [Fact]
    public void TellsTheMissingRowsOfAnOuterJoinInTheStepFromRows()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(RowSeed(db), OptionalStep(db))
                    .Select(x => new ValueTuple<bool, long>(x.Row == null, x.Depth)),
            db =>
                RecursionReference
                    .Levels(RowSeed(db), OptionalStep(db).Compile())
                    .Select(x => new ValueTuple<bool, long>(x.Row == null, x.Depth))
        );
    }

    // The first walk has a row for every element, the second none for some.
    [Fact]
    public void ReadsTheMissingRowsOfTheSecondOfTwoConcatenatedWalks()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(RowSeed(db), level => level.Where(x => x.Depth < 0))
                    .Concat(
                        MarkerCalls.WithRecursive(
                            OptionalSeed(db),
                            level => level.Where(x => x.Depth < 0)
                        )
                    )
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth)),
            db =>
                RowSeed(db)
                    .Concat(OptionalSeed(db))
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth))
        );
    }

    // The same without a walk: the second side of a concatenation of stored rows.
    [Fact]
    public void ReadsTheMissingRowsOfTheSecondSideOfAConcatenation()
    {
        Enumeration.AssertSameElements(db =>
            RowSeed(db)
                .Concat(OptionalSeed(db))
                .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth))
        );
    }

    [Fact]
    public void ReadsTheMissingRowsOfTheFirstOfTwoConcatenatedWalks()
    {
        Enumeration.AssertSameElements(
            db =>
                MarkerCalls
                    .WithRecursive(
                        OptionalSeed(db),
                        level => level.Where(x => x.Depth < 0)
                    )
                    .Concat(
                        MarkerCalls.WithRecursive(
                            RowSeed(db),
                            level => level.Where(x => x.Depth < 0)
                        )
                    )
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth)),
            db =>
                OptionalSeed(db)
                    .Concat(RowSeed(db))
                    .Select(x => new ValueTuple<IRow?, long>(x.Row, x.Depth))
        );
    }

    /// <summary>
    /// Rows of <c>Values</c> from those whose code is empty to the next id, while
    /// the code of the level is the empty uuid and its flag, where it has one, true.
    /// </summary>
    private IQueryable<IRow> ValueWalk(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(
            db.Values,
            level =>
                db.Values.Join(
                    level.Where(x =>
                        !string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                        && Guid.Parse(x.Cells[Code].Value.TextValue) == Guid.Empty
                        && (
                            string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                            || bool.Parse(x.Cells[Flag].Value.TextValue)
                        )
                    ),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue) + 1,
                    (row, before) => row
                )
        );
    }

    private List<IRow> ValueReference(ExecutionTables db)
    {
        return RecursionReference.Levels(
            db.Values,
            level =>
                db.Values.Join(
                    level.Where(x =>
                        !string.IsNullOrEmpty(x.Cells[Code].Value.TextValue)
                        && Guid.Parse(x.Cells[Code].Value.TextValue) == Guid.Empty
                        && (
                            string.IsNullOrEmpty(x.Cells[Flag].Value.TextValue)
                            || bool.Parse(x.Cells[Flag].Value.TextValue)
                        )
                    ),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => long.Parse(x.Cells[ValueId].Value.TextValue) + 1,
                    (row, before) => row
                )
        );
    }

    /// <summary>
    /// Rows of <c>Extras</c> from every row to the next id, while the measure of the
    /// level is below 8.
    /// </summary>
    private IQueryable<IRow> ExtraWalk(ExecutionTables db)
    {
        return MarkerCalls.WithRecursive(
            db.Extras,
            level =>
                db.Extras.Join(
                    level.Where(x =>
                        !string.IsNullOrEmpty(x.Cells[Measure].Value.TextValue)
                        && double.Parse(
                            x.Cells[Measure].Value.TextValue,
                            CultureInfo.InvariantCulture
                        ) < 8
                    ),
                    x => long.Parse(x.Cells[ExtraId].Value.TextValue),
                    x => long.Parse(x.Cells[ExtraId].Value.TextValue) + 1,
                    (row, before) => row
                )
        );
    }

    private List<IRow> ExtraReference(ExecutionTables db)
    {
        return RecursionReference.Levels(
            db.Extras,
            level =>
                db.Extras.Join(
                    level.Where(x =>
                        !string.IsNullOrEmpty(x.Cells[Measure].Value.TextValue)
                        && double.Parse(
                            x.Cells[Measure].Value.TextValue,
                            CultureInfo.InvariantCulture
                        ) < 8
                    ),
                    x => long.Parse(x.Cells[ExtraId].Value.TextValue),
                    x => long.Parse(x.Cells[ExtraId].Value.TextValue) + 1,
                    (row, before) => row
                )
        );
    }

    /// <summary>
    /// Every parent and the parent whose id is its number, or none.
    /// </summary>
    private IQueryable<OptionalRowHop> OptionalSeed(ExecutionTables db)
    {
        return db
            .Parents.GroupJoin(
                db.Parents,
                x => x.Cells[ParentNumber].Value.TextValue,
                x => x.Cells[ParentId].Value.TextValue,
                (parent, numbered) => new { Parent = parent, Numbered = numbered }
            )
            .SelectMany(
                x => x.Numbered.DefaultIfEmpty(),
                (x, numbered) => new OptionalRowHop(numbered, 0)
            );
    }

    private static IQueryable<OptionalRowHop> RowSeed(ExecutionTables db)
    {
        return db.Parents.Select(x => new OptionalRowHop(x, 0));
    }

    /// <summary>
    /// The parent whose id is the number of the row of the level, or none.
    /// </summary>
    private Expression<
        Func<IQueryable<OptionalRowHop>, IQueryable<OptionalRowHop>>
    > OptionalStep(ExecutionTables db)
    {
        return level =>
            level
                .Where(x => x.Depth < 1)
                .GroupJoin(
                    db.Parents,
                    x => x.Row!.Cells[ParentNumber].Value.TextValue,
                    x => x.Cells[ParentId].Value.TextValue,
                    (hop, numbered) => new { Hop = hop, Numbered = numbered }
                )
                .SelectMany(
                    x => x.Numbered.DefaultIfEmpty(),
                    (x, numbered) => new OptionalRowHop(numbered, x.Hop.Depth + 1)
                );
    }
}
