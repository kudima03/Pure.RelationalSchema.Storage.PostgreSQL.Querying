using System.Globalization;
using System.Runtime.CompilerServices;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// Translated queries over cells of every translated column type, NULL cells among
/// them, must return in one statement what the same query returns over the
/// enumerated rows in memory, or throw the same exception. The theories over
/// <c>nullCells</c> run each query over the rows without NULL cells and over all
/// rows.
/// </summary>
public sealed record ExecutionSemanticsTests : IClassFixture<ExecutionFixture>
{
    private const string Thrown = "throws ";

    private readonly ExecutionFixture _fixture;

    public ExecutionSemanticsTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private IColumn Number => _fixture.Number;

    private IColumn Code => _fixture.Code;

    private IColumn ParentId => _fixture.ParentId;

    private IColumn ParentKey => _fixture.ParentKey;

    private IColumn ParentNumber => _fixture.ParentNumber;

    private IColumn ChildId => _fixture.ChildId;

    private IColumn ChildKey => _fixture.ChildKey;

    private IColumn ChildNumber => _fixture.ChildNumber;

    private IColumn ChildLabel => _fixture.ChildLabel;

    private IColumn EntryId => _fixture.EntryId;

    private IColumn EntryKey => _fixture.EntryKey;

    private IColumn EntryAmount => _fixture.EntryAmount;

    [Theory]
    [InlineData("Text")]
    [InlineData("Flag")]
    [InlineData("Small")]
    [InlineData("Number")]
    [InlineData("Big")]
    [InlineData("Unsigned")]
    [InlineData("Huge")]
    [InlineData("Code")]
    [InlineData("Moment")]
    public void ReadsCellsAsTheirText(string name)
    {
        IColumn column = Column(name);

        AssertLikeEnumeration(db =>
            WithoutNull(db.Values, ValueId)
                .Select(x => new ValueTuple<string, string>(
                    x.Cells[ValueId].Value.TextValue,
                    x.Cells[column].Value.TextValue
                ))
        );
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("Flag")]
    [InlineData("Small")]
    [InlineData("Number")]
    [InlineData("Big")]
    [InlineData("Unsigned")]
    [InlineData("Huge")]
    [InlineData("Code")]
    [InlineData("Moment")]
    public void ReadsNullCellsAsEmptyText(string name)
    {
        IColumn column = Column(name);

        AssertLikeEnumeration(db =>
            db.Values.Select(x => new ValueTuple<string, string>(
                x.Cells[ValueId].Value.TextValue,
                x.Cells[column].Value.TextValue
            ))
        );
    }

    [Fact]
    public void ReadsRowsHoldingNullCells()
    {
        AssertLikeEnumeration(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue != "")
        );
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("Flag")]
    [InlineData("Small")]
    [InlineData("Number")]
    [InlineData("Big")]
    [InlineData("Unsigned")]
    [InlineData("Huge")]
    [InlineData("Code")]
    [InlineData("Moment")]
    public void FiltersRowsByCellsDifferingFromText(string name)
    {
        IColumn column = Column(name);

        AssertLikeEnumeration(db =>
            db.Values.Where(x => x.Cells[column].Value.TextValue != "x")
        );
    }

    [Theory]
    [InlineData("Text", "1")]
    [InlineData("Text", "2")]
    [InlineData("Text", "3")]
    [InlineData("Text", "4")]
    [InlineData("Flag", "1")]
    [InlineData("Flag", "2")]
    [InlineData("Small", "2")]
    [InlineData("Number", "2")]
    [InlineData("Number", "3")]
    [InlineData("Big", "2")]
    [InlineData("Big", "3")]
    [InlineData("Unsigned", "2")]
    [InlineData("Huge", "2")]
    [InlineData("Huge", "3")]
    [InlineData("Code", "2")]
    [InlineData("Code", "4")]
    [InlineData("Moment", "2")]
    [InlineData("Moment", "3")]
    [InlineData("Moment", "4")]
    [InlineData("Moment", "92")]
    public void FiltersRowsByCellText(string name, string id)
    {
        IColumn column = Column(name);
        string text = EnumeratedText(column, id);

        AssertLikeEnumeration(db =>
            db.Values.Where(x => x.Cells[column].Value.TextValue == text)
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("Flag")]
    [InlineData("Number")]
    [InlineData("Code")]
    [InlineData("Moment")]
    public void FiltersRowsByNonEmptyCells(string name)
    {
        IColumn column = Column(name);

        AssertLikeEnumeration(db =>
            db.Values.Where(x => x.Cells[column].Value.TextValue != "")
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("Flag")]
    [InlineData("Number")]
    [InlineData("Code")]
    [InlineData("Moment")]
    public void FiltersRowsByEmptyCells(string name)
    {
        IColumn column = Column(name);

        AssertLikeEnumeration(db =>
            db.Values.Where(x => x.Cells[column].Value.TextValue == "")
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersRowsByNullOrEmptyCells()
    {
        AssertLikeEnumeration(db =>
            db.Values.Where(x => string.IsNullOrEmpty(x.Cells[Text].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersRowsByNegatedComparisons()
    {
        AssertLikeEnumeration(db =>
            db.Parents.Where(x => !(x.Cells[ParentKey].Value.TextValue == "a"))
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Fact]
    public void FiltersRowsByConjunctionsAndDisjunctions()
    {
        AssertLikeEnumeration(db =>
            db.Children.Where(x =>
                    (
                        x.Cells[ChildKey].Value.TextValue == "a"
                        && x.Cells[ChildLabel].Value.TextValue != "x"
                    )
                    || x.Cells[ChildNumber].Value.TextValue == "3"
                )
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiltersRowsByMembershipOfCells(bool nullCells)
    {
        string[] keys = ["a", ""];

        AssertLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .Where(x => keys.Contains(x.Cells[ChildKey].Value.TextValue))
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiltersRowsByMembershipOfNumberCells(bool nullCells)
    {
        string[] numbers = ["0", "-1", "2147483647"];

        AssertLikeEnumeration(db =>
            Rows(db.Values, ValueId, nullCells)
                .Where(x => !numbers.Contains(x.Cells[Number].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiltersRowsByTheLengthOfCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .Where(x => x.Cells[ChildKey].Value.TextValue.Length < 1)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectsConcatenatedCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Values, ValueId, nullCells)
                .Select(x =>
                    x.Cells[ValueId].Value.TextValue
                    + "|"
                    + x.Cells[Flag].Value.TextValue
                    + "|"
                    + x.Cells[Number].Value.TextValue
                    + "|"
                    + x.Cells[Code].Value.TextValue
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectsConditionsOnCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Values, ValueId, nullCells)
                .Select(x => new ValueTuple<string, string>(
                    x.Cells[ValueId].Value.TextValue,
                    x.Cells[Flag].Value.TextValue == "True" ? "yes"
                        : x.Cells[Flag].Value.TextValue == "False" ? "no"
                        : "none"
                ))
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdersRowsByCells(bool nullCells)
    {
        AssertOrderedLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .OrderBy(x => x.Cells[ParentKey].Value.TextValue)
                .ThenBy(x => x.Cells[ParentId].Value.TextValue)
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdersRowsByCellsOfEmptyAndNullText(bool nullCells)
    {
        AssertOrderedLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .OrderBy(x => x.Cells[ChildKey].Value.TextValue)
                .ThenByDescending(x => x.Cells[ChildId].Value.TextValue)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdersRowsByNumberCellsDescending(bool nullCells)
    {
        AssertOrderedLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .OrderByDescending(x => x.Cells[ParentNumber].Value.TextValue)
                .ThenByDescending(x => x.Cells[ParentId].Value.TextValue)
                .Select(x => x.Cells[ParentId].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdersRowsByTheLengthOfCells(bool nullCells)
    {
        AssertOrderedLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .OrderBy(x => x.Cells[ChildLabel].Value.TextValue.Length)
                .ThenBy(x => x.Cells[ChildId].Value.TextValue)
                .Select(x => x.Cells[ChildId].Value.TextValue)
        );
    }

    [Fact]
    public void PagesOrderedCells()
    {
        AssertOrderedLikeEnumeration(db =>
            db.Children.OrderBy(x => x.Cells[ChildId].Value.TextValue)
                .Skip(1)
                .Take(4)
                .Select(x => new ValueTuple<string, string>(
                    x.Cells[ChildId].Value.TextValue,
                    x.Cells[ChildLabel].Value.TextValue
                ))
        );
    }

    [Fact]
    public void PagesOrderedRows()
    {
        AssertOrderedLikeEnumeration(db =>
            db.Values.OrderByDescending(x => x.Cells[ValueId].Value.TextValue)
                .Skip(1)
                .Take(4)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovesDuplicateCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Values, ValueId, nullCells)
                .Select(x => x.Cells[Text].Value.TextValue)
                .Distinct()
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovesDuplicateTuplesOfCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Values, ValueId, nullCells)
                .Select(x => new ValueTuple<string, string>(
                    x.Cells[Flag].Value.TextValue,
                    x.Cells[Code].Value.TextValue
                ))
                .Distinct()
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JoinsRowsOnCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .Join(
                    Rows(db.Children, ChildId, nullCells),
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, child) =>
                        new ValueTuple<string, string>(
                            parent.Cells[ParentId].Value.TextValue,
                            child.Cells[ChildId].Value.TextValue
                        )
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JoinsRowsOnNumberCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .Join(
                    Rows(db.Children, ChildId, nullCells),
                    x => x.Cells[ParentNumber].Value.TextValue,
                    x => x.Cells[ChildNumber].Value.TextValue,
                    (parent, child) =>
                        new ValueTuple<string, string>(
                            parent.Cells[ParentId].Value.TextValue,
                            child.Cells[ChildId].Value.TextValue
                        )
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JoinsRowsOnCompositeKeys(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .Join(
                    Rows(db.Children, ChildId, nullCells),
                    x => new ValueTuple<string, string>(
                        x.Cells[ParentKey].Value.TextValue,
                        x.Cells[ParentNumber].Value.TextValue
                    ),
                    x => new ValueTuple<string, string>(
                        x.Cells[ChildKey].Value.TextValue,
                        x.Cells[ChildNumber].Value.TextValue
                    ),
                    (parent, child) =>
                        new ValueTuple<string, string>(
                            parent.Cells[ParentId].Value.TextValue,
                            child.Cells[ChildId].Value.TextValue
                        )
                )
        );
    }

    [Fact]
    public void JoinsRowsReturningRows()
    {
        AssertLikeEnumeration(db =>
            db.Parents.Where(x => x.Cells[ParentKey].Value.TextValue == "a")
                .Join(
                    db.Children,
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, child) => child
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeftJoinsRowsOnCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .GroupJoin(
                    Rows(db.Children, ChildId, nullCells),
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[ChildKey].Value.TextValue,
                    (parent, children) => new { Parent = parent, Children = children }
                )
                .SelectMany(
                    x => x.Children.DefaultIfEmpty(),
                    (x, child) =>
                        new ValueTuple<string, string>(
                            x.Parent.Cells[ParentId].Value.TextValue,
                            child == null ? "-" : child.Cells[ChildLabel].Value.TextValue
                        )
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountsGroupsOfJoinedRows(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .GroupJoin(
                    Rows(db.Children, ChildId, nullCells),
                    x => x.Cells[ParentNumber].Value.TextValue,
                    x => x.Cells[ChildNumber].Value.TextValue,
                    (parent, children) =>
                        parent.Cells[ParentId].Value.TextValue
                        + ":"
                        + children.Count().ToString()
                )
        );
    }

    [Fact]
    public void ReadsTuplesOfGroupsOfJoinedRows()
    {
        AssertLikeEnumeration(db =>
            WithoutNull(db.Parents, ParentId)
                .GroupJoin(
                    WithoutNull(db.Children, ChildId),
                    x => x.Cells[ParentNumber].Value.TextValue,
                    x => x.Cells[ChildNumber].Value.TextValue,
                    (parent, children) =>
                        new ValueTuple<string, int>(
                            parent.Cells[ParentId].Value.TextValue,
                            children.Count()
                        )
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JoinsCorrelatedRows(bool nullCells)
    {
        AssertLikeEnumeration(db =>
        {
            IQueryable<IRow> children = Rows(db.Children, ChildId, nullCells);
            return Rows(db.Parents, ParentId, nullCells)
                .SelectMany(
                    parent =>
                        children.Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        ),
                    (parent, child) =>
                        new ValueTuple<string, string>(
                            parent.Cells[ParentId].Value.TextValue,
                            child.Cells[ChildId].Value.TextValue
                        )
                );
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossJoinsRowsOfTwoSchemas(bool nullCells)
    {
        AssertLikeEnumeration(db =>
        {
            IQueryable<IRow> entries = Rows(db.Entries, EntryId, nullCells);
            return Rows(db.Parents, ParentId, nullCells)
                .SelectMany(
                    x => entries,
                    (parent, entry) =>
                        new ValueTuple<string, string>(
                            parent.Cells[ParentKey].Value.TextValue,
                            entry.Cells[EntryAmount].Value.TextValue
                        )
                );
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JoinsRowsOfTwoSchemas(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .Join(
                    Rows(db.Entries, EntryId, nullCells),
                    x => x.Cells[ParentKey].Value.TextValue,
                    x => x.Cells[EntryKey].Value.TextValue,
                    (parent, entry) =>
                        new ValueTuple<string, string>(
                            parent.Cells[ParentId].Value.TextValue,
                            entry.Cells[EntryAmount].Value.TextValue
                        )
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupsRowsByCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .GroupBy(x => x.Cells[ChildKey].Value.TextValue)
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupsRowsByBoolCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Values, ValueId, nullCells)
                .GroupBy(x => x.Cells[Flag].Value.TextValue)
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AggregatesGroupsOfCells(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .GroupBy(
                    x => x.Cells[ChildNumber].Value.TextValue,
                    x => x.Cells[ChildLabel].Value.TextValue
                )
                .Select(x => new ValueTuple<string, string?, string?, int, int>(
                    x.Key,
                    x.Min(),
                    x.Max(),
                    x.Sum(label => label.Length),
                    x.Count(label => label.Length > 0)
                ))
        );
    }

    [Fact]
    public void FiltersGroups()
    {
        AssertLikeEnumeration(db =>
            db.Parents.GroupBy(x => x.Cells[ParentNumber].Value.TextValue)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
        );
    }

    [Fact]
    public void CountsRows()
    {
        AssertValueLikeEnumeration(db => db.Values.LongCount());
    }

    [Fact]
    public void CountsRowsOfCellsDifferingFromText()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.Count(x => x.Cells[ChildLabel].Value.TextValue != "x")
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountsRowsOfEmptyCells(bool nullCells)
    {
        AssertValueLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .Count(x => x.Cells[ChildKey].Value.TextValue.Length == 0)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SumsTheLengthsOfCells(bool nullCells)
    {
        AssertValueLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .Sum(x => x.Cells[ChildKey].Value.TextValue.Length)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AveragesTheLengthsOfCells(bool nullCells)
    {
        AssertValueLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .Average(x => x.Cells[ChildKey].Value.TextValue.Length)
        );
    }

    [Fact]
    public void FindsTheMinimumOfCells()
    {
        AssertValueLikeEnumeration(db =>
            db.Parents.Min(x => x.Cells[ParentKey].Value.TextValue)
        );
    }

    [Fact]
    public void FindsTheMaximumOfCells()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.Max(x => x.Cells[ChildLabel].Value.TextValue)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnswersWhetherAnyCellIsEmpty(bool nullCells)
    {
        AssertValueLikeEnumeration(db =>
            Rows(db.Entries, EntryId, nullCells)
                .Any(x => x.Cells[EntryAmount].Value.TextValue == "")
        );
    }

    [Fact]
    public void AnswersWhetherAnyCellMatches()
    {
        AssertValueLikeEnumeration(db =>
            db.Entries.Any(x => x.Cells[EntryKey].Value.TextValue == "b")
        );
    }

    [Fact]
    public void AnswersWhetherAnyRowIsLeft()
    {
        AssertValueLikeEnumeration(db =>
            db.Entries.Where(x => x.Cells[EntryKey].Value.TextValue == "z").Any()
        );
    }

    [Fact]
    public void AnswersWhetherAllCellsAreNotEmpty()
    {
        AssertValueLikeEnumeration(db =>
            db.Entries.All(x => x.Cells[EntryKey].Value.TextValue != "")
        );
    }

    [Fact]
    public void AnswersWhetherAllCellsDifferFromText()
    {
        AssertValueLikeEnumeration(db =>
            db.Entries.All(x => x.Cells[EntryKey].Value.TextValue != "c")
        );
    }

    [Fact]
    public void ReadsTheFirstOrderedRow()
    {
        AssertValueLikeEnumeration(db =>
            db.Values.OrderByDescending(x => x.Cells[ValueId].Value.TextValue).First()
        );
    }

    [Fact]
    public void ReadsTheLastOrderedRow()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.OrderByDescending(x => x.Cells[ChildId].Value.TextValue).Last()
        );
    }

    [Fact]
    public void ReadsTheFirstMatchingCell()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.OrderBy(x => x.Cells[ChildId].Value.TextValue)
                .Where(x => x.Cells[ChildNumber].Value.TextValue == "0")
                .Select(x => x.Cells[ChildLabel].Value.TextValue)
                .FirstOrDefault()
        );
    }

    [Fact]
    public void ReadsTheOrderedCellAtAnIndex()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.OrderBy(x => x.Cells[ChildId].Value.TextValue)
                .Select(x => x.Cells[ChildLabel].Value.TextValue)
                .ElementAt(2)
        );
    }

    [Fact]
    public void ThrowsWhenNoRowIsFirst()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.Where(x => x.Cells[ChildKey].Value.TextValue == "none").First()
        );
    }

    [Fact]
    public void ThrowsWhenTheSingleRowIsNotSingle()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.Single(x => x.Cells[ChildKey].Value.TextValue == "a")
        );
    }

    [Fact]
    public void FindsCellsInTheTable()
    {
        AssertValueLikeEnumeration(db =>
            db.Children.Select(x => x.Cells[ChildLabel].Value.TextValue).Contains("v")
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FindsNumberCellsInTheTable(bool nullCells)
    {
        AssertValueLikeEnumeration(db =>
            Rows(db.Entries, EntryId, nullCells)
                .Select(x => x.Cells[EntryAmount].Value.TextValue)
                .Contains("7")
        );
    }

    [Fact]
    public void FiltersRowsByMembershipInASubquery()
    {
        AssertLikeEnumeration(db =>
        {
            IQueryable<string> keys = db.Children.Select(x =>
                x.Cells[ChildKey].Value.TextValue
            );
            return db
                .Parents.Where(x => keys.Contains(x.Cells[ParentKey].Value.TextValue))
                .Select(x => x.Cells[ParentId].Value.TextValue);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiltersRowsByCorrelatedExistence(bool nullCells)
    {
        AssertLikeEnumeration(db =>
        {
            IQueryable<IRow> children = Rows(db.Children, ChildId, nullCells);
            return Rows(db.Parents, ParentId, nullCells)
                .Where(x =>
                    children.Any(child =>
                        child.Cells[ChildNumber].Value.TextValue
                        == x.Cells[ParentNumber].Value.TextValue
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiltersAndProjectsRowsByManySubqueries(bool nullCells)
    {
        AssertLikeEnumeration(db =>
        {
            IQueryable<IRow> children = Rows(db.Children, ChildId, nullCells);
            IQueryable<IRow> entries = Rows(db.Entries, EntryId, nullCells);
            IQueryable<string> keys = children
                .Where(x => x.Cells[ChildNumber].Value.TextValue == "1")
                .Select(x => x.Cells[ChildKey].Value.TextValue);
            return Rows(db.Parents, ParentId, nullCells)
                .Where(parent =>
                    children.Any(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                    || entries.Any(entry =>
                        entry.Cells[EntryKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    )
                )
                .Select(parent => new
                {
                    Id = parent.Cells[ParentId].Value.TextValue,
                    Children = children.Count(child =>
                        child.Cells[ChildKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    ),
                    Label = children
                        .Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Max(child => child.Cells[ChildLabel].Value.TextValue),
                    SameNumbers = children
                        .Where(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .All(child =>
                            child.Cells[ChildNumber].Value.TextValue
                            == parent.Cells[ParentNumber].Value.TextValue
                        ),
                    Entries = entries.LongCount(entry =>
                        entry.Cells[EntryKey].Value.TextValue
                        == parent.Cells[ParentKey].Value.TextValue
                    ),
                    Amounts = entries
                        .Where(entry =>
                            entry.Cells[EntryKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                        .Sum(entry => entry.Cells[EntryAmount].Value.TextValue.Length),
                    Keyed = keys.Contains(parent.Cells[ParentKey].Value.TextValue),
                    Larger = entries.Any(entry =>
                        entry.Cells[EntryAmount].Value.TextValue.Length
                        > children.Count(child =>
                            child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        )
                    ),
                });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiltersRowsBySubqueriesNestedThreeDeep(bool nullCells)
    {
        AssertLikeEnumeration(db =>
        {
            IQueryable<IRow> parents = Rows(db.Parents, ParentId, nullCells);
            IQueryable<IRow> children = Rows(db.Children, ChildId, nullCells);
            IQueryable<IRow> entries = Rows(db.Entries, EntryId, nullCells);
            return parents
                .Where(parent =>
                    children.Any(child =>
                        child.Cells[ChildKey].Value.TextValue
                            == parent.Cells[ParentKey].Value.TextValue
                        && entries.Any(entry =>
                            entry.Cells[EntryKey].Value.TextValue
                                == child.Cells[ChildKey].Value.TextValue
                            && parents.Any(other =>
                                other.Cells[ParentNumber].Value.TextValue
                                    == child.Cells[ChildNumber].Value.TextValue
                                && other.Cells[ParentId].Value.TextValue
                                    != parent.Cells[ParentId].Value.TextValue
                            )
                        )
                    )
                )
                .Select(x => x.Cells[ParentId].Value.TextValue);
        });
    }

    [Fact]
    public void ConcatenatesRowsOfTheTable()
    {
        AssertLikeEnumeration(db =>
            db.Parents.Where(x => x.Cells[ParentKey].Value.TextValue == "a")
                .Concat(
                    db.Parents.Where(x => x.Cells[ParentNumber].Value.TextValue == "1")
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnitesCellsOfTheTable(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .Where(x => x.Cells[ParentKey].Value.TextValue != "b")
                .Select(x => x.Cells[ParentKey].Value.TextValue)
                .Union(
                    Rows(db.Parents, ParentId, nullCells)
                        .Where(x => x.Cells[ParentNumber].Value.TextValue != "1")
                        .Select(x => x.Cells[ParentKey].Value.TextValue)
                )
        );
    }

    [Fact]
    public void UnitesTuplesOfCells()
    {
        AssertLikeEnumeration(db =>
            WithoutNull(db.Parents, ParentId)
                .Select(x => new ValueTuple<string, string>(
                    x.Cells[ParentKey].Value.TextValue,
                    x.Cells[ParentNumber].Value.TextValue
                ))
                .Union(
                    WithoutNull(db.Parents, ParentId)
                        .Select(x => new ValueTuple<string, string>(
                            x.Cells[ParentKey].Value.TextValue,
                            x.Cells[ParentNumber].Value.TextValue
                        ))
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IntersectsCellsOfTheTable(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .Where(x => x.Cells[ParentKey].Value.TextValue != "b")
                .Select(x => x.Cells[ParentId].Value.TextValue)
                .Intersect(
                    Rows(db.Parents, ParentId, nullCells)
                        .Where(x => x.Cells[ParentNumber].Value.TextValue != "1")
                        .Select(x => x.Cells[ParentId].Value.TextValue)
                )
        );
    }

    [Fact]
    public void ExceptsCellsOfTheTable()
    {
        AssertLikeEnumeration(db =>
            db.Values.Select(x => x.Cells[ValueId].Value.TextValue)
                .Except(
                    db.Values.Where(x => x.Cells[Text].Value.TextValue == "plain")
                        .Select(x => x.Cells[ValueId].Value.TextValue)
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnitesCellsOfTwoTables(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Parents, ParentId, nullCells)
                .Select(x => x.Cells[ParentKey].Value.TextValue)
                .Union(
                    Rows(db.Children, ChildId, nullCells)
                        .Select(x => x.Cells[ChildKey].Value.TextValue)
                )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExceptsCellsOfTwoTables(bool nullCells)
    {
        AssertLikeEnumeration(db =>
            Rows(db.Children, ChildId, nullCells)
                .Select(x => x.Cells[ChildKey].Value.TextValue)
                .Except(
                    Rows(db.Parents, ParentId, nullCells)
                        .Select(x => x.Cells[ParentKey].Value.TextValue)
                )
        );
    }

    [Theory]
    [InlineData("Day")]
    [InlineData("Clock")]
    [InlineData("Measure")]
    [InlineData("Ratio")]
    [InlineData("Digest")]
    public void ReadsCellsOfOtherTypesAsTheirText(string name)
    {
        IColumn column = _fixture.Extras.Columns.Single(x => x.Name.TextValue == name);

        AssertLikeEnumeration(db =>
            db.Extras.Select(x => new ValueTuple<string, string>(
                x.Cells[_fixture.ExtraId].Value.TextValue,
                x.Cells[column].Value.TextValue
            ))
        );
    }

    [Fact]
    public void ReadsDateTimeValuesNextToCells()
    {
        DateTime since = new DateTime(2000, 1, 1);

        AssertLikeEnumeration(db =>
            WithoutNull(db.Values, ValueId)
                .Select(x => new ValueTuple<string, DateTime>(
                    x.Cells[Text].Value.TextValue,
                    since
                ))
        );
    }

    /// <summary>
    /// The rows, or only those without NULL cells, whose ids do not start with 9.
    /// </summary>
    private static IQueryable<IRow> Rows(
        IQueryable<IRow> rows,
        IColumn id,
        bool nullCells
    )
    {
        return nullCells ? rows : WithoutNull(rows, id);
    }

    private static IQueryable<IRow> WithoutNull(IQueryable<IRow> rows, IColumn id)
    {
        return rows.Where(x => !x.Cells[id].Value.TextValue.StartsWith('9'));
    }

    private IColumn Column(string name)
    {
        return _fixture.Values.Columns.Single(x => x.Name.TextValue == name);
    }

    /// <summary>
    /// The text enumeration gives the cell of the column in the row of the id.
    /// </summary>
    private string EnumeratedText(IColumn column, string id)
    {
        return new PostgreSqlStoredSchemaDataSet(_fixture.Store, _fixture.Connection)[
            _fixture.Values
        ]
            .AsEnumerable()
            .Single(x => x.Cells[ValueId].Value.TextValue == id)
            .Cells[column]
            .Value.TextValue;
    }

    /// <summary>
    /// The query returns the same elements, in any order, over the stored tables in
    /// one statement as over their enumerated rows.
    /// </summary>
    private void AssertLikeEnumeration<T>(Func<ExecutionTables, IQueryable<T>> query)
    {
        AssertSameOutcome(tables =>
            query(tables).AsEnumerable().Select(Canonical).Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// The query returns the same elements in the same order over the stored tables
    /// in one statement as over their enumerated rows.
    /// </summary>
    private void AssertOrderedLikeEnumeration<T>(
        Func<ExecutionTables, IQueryable<T>> query
    )
    {
        AssertSameOutcome(tables => query(tables).AsEnumerable().Select(Canonical));
    }

    /// <summary>
    /// The query returns the same value over the stored tables in one statement as
    /// over their enumerated rows.
    /// </summary>
    private void AssertValueLikeEnumeration<T>(Func<ExecutionTables, T> query)
    {
        AssertSameOutcome(tables => [Canonical(query(tables))]);
    }

    private void AssertSameOutcome(Func<ExecutionTables, IEnumerable<string>> query)
    {
        string[] expected = Outcome(() => query(ExecutionTables.Enumerated(_fixture)));

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        string[] actual = Outcome(() =>
            query(ExecutionTables.Stored(_fixture, connection))
        );

        Assert.Equal(expected, actual);
        if (!expected[0].StartsWith(Thrown, StringComparison.Ordinal))
        {
            _ = Assert.Single(connection.Statements);
        }
    }

    /// <summary>
    /// The elements the query returns, or the type of the exception it throws.
    /// </summary>
    private static string[] Outcome(Func<IEnumerable<string>> query)
    {
        try
        {
            return [.. query()];
        }
        catch (Exception exception)
        {
            return [Thrown + exception.GetType().FullName];
        }
    }

    /// <summary>
    /// The text of an element that tells null from empty text and shows the cells of
    /// rows, in the order of their column names, and the items of tuples.
    /// </summary>
    private static string Canonical<T>(T value)
    {
        return value switch
        {
            null => "null",
            string text => "\"" + text + "\"",
            IRow row => "row("
                + string.Join(
                    ", ",
                    row.Cells.OrderBy(x => x.Key.Name.TextValue, StringComparer.Ordinal)
                        .Select(x => Canonical(x.Value.Value.TextValue))
                )
                + ")",
            ITuple tuple => "("
                + string.Join(
                    ", ",
                    Enumerable.Range(0, tuple.Length).Select(x => Canonical(tuple[x]))
                )
                + ")",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        };
    }
}
