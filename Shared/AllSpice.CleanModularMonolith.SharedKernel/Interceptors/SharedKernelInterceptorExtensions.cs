using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

public static class SharedKernelInterceptorExtensions
{
    /// <summary>
    /// Registers the cross-cutting EF Core save interceptors (concurrency diagnostics + audit-user
    /// stamping) <em>explicitly</em> as <see cref="IInterceptor"/> singletons (via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>).
    /// EF Core does not scan for interceptors — it applies the <see cref="IInterceptor"/> services it resolves
    /// from the application service provider, so this registration is what makes every module DbContext pick
    /// them up. A <see cref="NullCurrentUserProvider"/> is registered as the default
    /// <see cref="ICurrentUserProvider"/> unless the host has already provided one (e.g. an
    /// HttpContext-backed implementation in the gateway). Idempotent.
    /// </summary>
    public static IServiceCollection AddSharedKernelInterceptors(this IServiceCollection services)
    {
        services.TryAddSingleton<ICurrentUserProvider, NullCurrentUserProvider>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInterceptor, ConcurrencyDiagnosticInterceptor>());
        // SoftDelete runs before Auditable so a delete-turned-modify is also audit-stamped (LastModified*).
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInterceptor, SoftDeleteInterceptor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInterceptor, AuditableEntityInterceptor>());

        return services;
    }
}
