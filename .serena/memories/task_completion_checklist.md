# Task Completion Checklist

After completing a task:
1. Build: `dotnet build AllSpice.CleanModularMonolith.slnx`
2. Run relevant tests: `dotnet test AllSpice.CleanModularMonolith.slnx`
3. Verify no new warnings introduced (warnings are not treated as errors but should be minimized)
4. If adding a new module, ensure it's registered in `GatewayModuleRegistrationExtensions.RegisterGatewayModules()` and database ensured in `Program.cs`
5. If adding packages, add version to `Directory.Packages.props` only
