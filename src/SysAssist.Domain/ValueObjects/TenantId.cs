namespace SysAssist.Domain.ValueObjects;

public readonly record struct TenantId(string Value)
{
    public static TenantId Demo => new("demo");

    public override string ToString() => Value;
}
