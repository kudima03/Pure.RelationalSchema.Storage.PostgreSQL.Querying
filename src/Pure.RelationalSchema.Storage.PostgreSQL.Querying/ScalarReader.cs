using System.Data;
using System.Globalization;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads one value of a scalar type: text from the bytes of a <c>bytea</c> column,
/// and any other value converted to the type (a value of the type stays as it is).
/// </summary>
internal sealed record ScalarReader : IResultReader
{
    private readonly Type _type;

    public ScalarReader(Type type)
    {
        _type = Nullable.GetUnderlyingType(type) ?? type;
    }

    public int Width => 1;

    public object? Read(IDataRecord record, int offset)
    {
        object value = record.GetValue(offset);
        return value is DBNull ? null
            : _type == typeof(string) ? new CellText(value).TextValue
            : Convert.ChangeType(value, _type, CultureInfo.InvariantCulture);
    }
}
