using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// A type built for a stored table, so that the translator, which maps a table from
/// the element type of a query root, names the table and its columns by their
/// hashes. It is built from reflection objects rather than emitted, so that it needs
/// no dynamic code: it delegates to <see cref="ElementRow"/>, so that it is a row,
/// and its properties (see <see cref="EntityProperty"/>) are only read by the
/// translator, never invoked.
/// </summary>
internal sealed class EntityType : TypeDelegator
{
    private static readonly Dictionary<(string Schema, string Table), EntityType> Types =
    [];

    private readonly TableAttribute _table;

    private readonly IReadOnlyList<EntityProperty> _properties;

    private EntityType(ITable table, string schema)
        : base(typeof(ElementRow))
    {
        HexString hash = new(new TableHash(table));
        Name = hash.TextValue;
        _table = new TableAttribute(new TrimmedHash(hash).TextValue);
        if (schema.Length != 0)
        {
            _table.Schema = schema;
        }
        _properties =
        [
            .. table.Columns.Select(x => new EntityProperty(this, new ColumnProperty(x))),
        ];
    }

    public override string Name { get; }

    /// <summary>
    /// The type of a table of a schema, built once.
    /// </summary>
    public static EntityType Of(ITable table, string schema)
    {
        (string, string) key = (schema, new HexString(new TableHash(table)).TextValue);
        lock (Types)
        {
            if (!Types.TryGetValue(key, out EntityType? type))
            {
                type = new EntityType(table, schema);
                Types.Add(key, type);
            }
            return type;
        }
    }

    public PropertyInfo Property(string name)
    {
        return _properties.Single(x => x.Name == name);
    }

    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.NonPublicProperties
    )]
    public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
    {
        return [.. _properties];
    }

    public override object[] GetCustomAttributes(bool inherit)
    {
        Attribute[] attributes = [_table];
        return attributes;
    }

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        Attribute[] attributes = IsDefined(attributeType, inherit) ? [_table] : [];
        return attributes;
    }

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return attributeType.IsAssignableFrom(typeof(TableAttribute));
    }

    public override IList<CustomAttributeData> GetCustomAttributesData()
    {
        return [];
    }

    public override bool Equals(object? o)
    {
        return ReferenceEquals(this, o);
    }

    public override bool Equals(Type? o)
    {
        return ReferenceEquals(this, o);
    }

    public override int GetHashCode()
    {
        return RuntimeHelpers.GetHashCode(this);
    }
}
