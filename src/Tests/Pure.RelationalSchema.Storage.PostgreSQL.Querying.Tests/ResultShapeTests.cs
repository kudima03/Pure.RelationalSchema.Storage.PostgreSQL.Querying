using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The objects a translated query builds — tuples, records, rows inside them — must
/// be read as the same query returns them over the enumerated rows in memory.
/// </summary>
public sealed record ResultShapeTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public ResultShapeTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsValueTuplesOfEightItems()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new ValueTuple<
                string,
                string,
                int,
                int,
                bool,
                string,
                string,
                ValueTuple<string>
            >(
                x.Cells[ParentId].Value.TextValue,
                x.Cells[ParentKey].Value.TextValue,
                x.Cells[ParentKey].Value.TextValue.Length,
                4,
                x.Cells[ParentKey].Value.TextValue == "a",
                "six",
                "seven",
                new ValueTuple<string>(x.Cells[ParentId].Value.TextValue + "!")
            ))
        );
    }

    [Fact]
    public void ReadsObjectsBuiltByConstructors()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new Tuple<string, string>(
                x.Cells[ParentId].Value.TextValue,
                x.Cells[ParentKey].Value.TextValue
            ))
        );
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new KeyValuePair<string, string>(
                x.Cells[ParentId].Value.TextValue,
                x.Cells[ParentKey].Value.TextValue
            ))
        );
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new Pair(
                x.Cells[ParentId].Value.TextValue,
                x.Cells[ParentKey].Value.TextValue
            ))
        );
    }

    [Fact]
    public void ReadsMembersOfConstructedRecords()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new Pair(
                    x.Cells[ParentId].Value.TextValue,
                    x.Cells[ParentKey].Value.TextValue
                ))
                .Where(x => x.Second != "")
                .Select(x => x.First)
        );
    }

    [Fact]
    public void ReadsScalarsThroughTheItemsOfValueTuples()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Join(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, child) => new ValueTuple<IRow, IRow>(parent, child)
                )
                .Where(x => x.Item2.Cells[ChildLabel].Value.TextValue != "y")
                .Select(x => new ValueTuple<string, string>(
                    x.Item1.Cells[ParentId].Value.TextValue,
                    x.Item2.Cells[ChildId].Value.TextValue
                ))
        );
    }

    [Fact]
    public void ReadsResultsOfGroupingWithAResultSelector()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.GroupBy(
                x => x.Cells[ChildKey].Value.TextValue,
                (key, children) => new ValueTuple<string, int>(key, children.Count())
            )
        );
    }

    [Fact]
    public void ReadsResultsOfGroupingWithElementAndResultSelectors()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.GroupBy(
                x => x.Cells[ChildKey].Value.TextValue,
                x => x.Cells[ChildLabel].Value.TextValue,
                (key, labels) => new ValueTuple<string, string?>(key, labels.Max())
            )
        );
    }

    [Fact]
    public void ReadsValueTupleGroupKeysAsAWhole()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.GroupBy(x => new ValueTuple<string, string>(
                    x.Cells[ChildKey].Value.TextValue,
                    x.Cells[ChildNumber].Value.TextValue
                ))
                .Select(x => new ValueTuple<ValueTuple<string, string>, int>(
                    x.Key,
                    x.Count()
                ))
        );
    }

    [Fact]
    public void ReadsAnonymousGroupKeysAsAWhole()
    {
        Enumeration.AssertSameElements(db =>
            db.Children.GroupBy(x => new
            {
                Key = x.Cells[ChildKey].Value.TextValue,
                Number = x.Cells[ChildNumber].Value.TextValue,
            })
                .Select(x => new { x.Key, Count = x.Count() })
        );
    }

    [Fact]
    public void ReadsRowsThroughTheItemsOfValueTuples()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Join(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, child) => new ValueTuple<IRow, IRow>(parent, child)
                )
                .Select(x => x.Item2)
        );
    }

    [Fact]
    public void CountsTheCellsOfRowsThroughTheItemsOfValueTuples()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new ValueTuple<IRow, string>(
                    x,
                    x.Cells[ParentKey].Value.TextValue
                ))
                .Select(x => x.Item1.Cells.Count)
        );
    }

    [Fact]
    public void ReadsRowsOfLeftJoins()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.GroupJoin(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, children) => new { Parent = parent, Children = children }
                )
                .SelectMany(
                    x => x.Children.DefaultIfEmpty(),
                    (x, child) => new ValueTuple<IRow, IRow?>(x.Parent, child)
                )
        );
    }

    [Fact]
    public void ReadsObjectsBuiltWithInitializers()
    {
        Enumeration.AssertSameElements(db =>
            db.Parents.Select(x => new Labelled
            {
                Id = x.Cells[ParentId].Value.TextValue,
                Key = x.Cells[ParentKey].Value.TextValue,
            })
        );
    }

    private sealed record Pair(string First, string Second);

    private sealed record Labelled
    {
        public string Id { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;
    }
}
