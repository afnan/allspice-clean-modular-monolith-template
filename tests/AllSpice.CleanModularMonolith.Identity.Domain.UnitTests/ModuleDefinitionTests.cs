using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;

namespace AllSpice.CleanModularMonolith.Identity.Domain.UnitTests;

public class ModuleDefinitionTests
{
    [Fact]
    public void Create_ModuleDefinition_Succeeds()
    {
        var module = ModuleDefinition.Create("HR", "Human Resources", "HR module");

        Assert.Equal("HR", module.Key);
        Assert.Equal("Human Resources", module.DisplayName);
    }

    [Fact]
    public void AddRole_AddsRoleToModule()
    {
        var module = ModuleDefinition.Create("Finance", "Finance", "Finance module");

        var role = module.AddRole("Admin", "Finance Administrator", "Full access");

        Assert.Single(module.Roles);
        Assert.Equal("Admin", role.RoleKey);
    }

    [Fact]
    public void AddRole_Throws_WhenDuplicateRole()
    {
        var module = ModuleDefinition.Create("Events", "Events", "Events module");
        module.AddRole("Viewer", "Viewer", "Read only");

        Assert.Throws<InvalidOperationException>(() => module.AddRole("Viewer", "Viewer", "Duplicate"));
    }
}


