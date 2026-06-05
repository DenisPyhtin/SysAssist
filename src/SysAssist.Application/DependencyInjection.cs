using Microsoft.Extensions.DependencyInjection;
using SysAssist.Application.Common;

namespace SysAssist.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
