using System.Data;
using System.Globalization;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads one value of a scalar type: text from the bytes of a <c>bytea</c> column,
/// and any other value converted to the type (a value of the type stays as it is).
/// NULL is null for a reference or nullable type, where a value type has no value
/// to give for it.
/// </summary>
internal sealed record ScalarReader : IResultReader
{
    private readonly Type _type;

    private readonly bool _nullable;

    public ScalarReader(Type type)
    {
        _type = Nullable.GetUnderlyingType(type) ?? type;
        _nullable = !type.IsValueType || _type != type;
    }

    public int Width => 1;

    public object? Read(IDataRecord record, int offset)
    {
        object value = record.GetValue(offset);
        return value is DBNull ? Null()
            : _type == typeof(string) ? new CellText(value).TextValue
            : Convert.ChangeType(value, _type, CultureInfo.InvariantCulture);
    }

    /// <exception cref="InvalidOperationException">
    /// The type is a value type, which has no value for NULL: the exception of an
    /// aggregate of no elements, which a NULL cell parsed bare raises too.
    /// </exception>
    private object? Null()
    {
        return _nullable
            ? null
            : throw new InvalidOperationException("Sequence contains no elements");
    }
}
