using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleAssignment;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;

namespace AllSpice.CleanModularMonolith.Identity.Domain.UnitTests;

public class ModuleRoleAssignmentTests
{
    [Fact]
    public void Create_Assignment_SetsProperties()
    {
        var userId = ExternalUserId.From("user-guid");

        var assignment = ModuleRoleAssignment.Create(userId, "HR", "Admin", "identity-admin");

        Assert.Equal(userId, assignment.UserId);
        Assert.Equal("HR", assignment.ModuleKey);
        Assert.Equal("Admin", assignment.RoleKey);
        Assert.Equal("identity-admin", assignment.AssignedBy);
        Assert.True(assignment.IsActive());
    }

    [Fact]
    public void Revoke_MarksAssignmentInactive()
    {
        var assignment = ModuleRoleAssignment.Create(ExternalUserId.From("user-guid"), "HR", "Admin", "identity-admin");

        assignment.Revoke("identity-admin");

        Assert.False(assignment.IsActive());
        Assert.NotNull(assignment.RevokedUtc);
    }
}


