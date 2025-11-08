using Ardalis.Specification;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.SharedKernel.Repositories;

public interface IRepository<T> : IRepositoryBase<T>
    where T : AggregateRoot
{
}


