using System.Runtime.CompilerServices;

namespace POS.Domain.Common;

/// <summary>
/// Lớp cơ sở cho các thực thể sử dụng khóa chính số nguyên.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    /// <summary>
    /// Khóa chính của thực thể.
    ///
    /// Giá trị bằng 0 nghĩa là thực thể chưa được lưu vào database.
    /// EF Core có thể gán giá trị thông qua private setter.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Cho biết thực thể chưa được lưu vào database.
    /// </summary>
    public bool IsTransient => Id <= 0;

    public bool Equals(Entity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        if (IsTransient || other.IsTransient)
        {
            return false;
        }

        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity other &&
               Equals(other);
    }

    public override int GetHashCode()
    {
        if (IsTransient)
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(
        Entity? left,
        Entity? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    public static bool operator !=(
        Entity? left,
        Entity? right)
    {
        return !(left == right);
    }
}