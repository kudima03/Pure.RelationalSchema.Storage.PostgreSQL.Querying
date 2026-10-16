using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads an element of a translated query from the columns of its result, in the
/// order the translator projects them: the columns of a row's table, one column for
/// a scalar, and the members of an anonymous type or value tuple in turn.
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
    ];

    private readonly IResultReader? _reader;

    public ResultReader(Type type, ITable? table)
    {
        _reader = Reader(type, table);
    }

    public bool IsSupported => _reader is not null;

    public int Width => _reader!.Width;

    public object? Read(IDataRecord record, int offset)
    {
        return _reader!.Read(record, offset);
    }

    private static IResultReader? Reader(Type type, ITable? table)
    {
        if (type == typeof(IRow))
        {
            return table is null ? null : new RowReader(table);
        }
        if (Scalars.Contains(Nullable.GetUnderlyingType(type) ?? type))
        {
            return new ScalarReader(type);
        }
        if (!type.IsDefined(typeof(CompilerGeneratedAttribute)) && !IsValueTuple(type))
        {
            return null;
        }
        ConstructorInfo constructor = type.GetConstructors().Single();
        IResultReader?[] arguments =
        [
            .. constructor.GetParameters().Select(x => Reader(x.ParameterType, null)),
        ];
        return arguments.Any(x => x is null)
            ? null
            : new ConstructorReader(constructor, arguments!);
    }

    private static bool IsValueTuple(Type type)
    {
        return type.IsGenericType
            && type.Namespace == nameof(System)
            && type.Name.StartsWith(nameof(ValueTuple) + "`", StringComparison.Ordinal);
    }
}
