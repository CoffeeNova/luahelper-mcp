# Skill: .NET Project Scaffolding

Use when creating new projects, adding packages, or modifying project files in this solution.

## Solution structure

```
LuaHelperMcpServer.slnx
├── src/LuaHelperMcpServer/                    # Main console app (net10.0)
├── src/LuaHelperMcpServer.Tests.Unit/         # Unit tests (NUnit, net10.0)
└── src/LuaHelperMcpServer.Tests.Integration/  # Integration tests (NUnit, net10.0)
```

## Creating a new project

```powershell
# Console app
dotnet new console -n ProjectName -o src\ProjectName --framework net10.0

# NUnit test project
dotnet new nunit -n ProjectName.Tests -o src\ProjectName.Tests --framework net10.0

# Add to solution
dotnet sln add src\ProjectName\ProjectName.csproj

# Add project reference
dotnet add src\ProjectName.Tests\ProjectName.Tests.csproj reference src\ProjectName\ProjectName.csproj
```

## Key packages

### Main project
```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.11" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.11" />
<!-- Phase 1+ -->
<PackageReference Include="ModelContextProtocol" Version="2.1.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

### Unit test project
```xml
<PackageReference Include="NUnit" Version="4.3.2" />
<PackageReference Include="NUnit3TestAdapter" Version="5.0.0" />
<PackageReference Include="NUnit.Analyzers" Version="4.7.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
<PackageReference Include="Moq" Version="4.20.72" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.11" />
```

### Integration test project
```xml
<PackageReference Include="NUnit" Version="4.3.2" />
<PackageReference Include="NUnit3TestAdapter" Version="5.0.0" />
<PackageReference Include="NUnit.Analyzers" Version="4.7.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.11" />
```

## Project file conventions

- `<ImplicitUsings>enable</ImplicitUsings>` — auto-imports System, System.Collections.Generic, etc.
- `<Nullable>enable</Nullable>` — nullable reference types enabled
- Copy `appsettings.json` to output: `<Content Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />`
- Copy test fixtures: `<Content Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />`

## InternalsVisibleTo

To expose `internal` members to test projects, add to the source `.csproj`:

```xml
<ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
        <_Parameter1>LuaHelperMcpServer.Tests.Unit</_Parameter1>
    </AssemblyAttribute>
</ItemGroup>
```

Alternatively, make the member `public` with a doc comment explaining it's for testing.
