using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The cell text of a <c>time</c> is <c>HH:mm:ss</c> and that of a <c>datetime</c>
/// <c>MM/dd/yyyy HH:mm:ss</c>, each followed by the fraction of its second without
/// trailing zeros when it has one, and NULL is empty text (issue #101): in a
/// translated query, in each position a cell is read, and when the table is
/// enumerated, so that the text parses back to the value stored, as the parse of
/// the column read whole gives it.
/// </summary>
public sealed record TemporalCellTextTests : IClassFixture<ExecutionFixture>
{
    private const string TimeFormat = "HH:mm:ss.FFFFFFF";

    private const string DateTimeFormat = "MM/dd/yyyy HH:mm:ss.FFFFFFF";

    private readonly ExecutionFixture _fixture;

    public TemporalCellTextTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Moment => _fixture.Moment;

    private IColumn ExtraId => _fixture.ExtraId;

    private IColumn Clock => _fixture.Clock;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Theory]
    [InlineData("1", "03:04:05")]
    [InlineData("2", "23:59:59.5")]
    [InlineData("3", "00:00:00")]
    [InlineData("4", "23:59:59.999999")]
    [InlineData("5", "12:00:00.1")]
    [InlineData("6", "00:00:00.000001")]
    [InlineData("7", "07:08:09.12")]
    [InlineData("8", "23:59:59")]
    [InlineData("10", "13:14:15.00001")]
    [InlineData("11", "00:00:59.999")]
    [InlineData("91", "")]
    public void WritesTimeCellsWithTheirSecondsAndTheirFraction(string id, string text)
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);

        Assert.Equal([text], Translated(db.Extras, ExtraId, Clock, id));
        Assert.Equal(text, Enumerated(db.Extras, ExtraId, Clock, id));
    }

    [Theory]
    [InlineData("1", "01/01/2000 00:00:00")]
    [InlineData("2", "01/01/0001 00:00:00")]
    [InlineData("3", "12/31/9999 23:59:59.999999")]
    [InlineData("4", "02/03/0999 04:05:06.5")]
    [InlineData("7", "02/29/2024 23:59:59")]
    [InlineData("8", "12/31/2024 12:34:56.12")]
    [InlineData("10", "01/01/0001 00:00:00.000001")]
    [InlineData("11", "06/15/2000 08:09:10.00001")]
    [InlineData("12", "12/31/9999 23:59:59")]
    [InlineData("13", "12/31/1999 23:59:59.9")]
    [InlineData("92", "01/01/1970 00:00:00.000001")]
    [InlineData("91", "")]
    public void WritesDateTimeCellsWithTheirSecondsAndTheirFraction(string id, string text)
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);

        Assert.Equal([text], Translated(db.Values, ValueId, Moment, id));
        Assert.Equal(text, Enumerated(db.Values, ValueId, Moment, id));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("7")]
    [InlineData("8")]
    [InlineData("10")]
    [InlineData("11")]
    public void ParsesTimeCellTextBackToTheStoredValue(string id)
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);
        string text = Enumerated(db.Extras, ExtraId, Clock, id);

        TimeOnly stored = db
            .Extras.Where(x => x.Cells[ExtraId].Value.TextValue == id)
            .Select(x =>
                TimeOnly.Parse(x.Cells[Clock].Value.TextValue, CultureInfo.InvariantCulture)
            )
            .Single();

        Assert.Equal(stored, TimeOnly.Parse(text, CultureInfo.InvariantCulture));
        Assert.Equal(text, stored.ToString(TimeFormat, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("6")]
    [InlineData("7")]
    [InlineData("8")]
    [InlineData("10")]
    [InlineData("11")]
    [InlineData("12")]
    [InlineData("13")]
    [InlineData("92")]
    public void ParsesDateTimeCellTextBackToTheStoredValue(string id)
    {
        ExecutionTables db = ExecutionTables.Stored(_fixture, _fixture.Connection);
        string text = Enumerated(db.Values, ValueId, Moment, id);

        DateTime stored = db
            .Values.Where(x => x.Cells[ValueId].Value.TextValue == id)
            .Select(x =>
                DateTime.Parse(x.Cells[Moment].Value.TextValue, CultureInfo.InvariantCulture)
            )
            .Single();

        Assert.Equal(stored, DateTime.Parse(text, CultureInfo.InvariantCulture));
        Assert.Equal(text, stored.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("03:04:05", "1")]
    [InlineData("23:59:59.5", "2")]
    [InlineData("00:00:00", "3")]
    [InlineData("23:59:59.999999", "4")]
    [InlineData("12:00:00.1", "5")]
    [InlineData("00:00:00.000001", "6")]
    [InlineData("07:08:09.12", "7")]
    [InlineData("23:59:59", "8")]
    [InlineData("13:14:15.00001", "10")]
    [InlineData("00:00:59.999", "11")]
    [InlineData("", "91")]
    public void FiltersRowsByTimeCellText(string text, string id)
    {
        Assert.Equal([id], Ids(db => FilteredExtras(db, text)));
        Enumeration.AssertSameElements(db => FilteredExtras(db, text));
    }

    [Theory]
    [InlineData("12:00:00.100000")]
    [InlineData("23:59")]
    [InlineData("23:59:59.9999990")]
    [InlineData("3:04:05")]
    public void FindsNoTimeCellsByOtherSpellings(string text)
    {
        Assert.Empty(Ids(db => FilteredExtras(db, text)));
        Enumeration.AssertSameValue(db => FilteredExtras(db, text).Count());
    }

    [Theory]
    [InlineData("01/01/2000 00:00:00", "1,6")]
    [InlineData("12/31/9999 23:59:59.999999", "3")]
    [InlineData("02/03/0999 04:05:06.5", "4")]
    [InlineData("12/31/2024 12:34:56.12", "8")]
    [InlineData("01/01/0001 00:00:00.000001", "10")]
    [InlineData("06/15/2000 08:09:10.00001", "11")]
    [InlineData("12/31/1999 23:59:59.9", "13")]
    [InlineData("01/01/1970 00:00:00.000001", "92")]
    [InlineData("", "91")]
    public void FiltersRowsByDateTimeCellText(string text, string ids)
    {
        Assert.Equal(ids.Split(','), Ids(db => FilteredValues(db, text)));
        Enumeration.AssertSameElements(db => FilteredValues(db, text));
    }

    [Theory]
    [InlineData("12/31/2024 12:34:56.120")]
    [InlineData("12/31/2024 12:34:56")]
    [InlineData("12/31/9999 23:59:59.9999990")]
    [InlineData("2024-12-31 12:34:56.12")]
    public void FindsNoDateTimeCellsByOtherSpellings(string text)
    {
        Assert.Empty(Ids(db => FilteredValues(db, text)));
        Enumeration.AssertSameValue(db => FilteredValues(db, text).Count());
    }

    [Fact]
    public void WritesNoFractionWithTrailingZeros()
    {
        Assert.Empty(
            Ids(db =>
                db.Extras.Where(x =>
                        x.Cells[Clock].Value.TextValue.Contains('.')
                        && x.Cells[Clock].Value.TextValue.EndsWith('0')
                    )
                    .Select(x => x.Cells[ExtraId].Value.TextValue)
            )
        );
        Assert.Empty(
            Ids(db =>
                db.Values.Where(x =>
                        x.Cells[Moment].Value.TextValue.Contains('.')
                        && x.Cells[Moment].Value.TextValue.EndsWith('0')
                    )
                    .Select(x => x.Cells[ValueId].Value.TextValue)
            )
        );
    }

    [Fact]
    public void ReadsTimeCellsInEveryPosition()
    {
        string[] wanted = ["23:59:59.999999", "00:00:00", "12:00:00.1", "23:59"];

        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x => new ValueTuple<string, string, int>(
                x.Cells[ExtraId].Value.TextValue,
                x.Cells[Clock].Value.TextValue,
                x.Cells[Clock].Value.TextValue.Length
            ))
        );
        Enumeration.AssertSameOrder(db =>
            db.Extras.OrderBy(x => x.Cells[Clock].Value.TextValue)
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Where(x => wanted.Contains(x.Cells[Clock].Value.TextValue))
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Where(x => x.Cells[Clock].Value.TextValue != "")
                .GroupBy(x => x.Cells[Clock].Value.TextValue.Substring(0, 8))
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Join(
                db.Extras,
                x => x.Cells[Clock].Value.TextValue,
                x => x.Cells[Clock].Value.TextValue,
                (left, right) => new ValueTuple<string, string>(
                    left.Cells[ExtraId].Value.TextValue,
                    right.Cells[ExtraId].Value.TextValue
                )
            )
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x => x.Cells[Clock].Value.TextValue).Distinct()
        );
        Enumeration.AssertSameValue(db =>
            db.Extras.Max(x => x.Cells[Clock].Value.TextValue)
        );
    }

    [Fact]
    public void ReadsDateTimeCellsInEveryPosition()
    {
        string[] wanted = ["12/31/9999 23:59:59.999999", "02/29/2024 23:59:59", ""];

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string, int>(
                x.Cells[ValueId].Value.TextValue,
                x.Cells[Moment].Value.TextValue,
                x.Cells[Moment].Value.TextValue.Length
            ))
        );
        Enumeration.AssertSameOrder(db =>
            db.Values.OrderBy(x => x.Cells[Moment].Value.TextValue)
                .ThenBy(x => x.Cells[ValueId].Value.TextValue)
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => wanted.Contains(x.Cells[Moment].Value.TextValue))
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[Moment].Value.TextValue != "")
                .GroupBy(x => x.Cells[Moment].Value.TextValue.Substring(11, 8))
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                db.Values,
                x => x.Cells[Moment].Value.TextValue,
                x => x.Cells[Moment].Value.TextValue,
                (left, right) => new ValueTuple<string, string>(
                    left.Cells[ValueId].Value.TextValue,
                    right.Cells[ValueId].Value.TextValue
                )
            )
        );
        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => x.Cells[Moment].Value.TextValue).Distinct()
        );
    }

    [Fact]
    public void ParsesTimeCellsAsTheyAreParsedInMemory()
    {
        TimeOnly late = new TimeOnly(23, 59, 59);

        Enumeration.AssertSameElements(db =>
            db.Extras.Select(x => new ValueTuple<string, TimeOnly?>(
                x.Cells[ExtraId].Value.TextValue,
                string.IsNullOrEmpty(x.Cells[Clock].Value.TextValue)
                    ? null
                    : TimeOnly.Parse(
                        x.Cells[Clock].Value.TextValue,
                        CultureInfo.InvariantCulture
                    )
            ))
        );
        Enumeration.AssertSameElements(db =>
            db.Extras.Where(x => x.Cells[Clock].Value.TextValue != "")
                .Where(x =>
                    TimeOnly.Parse(
                        x.Cells[Clock].Value.TextValue,
                        CultureInfo.InvariantCulture
                    ) >= late
                )
                .Select(x => x.Cells[ExtraId].Value.TextValue)
        );
    }

    [Fact]
    public void ParsesDateTimeCellsAsTheyAreParsedInMemory()
    {
        DateTime late = new DateTime(9999, 12, 31, 23, 59, 59);

        Enumeration.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, DateTime?>(
                x.Cells[ValueId].Value.TextValue,
                string.IsNullOrEmpty(x.Cells[Moment].Value.TextValue)
                    ? null
                    : DateTime.Parse(
                        x.Cells[Moment].Value.TextValue,
                        CultureInfo.InvariantCulture
                    )
            ))
        );
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[Moment].Value.TextValue != "")
                .Where(x =>
                    DateTime.Parse(
                        x.Cells[Moment].Value.TextValue,
                        CultureInfo.InvariantCulture
                    ) >= late
                )
                .Select(x => x.Cells[ValueId].Value.TextValue)
        );
    }

    private IQueryable<string> FilteredExtras(ExecutionTables db, string text)
    {
        return db
            .Extras.Where(x => x.Cells[Clock].Value.TextValue == text)
            .Select(x => x.Cells[ExtraId].Value.TextValue);
    }

    private IQueryable<string> FilteredValues(ExecutionTables db, string text)
    {
        return db
            .Values.Where(x => x.Cells[Moment].Value.TextValue == text)
            .Select(x => x.Cells[ValueId].Value.TextValue);
    }

    /// <summary>
    /// The ids a query returns over the stored tables, in the order of their text.
    /// </summary>
    private string[] Ids(Func<ExecutionTables, IQueryable<string>> query)
    {
        return
        [
            .. query(ExecutionTables.Stored(_fixture, _fixture.Connection))
                .AsEnumerable()
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string[] Translated(
        IQueryable<IRow> rows,
        IColumn id,
        IColumn column,
        string wanted
    )
    {
        return
        [
            .. rows.Where(x => x.Cells[id].Value.TextValue == wanted)
                .Select(x => x.Cells[column].Value.TextValue),
        ];
    }

    private static string Enumerated(
        IQueryable<IRow> rows,
        IColumn id,
        IColumn column,
        string wanted
    )
    {
        return rows.AsEnumerable()
            .Single(x => x.Cells[id].Value.TextValue == wanted)
            .Cells[column]
            .Value.TextValue;
    }
}
