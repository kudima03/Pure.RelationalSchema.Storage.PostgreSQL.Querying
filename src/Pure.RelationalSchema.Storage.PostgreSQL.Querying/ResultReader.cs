using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads an element of a translated query from the columns of its result, in the
/// order the translator projects them: the columns of a row's table, one column for
/// a scalar, and the arguments of a constructed object (an anonymous type, a value
/// tuple, ...) in turn. The element expression (see <see cref="QueryElement"/>)
/// names the table of each row and the constructor of each object.
/// </summary>
internal sealed record ResultReader : IResultReader
{
    private static readonly HashSet<Type> Scalars =
    [
        typeof(bool),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(string),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(TimeSpan),
    ];

    private readonly IResultReader? _reader;

    public ResultReader(Type type, Expression? element)
    {
        _reader = Reader(type, element);
    }

    public bool IsSupported => _reader is not null;

    public int Width => _reader!.Width;

    public object? Read(IDataRecord record, int offset)
    {
        return _reader!.Read(record, offset);
    }

    private static IResultReader? Reader(Type type, Expression? element)
    {
        if (type == typeof(IRow))
        {
            return QueryElement.TableOf(element) is ITable table
                ? new RowReader(table)
                : null;
        }
        if (Scalars.Contains(Nullable.GetUnderlyingType(type) ?? type))
        {
            return new ScalarReader(type);
        }
        if (
            Construction(element)
            is not { Constructor: ConstructorInfo constructor } creation
        )
        {
            return null;
        }
        ParameterInfo[] parameters = constructor.GetParameters();
        IResultReader?[] readers =
        [
            .. creation.Arguments.Select(
                (argument, index) => Reader(parameters[index].ParameterType, argument)
            ),
        ];
        return readers.Any(x => x is null)
            ? null
            : new ConstructorReader(constructor, readers!);
    }

    /// <summary>
    /// The constructor call the element is, when it is one.
    /// </summary>
    private static NewExpression? Construction(Expression? element)
    {
        return QueryElement.Resolved(element) switch
        {
            NewExpression creation => creation,
            MemberInitExpression initialization => initialization.NewExpression,
            _ => null,
        };
    }
}
