# Skill: CSharpier Code Formatting

Use when formatting C# code in this project.

## Installation

```powershell
dotnet tool install -g csharpier
```

## Usage

```powershell
# Format all files in a directory
csharpier format src

# Check if files are formatted (CI use)
csharpier check src

# Format a single file
csharpier format src/LuaHelperMcpServer/Program.cs
```

## CI integration

Add to GitHub Actions workflow:

```yaml
- name: Check formatting
  run: |
    dotnet tool install -g csharpier
    csharpier check src
```

## Configuration

CSharpier uses default settings — no `.csharpierrc` file needed. It formats:
- Indentation: 4 spaces
- Braces: same line (K&R style)
- Line endings: LF
- Trailing commas: when multi-line

## Key behaviors

- CSharpier is opinionated — it does NOT have configuration options for brace style, indent size, etc.
- It formats the entire file, not just changed lines
- Run `csharpier format src` before every commit
- The `check` command exits with code 1 if any file is not formatted
- CSharpier v1.3.0+ supports .NET 10
