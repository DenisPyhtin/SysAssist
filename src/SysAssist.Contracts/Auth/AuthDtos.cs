namespace SysAssist.Contracts.Auth;

public sealed record LoginRequest(string Login, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

public sealed record CurrentUserDto(Guid Id, string Login, string DisplayName, string Email, IReadOnlyCollection<string> Roles);

public sealed record CreateUserRequest(string Login, string DisplayName, string Email, string Password, bool IsActive);

public sealed record UpdateUserRequest(string DisplayName, string Email, bool IsActive, string? Password = null);

public sealed record UpdateUserRolesRequest(IReadOnlyCollection<string> Roles);

public sealed record UserDto(Guid Id, string Login, string DisplayName, string Email, bool IsActive, IReadOnlyCollection<string> Roles);

public sealed record RoleDto(Guid Id, string Name, string? Description);
