# Suggested Commands

## Build & Run
```bash
dotnet restore AllSpice.CleanModularMonolith.slnx
dotnet build AllSpice.CleanModularMonolith.slnx
dotnet run --project AllSpice.CleanModularMonolith.AppHost/AllSpice.CleanModularMonolith.AppHost.csproj
```

## Testing
```bash
dotnet test AllSpice.CleanModularMonolith.slnx
dotnet test tests/AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests
dotnet test tests/AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests --filter "FullyQualifiedName~TestMethodName"
```

## Template Operations
```bash
dotnet new install .
dotnet new allspice-modular -n Contoso.Erp
dotnet new uninstall .
```

## System (Windows with bash shell)
Standard unix commands (ls, grep, find, git) work via bash. Use forward slashes in paths.
