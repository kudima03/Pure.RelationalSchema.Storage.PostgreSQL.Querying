using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The property of an <see cref="EntityType"/> that stands for a column: a readable
/// public property named after the column hash, with the column attribute the
/// translator maps. It has no value to get.
/// </summary>
internal sealed class EntityProperty : PropertyInfo
{
    private readonly EntityType _entity;

    private readonly ColumnProperty _column;

    private readonly EntityGetter _getter;

    public EntityProperty(EntityType entity, ColumnProperty column)
    {
        _entity = entity;
        _column = column;
        _getter = new EntityGetter(this);
    }

    public override string Name => _column.Name;

    public override Type PropertyType => _column.Type;

    public override Type DeclaringType => _entity;

    public override Type ReflectedType => _entity;

    public override Module Module => typeof(ElementRow).Module;

    public override PropertyAttributes Attributes => PropertyAttributes.None;

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override MethodInfo[] GetAccessors(bool nonPublic)
    {
        return [_getter];
    }

    public override MethodInfo GetGetMethod(bool nonPublic)
    {
        return _getter;
    }

    public override MethodInfo? GetSetMethod(bool nonPublic)
    {
        return null;
    }

    public override ParameterInfo[] GetIndexParameters()
    {
        return [];
    }

    public override object GetValue(
        object? obj,
        BindingFlags invokeAttr,
        Binder? binder,
        object?[]? index,
        CultureInfo? culture
    )
    {
        throw new NotSupportedException();
    }

    public override void SetValue(
        object? obj,
        object? value,
        BindingFlags invokeAttr,
        Binder? binder,
        object?[]? index,
        CultureInfo? culture
    )
    {
        throw new NotSupportedException();
    }

    public override object[] GetCustomAttributes(bool inherit)
    {
        Attribute[] attributes = [new ColumnAttribute(_column.ColumnName)];
        return attributes;
    }

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        Attribute[] attributes = IsDefined(attributeType, inherit)
            ? [new ColumnAttribute(_column.ColumnName)]
            : [];
        return attributes;
    }

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return attributeType.IsAssignableFrom(typeof(ColumnAttribute));
    }

    public override IList<CustomAttributeData> GetCustomAttributesData()
    {
        return [];
    }
}
