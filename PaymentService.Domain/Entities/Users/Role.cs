using PaymentService.Domain.Common;

namespace PaymentService.Domain.Entities.Users;

public sealed class Role : BaseEntity
{
    private readonly List<UserRole> _userRoles = [];

    public static class Names
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string Teacher = "Teacher";
        public const string Student = "Student";
        public const string Guest = "Guest";
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    private Role() { }

    private Role(string name, string? description)
    {
        Name = name;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<Role> Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Role>(
                Error.Validation("Role.Name.Empty", "Role name is required"));
        }

        name = name.Trim();
        if (name.Length > 50)
        {
            return Result.Failure<Role>(
                Error.Validation("Role.Name.TooLong", "Role name cannot exceed 50 characters"));
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            description = description.Trim();
            if (description.Length > 500)
            {
                return Result.Failure<Role>(
                    Error.Validation("Role. Description.TooLong", "Description cannot exceed 500 characters"));
            }
        }

        var role = new Role(name, description);

        return Result.Success(role);
    }

    public Result UpdateDescription(string description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            description = description.Trim();
            if (description.Length > 500)
            {
                return Result.Failure(
                    Error.Validation("Role. Description.TooLong", "Description cannot exceed 500 characters"));
            }
        }

        Description = description;
        UpdateTimestamp();

        return Result.Success();
    }

    public bool IsSystemRole()
    {
        return Name is Names.SuperAdmin or Names.Admin or Names.Teacher or Names.Student or Names.Guest;
    }
}