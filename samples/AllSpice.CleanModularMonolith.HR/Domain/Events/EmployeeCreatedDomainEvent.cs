using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.HR.Domain.Events;

public sealed class EmployeeCreatedDomainEvent : DomainEventBase
{
    public EmployeeCreatedDomainEvent(Guid employeeId, string firstName, string lastName, string email)
    {
        EmployeeId = employeeId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public Guid EmployeeId { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string Email { get; }
}


