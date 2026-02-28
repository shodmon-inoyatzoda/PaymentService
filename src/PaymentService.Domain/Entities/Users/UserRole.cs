namespace PaymentService.Domain.Entities.Users;

public sealed class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;
    public User? AssignedBy { get; private set; }

    private UserRole() { }

    private UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAt = DateTimeOffset.UtcNow;
    }

    public static UserRole Create(Guid userId, Guid roleId)
    {
        return new UserRole(userId, roleId);
    }
}