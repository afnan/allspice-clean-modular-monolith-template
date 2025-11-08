using Ardalis.Specification;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

public interface IReadRepository<T> : IReadRepositoryBase<T>
    where T : AggregateRoot
{
}


