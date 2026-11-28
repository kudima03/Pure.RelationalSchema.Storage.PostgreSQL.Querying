using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The edge values of every scalar type an in-memory sequence beside stored tables
/// can hold, written as <c>VALUES</c>: each value comes back from the statement as
/// LINQ to Objects returns it, on the first row of the list (which carries the
/// type of its column) and on the later ones, NULL included; and a value the
/// translator cannot write is refused before any statement runs.
/// </summary>
public sealed record InMemoryScalarValuesTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public InMemoryScalarValuesTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Number => _fixture.Number;

    private IColumn Big => _fixture.Big;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact]
    public void ReadsStringsWithQuotesBackslashesAndDollarQuotes()
    {
        AssertSameItems([
            "it's",
            "''",
            "\\",
            "C:\\path\\to\\",
            "\\'",
            "$$",
            "$tag$ body $tag$",
            "-- comment",
            "/* comment */",
            "'; DROP TABLE x; --",
            "\"quoted\"",
            "%_[]^",
            "@p0 :name $1 ? ;",
            "\\x41",
            "E'not an escape'",
        ]);
    }

    [Fact]
    public void ReadsStringsWithBackslashesBesideControlCharacters()
    {
        AssertSameItems([
            "tab\there",
            "back\\slash\tand tab",
            "line\nfeed",
            "carriage\r\nreturn",
            "bell\a",
            "vertical\vtab",
            "form\ffeed",
            "back\bspace",
            "escape\u001b[0m",
            "delete\u007f",
            "quote'\tand tab",
        ]);
    }

    [Fact]
    public void ReadsStringsWithUnicodeSpacesAndInvisibleCharacters()
    {
        AssertSameItems([
            "no\u00a0break",
            "line\u2028separator",
            "paragraph\u2029separator",
            "next\u0085line",
            "zero\u200bwidth",
            "\ufeffbom",
            "ideographic\u3000space",
            "\u0080\u009f",
        ]);
    }

    [Fact]
    public void ReadsStringsOfEmojiAndOtherAstralCharacters()
    {
        AssertSameItems([
            "😀",
            "👨‍👩‍👧‍👦",
            "🇺🇦",
            "𝔘𝔫𝔦𝔠𝔬𝔡𝔢",
            "Ünicøde",
            "日本語",
            "עברית",
            "e\u0301",
        ]);
    }

    [Fact]
    public void ReadsEmptyAndBlankStrings()
    {
        AssertSameItems(["", " ", "  ", " padded ", "trailing "]);
    }

    [Fact]
    public void ReadsAVeryLongString()
    {
        string text = string.Concat(Enumerable.Repeat("ab'c\\d😀", 20000));

        AssertSameItems([text, "short", text + "!"]);
    }

    [Fact]
    public void ReadsStringsWithNulls()
    {
        AssertSameItems([null, "", "x", null, "it's"]);
    }

    [Fact]
    public void ReadsASequenceOfNullStringsOnly()
    {
        AssertSameItems<string?>([null, null, null]);
    }

    [Fact]
    public void RefusesAStringWithTheNulCharacter()
    {
        string[] texts = ["plain", "nul\0inside"];

        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => texts, (row, text) => text)
        );
    }

    [Fact]
    public void ReadsTheEdgesOfLongOnLaterRows()
    {
        AssertSameItems([
            0,
            long.MinValue,
            long.MaxValue,
            int.MinValue,
            (long)int.MinValue - 1,
            (long)int.MaxValue + 1,
            -1,
        ]);
    }

    [Fact]
    public void ReadsTheEdgesOfIntOnLaterRows()
    {
        AssertSameItems([0, int.MinValue, int.MaxValue, -1, 1]);
    }

    [Fact]
    public void ReadsTheEdgesOfShortOnLaterRows()
    {
        AssertSameItems<short>([0, short.MinValue, short.MaxValue, -1, 1]);
    }

    [Fact]
    public void ReadsNullableShortsWithNullOnTheFirstRow()
    {
        AssertSameItems<short?>([null, short.MinValue, 7, null]);
    }

    [Fact]
    public void ReadsNullableLongsOfNullsOnly()
    {
        AssertSameItems<long?>([null, null]);
    }

    [Fact]
    public void ReadsBooleansWithNulls()
    {
        AssertSameItems<bool?>([null, true, false, null, true]);
    }

    [Fact]
    public void ReadsGuidsWithNulls()
    {
        AssertSameItems<Guid?>([
            null,
            Guid.Empty,
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Guid.Parse("A0B1C2D3-E4F5-A6B7-C8D9-E0F1A2B3C4D5"),
        ]);
    }

    [Fact]
    public void ReadsTheEdgesOfDecimal()
    {
        AssertSameItems([
            0m,
            decimal.MaxValue,
            decimal.MinValue,
            0.0000000000000000000000000001m,
            -0.0000000000000000000000000001m,
            7.9228162514264337593543950335m,
            1.500m,
            100m,
            -1m,
        ]);
    }

    [Fact]
    public void ReadsNullableDecimalsWithNullOnTheFirstRow()
    {
        AssertSameItems<decimal?>([null, 1.25m, -3m, null]);
    }

    [Fact]
    public void ReadsOrdinaryDoubles()
    {
        AssertSameItems([
            0.1,
            1.0 / 3,
            -2.5,
            1e-300,
            1.2345678901234568E+20,
            double.MaxValue,
            double.MinValue,
            123456789,
        ]);
    }

    [Fact]
    public void ReadsTheSpecialDoubles()
    {
        AssertSameItems([
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            1,
        ]);
    }

    [Fact]
    public void ReadsTheNegativeZeroDouble()
    {
        AssertSameItems([1, -0.0, 0.0]);
    }

    [Fact]
    public void ReadsASubnormalDouble()
    {
        AssertSameItems([1, double.Epsilon, -double.Epsilon]);
    }

    [Fact]
    public void ReadsNullableDoublesWithNullOnTheFirstRow()
    {
        AssertSameItems<double?>([null, 0.5, double.NaN, null]);
    }

    [Fact]
    public void ReadsFloats()
    {
        AssertSameItems([
            0.1f,
            1f / 3,
            16777216f,
            float.MaxValue,
            float.MinValue,
            float.NaN,
            float.PositiveInfinity,
            float.NegativeInfinity,
            float.Epsilon,
        ]);
    }

    [Fact]
    public void ReadsDatetimesOfUnspecifiedKind()
    {
        AssertSameItems([
            new DateTime(2000, 1, 1),
            DateTime.MinValue,
            new DateTime(9999, 12, 31, 23, 59, 59).AddTicks(9999990),
            new DateTime(1970, 1, 1).AddTicks(10),
            new DateTime(2024, 2, 29, 12, 30, 45, 120),
            new DateTime(999, 2, 3, 4, 5, 6, 500),
        ]);
    }

    [Fact]
    public void ReadsNullableDatetimesWithNullOnTheFirstRow()
    {
        AssertSameItems<DateTime?>([null, new DateTime(2000, 1, 1, 0, 0, 1), null]);
    }

    [Fact]
    public void ReadsDatetimesOfUtcKind()
    {
        AssertSameItems([
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 2, 29, 23, 59, 59, DateTimeKind.Utc),
        ]);
    }

    [Fact]
    public void RefusesDatetimesOfLocalKind()
    {
        DateTime[] moments = [new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)];

        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => moments, (row, moment) => moment)
        );
    }

    [Fact]
    public void RefusesDatetimesFinerThanAMicrosecond()
    {
        DateTime[] moments = [new DateTime(2000, 1, 1), DateTime.MaxValue];

        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => moments, (row, moment) => moment)
        );
    }

    [Fact]
    public void ReadsTheEdgesOfDateOnly()
    {
        AssertSameItems([
            new DateOnly(2024, 2, 29),
            DateOnly.MinValue,
            DateOnly.MaxValue,
            new DateOnly(1970, 1, 1),
            new DateOnly(999, 12, 31),
        ]);
    }

    [Fact]
    public void ReadsTimesToTheMicrosecond()
    {
        AssertSameItems([
            new TimeOnly(3, 4, 5),
            TimeOnly.MinValue,
            TimeOnly.MaxValue.Add(TimeSpan.FromTicks(-9)),
            new TimeOnly(12, 0, 0, 100),
            TimeOnly.FromTimeSpan(TimeSpan.FromTicks(10)),
        ]);
    }

    [Fact]
    public void RefusesTimesFinerThanAMicrosecond()
    {
        TimeOnly[] times = [new TimeOnly(1, 0), TimeOnly.MaxValue];

        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => times, (row, time) => time)
        );
    }

    [Fact]
    public void ReadsTimeSpans()
    {
        AssertSameItems([
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1.5),
            new TimeSpan(1, 2, 3, 4, 5),
            TimeSpan.FromDays(400),
            TimeSpan.FromTicks(10),
            new TimeSpan(0, 23, 59, 59, 999).Add(TimeSpan.FromTicks(9990)),
        ]);
    }

    [Fact]
    public void ReadsNegativeTimeSpans()
    {
        AssertSameItems([
            TimeSpan.FromHours(1),
            TimeSpan.FromSeconds(-1),
            TimeSpan.FromSeconds(-1.5),
            TimeSpan.FromMinutes(-90),
            new TimeSpan(-1, -2, -3, -4, -5),
            TimeSpan.FromTicks(-10),
        ]);
    }

    [Fact]
    public void RefusesTimeSpansFinerThanAMicrosecond()
    {
        TimeSpan[] spans = [TimeSpan.Zero, TimeSpan.FromTicks(1)];

        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => spans, (row, span) => span)
        );
    }

    [Fact]
    public void ReadsNullableTimeSpansWithNullOnTheFirstRow()
    {
        AssertSameItems<TimeSpan?>([null, TimeSpan.FromMinutes(5), null]);
    }

    [Fact]
    public void ReadsASequenceOfOneElement()
    {
        AssertSameItems<long>([42]);
    }

    [Fact]
    public void ReadsASequenceOfOneNull()
    {
        AssertSameItems<Guid?>([null]);
    }

    [Fact]
    public void ReadsDuplicatesOfASequence()
    {
        AssertSameItems(["a", "a", null, null, "a", ""]);
    }

    [Fact]
    public void ReadsBytes()
    {
        AssertSameItems<byte>([0, 255, 7]);
    }

    [Fact]
    public void ReadsSignedBytes()
    {
        AssertSameItems<sbyte>([0, sbyte.MinValue, sbyte.MaxValue]);
    }

    [Fact]
    public void ReadsInMemoryCharacters()
    {
        char[] letters = ['p', 'x', '\''];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => letters, (row, letter) => letter)
        );
    }

    [Fact]
    public void JoinsAnIntegerColumnToInMemoryEnumMembers()
    {
        DayOfWeek[] days = [DayOfWeek.Sunday, DayOfWeek.Saturday, DayOfWeek.Monday];

        Enumeration.AssertSameElements(db =>
            db.Values.Join(
                days,
                x =>
                    string.IsNullOrEmpty(x.Cells[Number].Value.TextValue)
                        ? null
                        : int.Parse(x.Cells[Number].Value.TextValue),
                x => (int?)(int)x,
                (row, day) =>
                    new ValueTuple<string, int>(
                        row.Cells[ValueId].Value.TextValue,
                        (int)day
                    )
            )
        );
    }

    [Fact]
    public void ComparesStoredLongsWithInMemoryEdgesOfLong()
    {
        long[] bounds = [long.MinValue, -1, long.MaxValue];

        Enumeration.AssertSameElements(db =>
            db.Values.SelectMany(
                    x => bounds,
                    (row, bound) => new { Row = row, Bound = bound }
                )
                .Where(x =>
                    !string.IsNullOrEmpty(x.Row.Cells[Big].Value.TextValue)
                    && long.Parse(x.Row.Cells[Big].Value.TextValue) >= x.Bound
                )
                .Select(x => new ValueTuple<string, long>(
                    x.Row.Cells[ValueId].Value.TextValue,
                    x.Bound
                ))
        );
    }

    [Fact]
    public void JoinsTheSameDoublesOfASequenceToThemselves()
    {
        double[] numbers = [double.NaN, 0.1, double.PositiveInfinity, 1e-300];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => numbers, (row, number) => number)
                .Join(numbers.AsQueryable(), x => x, x => x, (x, y) => y)
        );
    }

    [Fact]
    public void ComputesWithTheSpecialInMemoryDoubles()
    {
        double[] numbers =
        [
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            2,
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(
                    x => numbers,
                    (row, number) =>
                        new ValueTuple<double, double, double>(
                            number,
                            number * 2,
                            number - number
                        )
                )
        );
    }

    [Fact]
    public void ComparesTheSpecialInMemoryDoublesWithZero()
    {
        double[] numbers =
        [
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            2,
        ];

        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(
                    x => numbers,
                    (row, number) =>
                        new ValueTuple<double, bool, bool, bool>(
                            number,
                            number > 0,
                            number < 0,
                            number == number * 1
                        )
                )
        );
    }

    // PostgreSQL holds a time to the microsecond, so VALUES cannot hold the tick
    // of the last time.
    [Fact]
    public void RefusesInMemoryTimesFinerThanAMicrosecond()
    {
        TimeOnly[] times =
        [
            TimeOnly.MaxValue,
            new TimeOnly(3, 4, 5),
            new TimeOnly(3, 4, 5).Add(TimeSpan.FromTicks(1)),
        ];

        Enumeration.AssertNotSupported(db =>
            db.Extras.Join(
                times,
                x =>
                    string.IsNullOrEmpty(x.Cells[_fixture.Clock].Value.TextValue)
                        ? null
                        : TimeOnly.Parse(
                            x.Cells[_fixture.Clock].Value.TextValue,
                            CultureInfo.InvariantCulture
                        ),
                x => (TimeOnly?)x,
                (row, time) => row.Cells[_fixture.ExtraId].Value.TextValue
            )
        );
    }

    private void AssertSameItems<T>(T[] items)
    {
        Enumeration.AssertSameElements(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => items, (row, item) => item)
        );
    }
}
