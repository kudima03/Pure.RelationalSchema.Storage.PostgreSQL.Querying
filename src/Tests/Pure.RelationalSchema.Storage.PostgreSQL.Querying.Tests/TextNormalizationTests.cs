using System.Text;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;
using Xunit.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// <c>string.Normalize</c> of texts read from stored cells and joined to in-memory
/// texts, <c>NORMALIZE</c> in SQL, returns in one statement what it returns over the
/// enumerated rows in each of the four forms: for composed and decomposed letters,
/// combining marks out of canonical order, Hangul syllables and jamo, compatibility
/// characters (ligatures, full-width and half-width forms, circled digits,
/// superscripts), singletons and characters above U+FFFF; also in a filter, a
/// grouping key and an ordering. A form that is not a constant, or no form of
/// Unicode, is refused, and so is <c>IsNormalized</c>.
/// </summary>
public sealed record TextNormalizationTests : IClassFixture<ExecutionFixture>
{
    private static readonly string[] Samples =
    [
        "",
        "plain",
        "é",
        "é",
        "Å",
        "Å",
        "Å",
        "ą́",
        "ą́",
        "ẛ̣",
        "ḍ̇",
        "한글",
        "한",
        "ﬁ",
        "ＡＢ",
        "ｶﾞ",
        "①",
        "x²",
        "Ω",
        "Ǆ",
        "½",
        "™",
        "ﷺ",
        "ΐ",
        "ᾀ",
        "\U0001D400",
        "\U0001F600́",
        "क़",
        "⫝̸",
        "'quoted'",
    ];

    // The samples without characters above U+FFFF, which the "C" collation orders by
    // code point and StringComparer.Ordinal by UTF-16 code unit.
    private static readonly string[] BasicSamples =
    [
        .. Samples.Where(x => !x.Any(char.IsSurrogate)),
    ];

    private readonly ExecutionFixture _fixture;

    private readonly ITestOutputHelper _output;

    public TextNormalizationTests(ExecutionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private IColumn ValueId => _fixture.ValueId;

    private IColumn Text => _fixture.Text;

    private IColumn Flag => _fixture.Flag;

    private BuiltValueComparison Comparison => new BuiltValueComparison(_fixture, _output);

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Theory]
    [InlineData(NormalizationForm.FormC)]
    [InlineData(NormalizationForm.FormD)]
    [InlineData(NormalizationForm.FormKC)]
    [InlineData(NormalizationForm.FormKD)]
    public void NormalizesInMemoryTexts(NormalizationForm form)
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .SelectMany(
                    x => Samples,
                    (row, sample) => new ValueTuple<string, string, bool>(
                        sample,
                        sample.Normalize(form),
                        sample.Normalize(form) == sample
                    )
                )
        );
    }

    [Theory]
    [InlineData(NormalizationForm.FormC)]
    [InlineData(NormalizationForm.FormD)]
    [InlineData(NormalizationForm.FormKC)]
    [InlineData(NormalizationForm.FormKD)]
    public void NormalizesTheTextsOfCellsJoinedToCombiningMarks(NormalizationForm form)
    {
        Comparison.AssertSameElements(db =>
            db.Values.Select(x => new ValueTuple<string, string, string>(
                x.Cells[ValueId].Value.TextValue,
                (x.Cells[Text].Value.TextValue + "̣́").Normalize(form),
                ("ᄀ" + x.Cells[Text].Value.TextValue + "ᅡ").Normalize(form)
            ))
        );
    }

    [Fact]
    public void NormalizesInFormCWithoutAForm()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .SelectMany(
                    x => Samples,
                    (row, sample) => new ValueTuple<string, string>(sample, sample.Normalize())
                )
        );
    }

    [Fact]
    public void FiltersByNormalizedTexts()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .SelectMany(x => Samples, (row, sample) => sample)
                .Where(x =>
                    x.Normalize(NormalizationForm.FormC) != x.Normalize(NormalizationForm.FormD)
                )
        );
    }

    [Fact]
    public void GroupsTextsByTheirNormalizedForms()
    {
        Comparison.AssertSameElements(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .SelectMany(x => Samples, (row, sample) => sample)
                .GroupBy(x => x.Normalize(NormalizationForm.FormKC))
                .Select(x => new ValueTuple<string, int>(x.Key, x.Count()))
        );
    }

    [Fact]
    public void OrdersTextsByTheirNormalizedForms()
    {
        Enumeration.AssertSameOrder(db =>
            db.Values.Where(x => long.Parse(x.Cells[ValueId].Value.TextValue) == 1)
                .SelectMany(x => BasicSamples, (row, sample) => sample)
                .Select(x => x.Normalize(NormalizationForm.FormD))
                .Distinct()
                .OrderBy(x => x, StringComparer.Ordinal)
        );
    }

    [Fact]
    public void RefusesAFormThatIsNotAConstant()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Select(x =>
                x.Cells[Text].Value.TextValue.Normalize(
                    x.Cells[Flag].Value.TextValue == "True"
                        ? NormalizationForm.FormC
                        : NormalizationForm.FormD
                )
            )
        );
    }

    [Fact]
    public void RefusesAFormThatIsNotOneOfUnicode()
    {
        IQueryable<IRow> values = new PostgreSqlStoredSchemaDataSet(
            _fixture.Store,
            _fixture.Connection
        )[_fixture.Values];

        _ = Assert.Throws<NotSupportedException>(() =>
            values
                .Select(x => x.Cells[Text].Value.TextValue.Normalize((NormalizationForm)3))
                .ToList()
        );
    }

    [Fact]
    public void RefusesTheTestOfANormalizedText()
    {
        Enumeration.AssertNotSupported(db =>
            db.Values.Select(x =>
                x.Cells[Text].Value.TextValue.IsNormalized(NormalizationForm.FormD)
            )
        );
    }

    [Theory]
    [InlineData(NormalizationForm.FormC)]
    [InlineData(NormalizationForm.FormD)]
    [InlineData(NormalizationForm.FormKD)]
    public void NormalizesTheTextsOfDeterminedHashCells(NormalizationForm form)
    {
        Comparison.AssertSameElements(db =>
            db.Extras.Select(x => new ValueTuple<string, string>(
                x.Cells[_fixture.ExtraId].Value.TextValue,
                (x.Cells[_fixture.Digest].Value.TextValue + "\u0301").Normalize(form)
            ))
        );
    }
}
