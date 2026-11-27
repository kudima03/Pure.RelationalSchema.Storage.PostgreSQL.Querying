using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The stored rows a query reads after composing them with an in-memory sequence
/// written as <c>VALUES</c>: the row of a join or a cross join read whole, beside an
/// element of the sequence or through an anonymous object, the optional row of a
/// left join from an in-memory root (null where nothing matches), and the cells
/// and the width of such rows; each is read as LINQ to Objects reads it.
/// </summary>
public sealed record InMemorySequenceRowTests : IClassFixture<ExecutionFixture>
{
    private static readonly (long Id, string? Label)[] Labels =
    [
        (1, "one"),
        (2, null),
        (4, ""),
        (91, "z"),
        (91, null),
        (500, "unmatched"),
    ];

    private readonly ExecutionFixture _fixture;

    public InMemorySequenceRowTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsTheRowsOfAJoinFilteredByTheInMemoryElement()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                    Labels,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, label) => new { Row = row, Label = label }
                )
                .Where(x => x.Label.Label != null)
                .Select(x => x.Row)
        );
    }

    [Fact]
    public void ReadsTheRowsOfAJoinBesideTheInMemoryElement()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.Join(
                Labels,
                x => long.Parse(x.Cells[ChildId].Value.TextValue),
                x => x.Id,
                (row, label) =>
                    new ValueTuple<IRow, string?, long>(row, label.Label, label.Id)
            )
        );
    }

    [Fact]
    public void ReadsTheRowsOfACrossJoinOrderedByTheInMemoryElement()
    {
        long[] factors = [3, 1, 2];

        Enumeration.AssertSameOrder(db =>
            db.Children.SelectMany(
                    x => factors,
                    (row, factor) => new { Row = row, Factor = factor }
                )
                .OrderBy(x => x.Factor)
                .ThenBy(x => long.Parse(x.Row.Cells[ChildId].Value.TextValue))
                .Select(x => x.Row)
        );
    }

    [Fact]
    public void ReadsTheOptionalRowsOfALeftJoinFromAnInMemoryRoot()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.Provider.CreateQuery<IRow?>(
                Labels
                    .AsQueryable()
                    .GroupJoin(
                        db.Children,
                        x => x.Id,
                        x => long.Parse(x.Cells[ChildId].Value.TextValue),
                        (label, rows) => new { Label = label, Rows = rows }
                    )
                    .SelectMany(x => x.Rows.DefaultIfEmpty(), (x, row) => row)
                    .Expression
            )
        );
    }

    [Fact]
    public void ReadsTheOptionalRowsOfALeftJoinBesideTheInMemoryElement()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.Provider.CreateQuery<ValueTuple<long, IRow?>>(
                Labels
                    .AsQueryable()
                    .GroupJoin(
                        db.Children,
                        x => x.Id,
                        x => long.Parse(x.Cells[ChildId].Value.TextValue),
                        (label, rows) => new { Label = label, Rows = rows }
                    )
                    .SelectMany(
                        x => x.Rows.DefaultIfEmpty(),
                        (x, row) => new ValueTuple<long, IRow?>(x.Label.Id, row)
                    )
                    .Expression
            )
        );
    }

    [Fact]
    public void ReadsTheRowsJoinedToAnInMemoryRoot()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.Provider.CreateQuery<IRow>(
                Labels
                    .AsQueryable()
                    .Join(
                        db.Children,
                        x => x.Id,
                        x => long.Parse(x.Cells[ChildId].Value.TextValue),
                        (label, row) => row
                    )
                    .Expression
            )
        );
    }

    [Fact]
    public void CountsTheCellsOfRowsJoinedToAnInMemorySequence()
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                    Labels,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, label) => new { Row = row, Label = label }
                )
                .Select(x => x.Row.Cells.Count + x.Label.Id)
        );
    }

    [Fact]
    public void ReadsTheCellsOfRowsCrossJoinedToAnInMemorySequence()
    {
        string?[] keys = ["a", null, ""];

        Enumeration.AssertSameElements(db =>
            db.Children.SelectMany(x => keys, (row, key) => new { Row = row, Key = key })
                .Where(x => x.Row.Cells[ChildKey].Value.TextValue == x.Key)
                .Select(x => new ValueTuple<string, string?>(
                    x.Row.Cells[ChildId].Value.TextValue,
                    x.Key
                ))
        );
    }

    [Fact]
    public void ReadsTheFirstRowJoinedToAnInMemorySequence()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Join(
                    Labels,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, label) => new { Row = row, Label = label }
                )
                .OrderByDescending(x => x.Label.Id)
                .ThenBy(x => x.Label.Label == null)
                .Select(x => x.Row)
                .First()
        );
    }

    [Fact]
    public void ReadsNoRowForTheFirstOrDefaultOfAnUnmatchedJoin()
    {
        long[] ids = [600, 700];

        Enumeration.AssertSameValue(db =>
            db.Values.Join(
                    ids,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x,
                    (row, id) => row
                )
                .FirstOrDefault()
        );
    }

    [Fact]
    public void ThrowsForTheSingleRowOfAJoinMatchingTwice()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Join(
                    Labels,
                    x => long.Parse(x.Cells[ValueId].Value.TextValue),
                    x => x.Id,
                    (row, label) => row
                )
                .Single(x => x.Cells[ValueId].Value.TextValue == "91")
        );
    }
}
