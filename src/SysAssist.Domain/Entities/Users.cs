using SysAssist.Domain.Common;

namespace SysAssist.Domain.Entities;

public sealed class User : Entity
{
    public required string Login { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

public sealed class Role : Entity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}
