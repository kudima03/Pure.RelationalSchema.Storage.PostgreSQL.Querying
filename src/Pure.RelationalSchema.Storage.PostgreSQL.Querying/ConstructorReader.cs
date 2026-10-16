using System.Data;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record ConstructorReader : IResultReader
{
    private readonly ConstructorInfo _constructor;

    private readonly IReadOnlyList<IResultReader> _arguments;

    public ConstructorReader(
        ConstructorInfo constructor,
        IReadOnlyList<IResultReader> arguments
    )
    {
        _constructor = constructor;
        _arguments = arguments;
    }

    public int Width => _arguments.Sum(x => x.Width);

    public object? Read(IDataRecord record, int offset)
    {
        object?[] values = new object?[_arguments.Count];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = _arguments[index].Read(record, offset);
            offset += _arguments[index].Width;
        }
        return _constructor.Invoke(values);
    }
}
