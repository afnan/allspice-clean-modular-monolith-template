using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.HR.Domain.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.HR.Domain.Aggregates;

public class Employee : AuditableEntity
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime HireDate { get; private set; }

    // Private constructor for EF Core
    private Employee() { }

    // Factory method for creating new Employee
    public static Employee Create(string firstName, string lastName, string email, DateTime hireDate)
    {
        Guard.Against.NullOrWhiteSpace(firstName, nameof(firstName));
        Guard.Against.NullOrWhiteSpace(lastName, nameof(lastName));
        Guard.Against.NullOrWhiteSpace(email, nameof(email));
        Guard.Against.Default(hireDate, nameof(hireDate));

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            HireDate = hireDate
        };

        employee.SetCreated(null);
        employee.RegisterDomainEvent(new EmployeeCreatedDomainEvent(employee.Id, employee.FirstName, employee.LastName, employee.Email));

        return employee;
    }

    public void UpdateName(string firstName, string lastName)
    {
        FirstName = Guard.Against.NullOrWhiteSpace(firstName, nameof(firstName));
        LastName = Guard.Against.NullOrWhiteSpace(lastName, nameof(lastName));

        SetModified(null);
    }

    public void UpdateEmail(string email)
    {
        Email = Guard.Against.NullOrWhiteSpace(email, nameof(email));

        SetModified(null);
    }
}


