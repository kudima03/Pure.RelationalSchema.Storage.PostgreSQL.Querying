using System.Globalization;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The public getter of an <see cref="EntityProperty"/>, which the translator and the
/// expression tree require of a readable property. It cannot be invoked.
/// </summary>
internal sealed class EntityGetter : MethodInfo
{
    private readonly EntityProperty _property;

    private readonly EntityParameter _result;

    public EntityGetter(EntityProperty property)
    {
        _property = property;
        _result = new EntityParameter(this, property.PropertyType);
    }

    public override string Name => "get_" + _property.Name;

    public override Type DeclaringType => _property.DeclaringType;

    public override Type ReflectedType => _property.ReflectedType;

    public override Module Module => _property.Module;

    public override Type ReturnType => _property.PropertyType;

    public override ParameterInfo ReturnParameter => _result;

    public override ICustomAttributeProvider ReturnTypeCustomAttributes => _result;

    public override MethodAttributes Attributes =>
        MethodAttributes.Public
        | MethodAttributes.HideBySig
        | MethodAttributes.SpecialName;

    public override RuntimeMethodHandle MethodHandle => throw new NotSupportedException();

    public override CallingConventions CallingConvention =>
        CallingConventions.Standard | CallingConventions.HasThis;

    public override MethodInfo GetBaseDefinition()
    {
        return this;
    }

    public override MethodImplAttributes GetMethodImplementationFlags()
    {
        return MethodImplAttributes.IL;
    }

    public override ParameterInfo[] GetParameters()
    {
        return [];
    }

    public override object Invoke(
        object? obj,
        BindingFlags invokeAttr,
        Binder? binder,
        object?[]? parameters,
        CultureInfo? culture
    )
    {
        throw new NotSupportedException();
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
