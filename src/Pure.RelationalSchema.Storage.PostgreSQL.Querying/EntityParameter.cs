using System.Reflection;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The return parameter of an <see cref="EntityGetter"/>, which carries no
/// attributes: the nullability of a column is that of its type.
/// </summary>
internal sealed class EntityParameter : ParameterInfo
{
    public EntityParameter(MemberInfo member, Type type)
    {
        MemberImpl = member;
        ClassImpl = type;
        PositionImpl = -1;
        AttrsImpl = ParameterAttributes.Retval;
    }

    public override object[] GetCustomAttributes(bool inherit)
    {
        return [];
    }

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        return [];
    }

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return false;
    }

    public override IList<CustomAttributeData> GetCustomAttributesData()
    {
        return [];
    }
}
