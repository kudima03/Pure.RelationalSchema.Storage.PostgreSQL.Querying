using System.Data;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads an object built with an initializer: the constructed object, then the
/// value of each member the initializer assigns, a property (<c>Id = ...</c>) or a
/// field (<c>Item2 = ...</c> of a value tuple), in the order of the assignments.
/// </summary>
internal sealed record InitializerReader : IResultReader
{
    private readonly IResultReader _constructed;

    private readonly IReadOnlyList<(MemberInfo Member, IResultReader Reader)> _members;

    public InitializerReader(
        IResultReader constructed,
        IReadOnlyList<(MemberInfo Member, IResultReader Reader)> members
    )
    {
        _constructed = constructed;
        _members = members;
    }

    public int Width => _constructed.Width + _members.Sum(x => x.Reader.Width);

    public object? Read(IDataRecord record, int offset)
    {
        object instance = _constructed.Read(record, offset)!;
        offset += _constructed.Width;
        foreach ((MemberInfo member, IResultReader reader) in _members)
        {
            object? value = reader.Read(record, offset);
            offset += reader.Width;
            if (member is PropertyInfo property)
            {
                property.SetValue(instance, value);
            }
            else
            {
                ((FieldInfo)member).SetValue(instance, value);
            }
        }
        return instance;
    }
}
