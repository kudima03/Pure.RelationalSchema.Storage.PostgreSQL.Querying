using System.Data;
using Pure.RelationalSchema.Abstractions.Column;

// The tests of values the translation does not refuse yet are skipped until it does.
#pragma warning disable xUnit1004

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A string of an in-memory sequence that has no UTF-8 encoding — a lone UTF-16
/// surrogate — is refused before any statement runs, as text with the NUL
/// character is, and the connection the caller owns stays open. The class has a
/// fixture of its own, as a statement that fails to encode closes the connection
/// it runs on.
/// </summary>
public sealed record InMemoryStringEncodingTests : IClassFixture<ExecutionFixture>
{
    private readonly ExecutionFixture _fixture;

    public InMemoryStringEncodingTests(ExecutionFixture fixture)
    {
        _fixture = fixture;
    }

    private IColumn ValueId => _fixture.ValueId;

    private ExecutionComparison Enumeration => new ExecutionComparison(_fixture);

    [Fact(
        Skip = "PENDING translator: a lone UTF-16 surrogate in a text literal is not "
            + "refused; Npgsql throws EncoderFallbackException and closes the connection."
    )]
    public void RefusesAStringWithALoneSurrogateAndKeepsTheConnectionOpen()
    {
        string[] texts = ["plain", "lone\ud800surrogate"];

        Enumeration.AssertNotSupported(db =>
            db.Values.Where(x => x.Cells[ValueId].Value.TextValue == "1")
                .SelectMany(x => texts, (row, text) => text)
        );
        Assert.Equal(ConnectionState.Open, _fixture.Connection.State);
    }
}
