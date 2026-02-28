namespace PaymentService.Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var valueObjectType = (ValueObject)obj;

        return GetEqualityComponents().SequenceEqual(valueObjectType.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        const int seed = 17;
        const int multiplier = 31;
        return GetEqualityComponents()
            .Aggregate(seed, (current, obj) =>
            {
                var hash = obj?.GetHashCode() ?? 0;
                return current * multiplier + hash;
            });
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }
}
