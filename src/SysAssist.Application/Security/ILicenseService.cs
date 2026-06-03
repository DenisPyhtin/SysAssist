using SysAssist.Contracts.Api;

namespace SysAssist.Application.Security;

public interface ILicenseService
{
    LicenseStatusDto GetStatus();
}
