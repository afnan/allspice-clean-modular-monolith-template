using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

public static class SharedKernelInterceptorExtensions
{
    /// <summary>
    /// Registers the cross-cutting EF Core save interceptors (concurrency diagnostics + audit-user
    /// stamping) as singletons. EF Core discovers <see cref="IInterceptor"/> services from the
    /// application service provider, so every module DbContext registered in this container picks them
    /// up automatically. A <see cref="NullCurrentUserProvider"/> is registered as the default
    /// <see cref="ICurrentUserProvider"/> unless the host has already provided one (e.g. an
    /// HttpContext-backed implementation in the gateway). Idempotent.
    /// </summary>
    public static IServiceCollection AddSharedKernelInterceptors(this IServiceCollection services)
    {
        services.TryAddSingleton<ICurrentUserProvider, NullCurrentUserProvider>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInterceptor, ConcurrencyDiagnosticInterceptor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInterceptor, AuditableEntityInterceptor>());

        return services;
    }
}
