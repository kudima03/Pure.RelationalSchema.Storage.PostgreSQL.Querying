using System.Data;
using System.Globalization;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record ScalarReader : IResultReader
{
    private readonly Type _type;

    public ScalarReader(Type type)
    {
        _type = type;
    }

    public int Width => 1;

    public object? Read(IDataRecord record, int offset)
    {
        object value = record.GetValue(offset);
        return value is DBNull
            ? null
            : Convert.ChangeType(
                value,
                Nullable.GetUnderlyingType(_type) ?? _type,
                CultureInfo.InvariantCulture
            );
    }
}
