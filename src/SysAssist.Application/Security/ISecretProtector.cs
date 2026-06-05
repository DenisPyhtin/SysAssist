namespace SysAssist.Application.Security;

public interface ISecretProtector
{
    bool IsProtected(string? value);
    string? Protect(string? value);
    string? Unprotect(string? value);
}
