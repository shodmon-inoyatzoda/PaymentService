namespace PaymentService.Domain.Entities.Users;

public sealed class UserRole
{
    public int UserId { get; private set; }
    public int RoleId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;
    public User? AssignedBy { get; private set; }

    private UserRole() { }

    private UserRole(int userId, int roleId)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAt = DateTimeOffset.UtcNow;
    }

    public static UserRole Create(int userId, int roleId)
    {
        return new UserRole(userId, roleId);
    }
}