namespace Cashflow.SharedKernel.Domain;

public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity() { }

    protected Entity(Guid id)
    {
        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        if (Id == Guid.Empty || other.Id == Guid.Empty) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    // S3875 silenciado: operador == sobrescrito é o esperado para Entity em DDD
    //   (identidade comparada por Id, não por referência). Equals/GetHashCode acima
    //   é a fonte de verdade; estes operators redirecionam para ela.
#pragma warning disable S3875
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
#pragma warning restore S3875
}
