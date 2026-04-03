# Code Style & Conventions

- File-scoped namespaces (enforced as warning)
- Private fields: `_camelCase` prefix
- Constants: PascalCase
- `var` usage preferred
- Braces preferred (`csharp_prefer_braces = true`)
- System usings sorted first
- Central package versioning via `Directory.Packages.props` — never specify versions in .csproj
- Indent: 2 spaces for C# files, UTF-8 BOM encoding
- XML/config files: 2-space indent
- Template variables `{{ProjectName}}` and `{{ProjectNameLower}}` in config files
