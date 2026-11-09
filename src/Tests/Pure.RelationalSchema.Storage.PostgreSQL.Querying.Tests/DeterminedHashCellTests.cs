using Pure.RelationalSchema.Abstractions.Column;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Cells of a <c>determined_hash</c> column, a <c>bytea</c> whose bytes enumeration
/// reads as UTF-8 text, must be in a translated query the text they are in the same
/// query over the enumerated rows in memory.
/// </summary>
public sealed record DeterminedHashCellTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public DeterminedHashCellTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Digest => _fixture.Digest;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void FiltersRowsByDeterminedHashCells()
    {
        string[] wanted = ["abc", ""];

        Enumeration.AssertSameElements(db =>
            db.Extras.Where(x => x.Cells[Digest].Value.TextValue != "abc")
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Where(x => string.IsNullOrEmpty(x.Cells[Digest].Value.TextValue))
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Where(x => wanted.Contains(x.Cells[Digest].Value.TextValue))
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Where(x => x.Cells[Digest].Value.TextValue.StartsWith('a'))
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
    }

    [Fact]
    public void GroupsAndOrdersRowsByDeterminedHashCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Extras.GroupBy(x => x.Cells[Digest].Value.TextValue)
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
        Enumeration.AssertSameOrder(db =>
            db.Extras.OrderBy(x => x.Cells[Digest].Value.TextValue)
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x => x.Cells[Digest].Value.TextValue).Distinct()
        );
    }

    [Fact]
    public void JoinsRowsOnDeterminedHashCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Extras.Join(
                db.Extras,
                x => x.Cells[Digest].Value.TextValue,
                x => x.Cells[Digest].Value.TextValue,
                (left, right) =>
                    new ValueTuple<string, string>(
                        left.Cells[ExtraId].Value.TextValue,
                        right.Cells[ExtraId].Value.TextValue
                    )
            )
        );
    }

    [Fact]
    public void ProjectsConditionsAndConcatenationsOfDeterminedHashCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x =>
                x.Cells[Digest].Value.TextValue == "abc"
                    ? "known"
                    : x.Cells[Digest].Value.TextValue + "?"
            )
        );
    }

    [Fact]
    public void JoinsDeterminedHashCellsToTextCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Extras.Join(
                db.Parents,
                x => x.Cells[Digest].Value.TextValue,
                x => x.Cells[ParentKey].Value.TextValue,
                (extra, parent) =>
                    new ValueTuple<string, string>(
                        extra.Cells[ExtraId].Value.TextValue,
                        parent.Cells[ParentId].Value.TextValue
                    )
            )
        );
    }

    [Fact]
    public void UnitesDeterminedHashCellsWithTextCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x => x.Cells[Digest].Value.TextValue)
                .Union(db.Parents.Select(x => x.Cells[ParentKey].Value.TextValue))
        );
    }

    [Fact]
    public void ConvertsTheCaseOfDeterminedHashCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x => x.Cells[Digest].Value.TextValue.ToUpperInvariant())
        );
    }

    [Fact]
    public void TrimsDeterminedHashCells()
    {
        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x => x.Cells[Digest].Value.TextValue.Trim())
        );
    }
}
