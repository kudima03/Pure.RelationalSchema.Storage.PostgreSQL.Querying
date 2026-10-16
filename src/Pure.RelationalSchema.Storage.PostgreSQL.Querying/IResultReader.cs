using System.Data;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal interface IResultReader
{
    public int Width { get; }

    public object? Read(IDataRecord record, int offset);
}
