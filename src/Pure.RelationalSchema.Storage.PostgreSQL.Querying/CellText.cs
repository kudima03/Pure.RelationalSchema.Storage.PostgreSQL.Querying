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

    /// <summary>
    /// The invariant text of a value read from a cell: the UTF-8 text of bytes, a
    /// time as <c>HH:mm:ss</c> and a date and time as <c>MM/dd/yyyy HH:mm:ss</c>,
    /// each with the fraction of its second when it has one (<c>.FFFFFFF</c> drops
    /// trailing zeros and, for a whole second, the point), so that the text parses
    /// back to the value stored; anything else as <see cref="Convert"/> writes it.
    /// </summary>
    public string TextValue =>
        _value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            DateTime moment => moment.ToString(
                "MM/dd/yyyy HH:mm:ss.FFFFFFF",
                CultureInfo.InvariantCulture
            ),
            _ => Convert.ToString(_value, CultureInfo.InvariantCulture)!,
        };
}
