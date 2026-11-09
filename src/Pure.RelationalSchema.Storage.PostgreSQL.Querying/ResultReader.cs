using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads an element of a translated query from the columns of its result, in the
/// order the translator projects them: the columns of a row's table, one column for
/// a scalar, and the arguments of a constructed object (an anonymous type, a value
/// tuple, ...) in turn, followed by the members its initializer assigns. The element
/// expression (see <see cref="QueryElement"/>) names the table of each row and the
/// constructor of each object.
/// </summary>
internal sealed record ResultReader : IResultReader
{
    private static readonly HashSet<Type> Scalars =
    [
        typeof(bool),
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
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
            return QueryElement.RowOf(element) is ElementRow row
                ? new RowReader(row.Table, row.IsOptional)
                : null;
        }
        if (Scalars.Contains(Nullable.GetUnderlyingType(type) ?? type))
        {
            return new ScalarReader(type);
        }
        Expression? resolved = QueryElement.Resolved(element);
        NewExpression? creation = resolved switch
        {
            NewExpression construction => construction,
            MemberInitExpression initialized => initialized.NewExpression,
            _ => null,
        };
        if (creation is not { Constructor: ConstructorInfo constructor })
        {
            return null;
        }
        MemberInitExpression? initialization = resolved as MemberInitExpression;
        ParameterInfo[] parameters = constructor.GetParameters();
        IResultReader?[] arguments =
        [
            .. creation.Arguments.Select(
                (argument, index) =>
                    initialization is not null
                    && QueryElement.IsReplaced(parameters[index], initialization)
                        ? new DefaultReader(parameters[index].ParameterType)
                        : Reader(parameters[index].ParameterType, argument)
            ),
        ];
        if (arguments.Any(x => x is null))
        {
            return null;
        }
        ConstructorReader constructed = new(constructor, arguments!);
        return initialization is null
            ? constructed
            : Initialized(constructed, initialization.Bindings);
    }

    /// <summary>
    /// The reader of an object whose initializer assigns members, properties or
    /// fields, after its constructor, each read in turn after the arguments the
    /// initializer does not replace; null when a binding is not an assignment or
    /// assigns a value that cannot be read.
    /// </summary>
    private static IResultReader? Initialized(
        IResultReader constructed,
        IReadOnlyList<MemberBinding> bindings
    )
    {
        List<(MemberInfo Member, IResultReader Reader)> members = [];
        foreach (MemberBinding binding in bindings)
        {
            if (
                binding is not MemberAssignment assignment
                || Reader(assignment.Expression.Type, assignment.Expression)
                    is not IResultReader reader
            )
            {
                return null;
            }
            members.Add((assignment.Member, reader));
        }
        return new InitializerReader(constructed, members);
    }
}
