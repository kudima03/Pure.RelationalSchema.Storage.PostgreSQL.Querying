using System.Globalization;
using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Storage.Abstractions;
using Xunit.Abstractions;

// The tests of semantics the translation does not keep yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A sample of <see cref="NaNSampleFixture"/>: its id, its bucket and its stored
/// double.
/// </summary>
internal sealed record NaNSample(long Id, long Bucket, double Measure);

/// <summary>
/// The shapes of the translator's defect of <c>double</c> NaN (translator issue #128)
/// over stored cells that hold NaN and both infinities: <c>Max</c> and <c>Min</c>
/// of a table and of groups, a filter comparing with NaN, and the extremes and the
/// filters of a frame of <c>PostgreSqlWindows.Over</c>. PostgreSQL orders NaN above
/// every number and equal to itself, .NET below every number and unordered; each
/// query returns in one statement what it returns over the enumerated rows (for a
/// window, what the window computed in memory over them returns).
/// </summary>
public sealed record TranslatorDefectNaNTests : IClassFixture<NaNSampleFixture>
{
    private const string Thrown = "throws ";

    private readonly NaNSampleFixture _fixture;

    private readonly ITestOutputHelper _output;

    public TranslatorDefectNaNTests(NaNSampleFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void ReadsStoredNaNAndInfinitiesAsTextAndAsDoubles()
    {
        NaNSampleFixture f = _fixture;
        AssertSameElements(
            samples =>
                samples.Select(x => new ValueTuple<string, string, double>(
                    x.Cells[f.SampleId].Value.TextValue,
                    x.Cells[f.Measure].Value.TextValue,
                    double.Parse(
                        x.Cells[f.Measure].Value.TextValue,
                        CultureInfo.InvariantCulture
                    )
                )),
            samples =>
                samples.Select(x => new ValueTuple<string, string, double>(
                    x.Cells[f.SampleId].Value.TextValue,
                    x.Cells[f.Measure].Value.TextValue,
                    double.Parse(
                        x.Cells[f.Measure].Value.TextValue,
                        CultureInfo.InvariantCulture
                    )
                ))
        );
    }

    [Fact(
        Skip = "#111 (translator #128): MAX of doubles is NaN where one is NaN, as "
            + "PostgreSQL orders NaN above every number, where .NET's Max skips NaN "
            + "(also translator #132)."
    )]
    public void TakesTheMaximumOfStoredDoublesWithNaN()
    {
        AssertSameValue(samples => Measures(samples).Max());
    }

    [Fact(
        Skip = "#111 (translator #128): MIN of doubles skips NaN, as PostgreSQL orders "
            + "NaN above every number, where .NET's Min is NaN where one is NaN (also "
            + "translator #132)."
    )]
    public void TakesTheMinimumOfStoredDoublesWithNaN()
    {
        AssertSameValue(samples => Measures(samples).Min());
    }

    [Fact(
        Skip = "#111 (translator #128): MAX and MIN of the doubles of a group order NaN "
            + "as PostgreSQL does, above every number (also translator #132)."
    )]
    public void TakesTheExtremesOfGroupsOfStoredDoublesWithNaN()
    {
        AssertSameElements(samples =>
            Typed(samples)
                .GroupBy(x => x.Bucket)
                .Select(x => new ValueTuple<long, double, double>(
                    x.Key,
                    x.Max(sample => sample.Measure),
                    x.Min(sample => sample.Measure)
                ))
        );
    }

    [Fact(
        Skip = "#111 (translator #128): NaN > 2 and NaN = NaN are true in PostgreSQL, "
            + "false in .NET, so the filter keeps the rows of NaN and the comparisons of "
            + "NaN are true (also translator #132)."
    )]
    public void FiltersAndComparesStoredDoublesWithNaN()
    {
        AssertSameElements(samples =>
            Typed(samples)
                .Where(x => x.Measure > 2 || x.Bucket == 2)
                .Select(x => new ValueTuple<long, bool, bool>(
                    x.Id,
                    x.Measure > 2,
                    x.Measure >= x.Measure * 1
                ))
        );
    }

    [Fact(
        Skip = "#111 (translator #128): MAX and MIN over a frame order NaN as PostgreSQL "
            + "does, so the maximum of a frame holding NaN is NaN and its minimum skips "
            + "it (also translator #132)."
    )]
    public void TakesTheExtremesOfFramesOfStoredDoublesWithNaN()
    {
        AssertWindow(
            null,
            rows => rows.OrderBy(x => x.Id),
            PostgreSqlFrame.Rows(-1, 1),
            (x, w) =>
                new ValueTuple<long, double, double>(
                    x.Id,
                    w.Rows.Max(row => row.Measure),
                    w.Rows.Min(row => row.Measure)
                ),
            (x, w) =>
                new ValueTuple<long, double, double>(
                    x.Id,
                    w.Rows(-1, 1).Max(row => row.Measure),
                    w.Rows(-1, 1).Min(row => row.Measure)
                )
        );
    }

    [Fact(
        Skip = "#111 (translator #128): a frame filter comparing a double counts NaN as "
            + "greater than every number, where .NET's comparison with NaN is false "
            + "(also translator #132)."
    )]
    public void FiltersTheRowsOfFramesByStoredDoublesWithNaN()
    {
        AssertWindow(
            x => x.Bucket,
            rows => rows.OrderBy(x => x.Id),
            PostgreSqlFrame.Rows(-1, 1),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Rows.Count(row => row.Measure > 2),
                    w.Rows.Where(row => row.Measure <= 2).Sum(row => row.Id)
                ),
            (x, w) =>
                new ValueTuple<long, int, long>(
                    x.Id,
                    w.Rows(-1, 1).Count(row => row.Measure > 2),
                    w.Rows(-1, 1).Where(row => row.Measure <= 2).Sum(row => row.Id)
                )
        );
    }

    private IQueryable<NaNSample> Typed(IQueryable<IRow> samples)
    {
        NaNSampleFixture f = _fixture;
        return samples.Select(x => new NaNSample(
            long.Parse(x.Cells[f.SampleId].Value.TextValue),
            long.Parse(x.Cells[f.Bucket].Value.TextValue),
            double.Parse(x.Cells[f.Measure].Value.TextValue, CultureInfo.InvariantCulture)
        ));
    }

    private IQueryable<double> Measures(IQueryable<IRow> samples)
    {
        return Typed(samples).Select(x => x.Measure);
    }

    /// <summary>
    /// The window over the samples, translated, returns for each sample what the
    /// window computed in memory over the enumerated samples returns.
    /// </summary>
    private void AssertWindow<TResult>(
        Expression<Func<NaNSample, object?>>? partitionBy,
        Expression<Func<IEnumerable<NaNSample>, IOrderedEnumerable<NaNSample>>> orderBy,
        PostgreSqlFrame frame,
        Expression<Func<NaNSample, PostgreSqlWindow<NaNSample>, TResult>> selector,
        Func<NaNSample, InMemoryWindow<NaNSample>, TResult> expected
    )
    {
        Func<NaNSample, object?> partition = partitionBy?.Compile() ?? (_ => null);
        Func<IEnumerable<NaNSample>, IOrderedEnumerable<NaNSample>> order =
            orderBy.Compile();
        AssertSameElements(
            samples =>
                MarkerCalls.Over(Typed(samples), partitionBy, orderBy, frame, selector),
            samples =>
                InMemoryWindow<NaNSample>
                    .Over(Typed(samples), partition, order, x => x.Id)
                    .Select(x => expected(x.Row, x.Window))
        );
    }

    private void AssertSameValue<T>(Func<IQueryable<IRow>, T> query)
    {
        AssertSameOutcome(
            samples => [BuiltValueComparison.Canonical(query(samples))],
            samples => [BuiltValueComparison.Canonical(query(samples))]
        );
    }

    private void AssertSameElements<T>(Func<IQueryable<IRow>, IQueryable<T>> query)
    {
        AssertSameElements(query, query);
    }

    private void AssertSameElements<T>(
        Func<IQueryable<IRow>, IQueryable<T>> stored,
        Func<IQueryable<IRow>, IEnumerable<T>> enumerated
    )
    {
        AssertSameOutcome(
            samples =>
                stored(samples)
                    .AsEnumerable()
                    .Select(BuiltValueComparison.Canonical)
                    .Order(StringComparer.Ordinal),
            samples =>
                enumerated(samples)
                    .Select(BuiltValueComparison.Canonical)
                    .Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// The query over the stored samples returns, in one statement, what the query
    /// over the samples enumerated into memory returns.
    /// </summary>
    private void AssertSameOutcome(
        Func<IQueryable<IRow>, IEnumerable<string>> stored,
        Func<IQueryable<IRow>, IEnumerable<string>> enumerated
    )
    {
        IRow[] rows =
        [
            .. new PostgreSqlStoredSchemaDataSet(_fixture.Sensors, _fixture.Connection)[
                _fixture.Samples
            ]
                .AsEnumerable(),
        ];
        string[] expected = Outcome(() => enumerated(new EnumerableQuery<IRow>(rows)));

        using RecordingConnection connection = new RecordingConnection(
            _fixture.Connection
        );

        string[] actual = Outcome(() =>
            stored(
                new PostgreSqlStoredSchemaDataSet(_fixture.Sensors, connection)[
                    _fixture.Samples
                ]
            )
        );

        foreach (string statement in connection.Statements)
        {
            _output.WriteLine(statement);
        }
        Assert.Equal(expected, actual);
        _ = Assert.Single(connection.Statements);
    }

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
}
