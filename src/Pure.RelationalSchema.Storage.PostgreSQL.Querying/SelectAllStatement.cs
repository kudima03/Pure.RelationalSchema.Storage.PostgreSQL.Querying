using System.Collections;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Choices.String;
using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Char = Pure.Primitives.Char.Char;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record SelectAllStatement : IString
{
    private readonly ITable _table;

    private readonly IString _schemaName;

    public SelectAllStatement(ITable table)
        : this(table, new EmptyString()) { }

    public SelectAllStatement(ITable table, IString schemaName)
    {
        _table = table;
        _schemaName = schemaName;
    }

    public string TextValue =>
        new WhitespaceJoinedString(
            new String("SELECT"),
            new JoinedString(
                new ConcatenatedString(new CommaString(), new WhitespaceString()),
                _table.Columns.Select(x => new WrappedString(
                    new DoubleQuoteString(),
                    new HexString(new ColumnHash(x))
                ))
            ),
            new String("FROM"),
            new ConcatenatedString(
                new StringChoice(
                    new EqualCondition(_schemaName, new EmptyString()),
                    new WrappedString(
                        new DoubleQuoteString(),
                        new HexString(new TableHash(_table))
                    ),
                    new JoinedString(
                        new DotString(),
                        [
                            new WrappedString(new DoubleQuoteString(), _schemaName),
                            new WrappedString(
                                new DoubleQuoteString(),
                                new HexString(new TableHash(_table))
                            ),
                        ]
                    )
                ),
                new SemicolonString()
            )
        ).TextValue;

    public IEnumerator<IChar> GetEnumerator()
    {
        return TextValue.Select(x => new Char(x)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
