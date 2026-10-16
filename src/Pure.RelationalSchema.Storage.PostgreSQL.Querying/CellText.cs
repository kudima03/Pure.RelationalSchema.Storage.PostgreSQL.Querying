using System.Globalization;
using System.Text;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record CellText
{
    private readonly object _value;

    public CellText(object value)
    {
        _value = value;
    }

    public string TextValue =>
        _value is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : Convert.ToString(_value, CultureInfo.InvariantCulture)!;
}
