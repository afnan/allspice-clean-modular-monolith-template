using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>Test-only command exercised through <see cref="TransactionBehavior{TRequest,TResponse}"/>.</summary>
public sealed class AtomicityTestCommand : IMessage, ITransactional;

[Collection(nameof(PostgresCollection))]
public sealed class UnitOfWorkAtomicityTests(PostgresFixture pg)
{
    [Fact]
    public async Task Failed_command_rolls_back_all_writes()
    {
        var host = await TwoModuleHost.CreateAsync(pg);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var behavior = new TransactionBehavior<AtomicityTestCommand, Result>(
                sp.GetServices<IModuleDbContext>(),
                sp.GetRequiredService<IDomainEventDispatcher>(),
                sp.GetServices<IOutboxFlusher>(), // none registered here — empty set, so the flush step is a no-op
                NullLogger<TransactionBehavior<AtomicityTestCommand, Result>>.Instance);

            var users = sp.GetRequiredService<IUserRepository>();

            MessageHandlerDelegate<AtomicityTestCommand, Result> next = async (_, ct) =>
            {
                var user = User.Create(
                    ExternalUserId.From(Guid.NewGuid().ToString()),
                    "a@b.com", "a@b.com", "A", "B");
                await users.AddAsync(user, ct);
                throw new InvalidOperationException("boom after first write");
            };

            await Assert.ThrowsAnyAsync<Exception>(() =>
                behavior.Handle(new AtomicityTestCommand(), next, CancellationToken.None).AsTask());
        }

        // Fresh scope reads only committed state. On the buggy code the first AddAsync auto-committed
        // to identitydb (outside the transaction opened on notificationsdb), leaving an orphan row.
        await using (var verify = host.Services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var count = await db.Users.IgnoreQueryFilters().CountAsync();
            Assert.Equal(0, count);
        }
    }
}
