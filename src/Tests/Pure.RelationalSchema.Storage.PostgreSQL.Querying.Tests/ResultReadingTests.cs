using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The values of a translated query that has none to give — an element of no rows,
/// an aggregate of no rows, a NULL — must be what the same query returns over the
/// enumerated rows in memory, or the same exception.
/// </summary>
public sealed record ResultReadingTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public ResultReadingTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Number => _fixture.Number;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReturnsDefaultsOfValuesWhenNoElementIsFound()
    {
        Enumeration.AssertSameValue(db =>
            NoValues(db)
                .Select(x => x.Cells[Text].Value.TextValue.Length)
                .FirstOrDefault()
        );
        Enumeration.AssertSameValue(db =>
            NoValues(db)
                .Select(x => new ValueTuple<string, int>(
                    x.Cells[Text].Value.TextValue,
                    1
                ))
                .SingleOrDefault()
        );
    }

    [Fact]
    public void ReturnsANullElementOfNullableValues()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "91")
                .Select(x =>
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : (int?)int.Parse(x.Cells[Number].Value.TextValue)
                )
                .First()
        );
    }

    [Fact]
    public void ReturnsTheMatchingElementInsteadOfTheGivenDefault()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue)
                .FirstOrDefault(x => x == " padded ", "fallback")
        );
    }

    [Fact]
    public void SumsNoRowsOfASubqueryToZero()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, int>(
                parent.Cells[ParentId].Value.TextValue,
                db.Children.Where(x =>
                        x.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue + "?"
                    )
                    .Sum(x => x.Cells[ChildLabel].Value.TextValue.Length)
            ))
        );
    }

    [Fact]
    public void ReadsTheMissingElementOfASubqueryAsNull()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, string?>(
                parent.Cells[ParentId].Value.TextValue,
                db.Children.Where(x =>
                        x.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                    .OrderBy(x => x.Cells[ChildId].Value.TextValue)
                    .Select(x => x.Cells[ChildLabel].Value.TextValue)
                    .FirstOrDefault()
            ))
        );
    }

    [Fact]
    public void FiltersRowsByTheMissingElementOfASubquery()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    db.Children.Where(x =>
                            x.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .OrderBy(x => x.Cells[ChildId].Value.TextValue)
                        .Select(x => x.Cells[ChildLabel].Value.TextValue)
                        .FirstOrDefault() != "x"
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersRowsByTheMissingMaximumOfASubquery()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    db.Children.Where(x =>
                            x.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Max(x => x.Cells[ChildLabel].Value.TextValue) == null
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Parents.Where(parent =>
                    !(
                        db.Children.Where(x =>
                                x.Cells[ChildKey].Value.TextValue
                                == parent.Cells[ParentKey].Value.TextValue
                            )
                            .Max(x => x.Cells[ChildLabel].Value.TextValue) == "y"
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void ConcatenatesNullTextAsEmptyText()
    {
        string? suffix = null;

        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => x.Cells[ParentId].Value.TextValue + suffix)
        );
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x =>
                (
                    x.Cells[ParentKey].Value.TextValue == "a"
                        ? null
                        : x.Cells[ParentKey].Value.TextValue
                ) + "x"
            )
        );
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(parent =>
                db.Children.Where(x =>
                        x.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue + "?"
                    )
                    .Select(x => x.Cells[ChildLabel].Value.TextValue)
                    .FirstOrDefault() + "x"
            )
        );
    }

    [Fact]
    public void ThrowsOnTheMaximumOfNoRowsOfASubquery()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(parent =>
                db.Children.Where(x =>
                        x.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue + "?"
                    )
                    .Max(x => x.Cells[ChildLabel].Value.TextValue.Length)
            )
        );
    }

    [Fact]
    public void ThrowsOnTheMaximumOfNoRowsInAConstructedResult()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(parent => new ValueTuple<string, int>(
                parent.Cells[ParentId].Value.TextValue,
                db.Children.Where(x =>
                        x.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue + "?"
                    )
                    .Max(x => x.Cells[ChildLabel].Value.TextValue.Length)
            ))
        );
    }

    [Fact]
    public void ThrowsOnTheMaximumOfNoJoinedRowsInAConstructedResult()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.GroupJoin(
                db.Children,
                x => x.Cells[ParentKey].Value.TextValue,
                x => x.Cells[ChildKey].Value.TextValue,
                (parent, children) =>
                    new ValueTuple<string, int>(
                        parent.Cells[ParentId].Value.TextValue,
                        children.Max(x => x.Cells[ChildLabel].Value.TextValue.Length)
                    )
            )
        );
    }

    [Fact]
    public void ThrowsOnANullCellParsedBareIntoAConstructedResult()
    {
        IQueryable<ValueTuple<string, int>> parsed = ExecutionTables
            .Stored(_fixture, _fixture.Connection)
            .Values.Select(x => new ValueTuple<string, int>(
                x.Cells[ValueId].Value.TextValue,
                int.Parse(x.Cells[Number].Value.TextValue)
            ));

        _ = Assert.ThrowsAny<Exception>(() => parsed.ToList());
    }

    [Fact]
    public void ReturnsNullForTheMinimumByAKeyOfNoRows()
    {
        Enumeration.AssertSameValue(db =>
            NoValues(db).MinBy(x => x.Cells[Text].Value.TextValue)
        );
    }

    [Fact]
    public void ReturnsNullForTheMaximumByAKeyOfNoRows()
    {
        Enumeration.AssertSameValue(db =>
            NoValues(db).Select(x => x.Cells[Text].Value.TextValue).MaxBy(x => x)
        );
    }

    [Fact]
    public void ThrowsOnTheMinimumByAKeyOfNoValues()
    {
        Enumeration.AssertSameValue(db =>
            NoValues(db).Select(x => x.Cells[Text].Value.TextValue.Length).MinBy(x => x)
        );
    }

    [Fact]
    public void ReturnsTheGivenDefaultWhenNoFirstElementMatches()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue)
                .FirstOrDefault(x => x == "none", "fallback")
        );
        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue.Length)
                .FirstOrDefault(x => x == 77, -1)
        );
    }

    [Fact]
    public void ReturnsTheGivenDefaultWhenNoSingleElementMatches()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue)
                .SingleOrDefault(x => x == "none", "fallback")
        );
    }

    [Fact]
    public void ReturnsTheGivenDefaultWhenNoLastElementMatches()
    {
        Enumeration.AssertSameValue(db =>
            db.Values.Select(x => x.Cells[Text].Value.TextValue)
                .OrderBy(x => x)
                .LastOrDefault(x => x == "none", "fallback")
        );
    }

    private IQueryable<IRow> NoValues(ExecutionTables db)
    {
        return db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "none");
    }
}
