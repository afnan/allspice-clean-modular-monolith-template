namespace AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

/// <summary>
/// Base class for value objects implementing rich equality and comparison semantics.
/// </summary>
public abstract class ValueObject : IComparable, IComparable<ValueObject>
{
    private int? _cachedHashCode;

    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(obj))
        {
            return false;
        }

        var valueObject = (ValueObject)obj;

        return GetEqualityComponents().SequenceEqual(valueObject.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        if (_cachedHashCode.HasValue)
        {
            return _cachedHashCode.Value;
        }

        _cachedHashCode = GetEqualityComponents()
            .Aggregate(1, (current, obj) =>
            {
                unchecked
                {
                    return current * 23 + (obj?.GetHashCode() ?? 0);
                }
            });

        return _cachedHashCode.Value;
    }

    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        var thisType = GetUnproxiedType(this);
        var otherType = GetUnproxiedType(obj);

        if (thisType != otherType)
        {
            return string.Compare(thisType.ToString(), otherType.ToString(), StringComparison.Ordinal);
        }

        var other = (ValueObject)obj;
        var components = GetEqualityComponents().ToArray();
        var otherComponents = other.GetEqualityComponents().ToArray();

        for (var i = 0; i < components.Length; i++)
        {
            var comparison = CompareComponents(components[i], otherComponents[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    public int CompareTo(ValueObject? other) => CompareTo((object?)other);

    public static bool operator ==(ValueObject? a, ValueObject? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);

    internal static Type GetUnproxiedType(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        const string EfCoreProxyPrefix = "Castle.Proxies.";
        const string NHibernateProxyPostfix = "Proxy";

        var type = obj.GetType();
        var typeString = type.ToString();

        if (typeString.Contains(EfCoreProxyPrefix, StringComparison.Ordinal) || typeString.EndsWith(NHibernateProxyPostfix, StringComparison.Ordinal))
        {
            return type.BaseType ?? type;
        }

        return type;
    }

    private static int CompareComponents(object? object1, object? object2)
    {
        if (object1 is null && object2 is null)
        {
            return 0;
        }

        if (object1 is null)
        {
            return -1;
        }

        if (object2 is null)
        {
            return 1;
        }

        if (object1 is IComparable comparable1 && object2 is IComparable comparable2)
        {
            return comparable1.CompareTo(comparable2);
        }

        return object1.Equals(object2) ? 0 : -1;
    }
}


