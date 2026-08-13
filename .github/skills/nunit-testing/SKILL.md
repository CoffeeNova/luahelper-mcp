# Skill: NUnit Testing — Patterns & Conventions

Use when writing, fixing, or running tests in this project.

## Test project separation

| Project | Tests | Rules |
|---|---|---|
| `Tests.Unit` | Service-level unit tests | No filesystem, no real processes, no network |
| `Tests.Integration` | End-to-end tests | Real lualsp.exe, real files, real processes |

## NUnit vs xUnit mapping

| xUnit | NUnit |
|---|---|
| `[Fact]` | `[Test]` |
| `[Theory]` + `[InlineData]` | `[TestCase]` |
| `Assert.Equal(a, b)` | `Assert.That(b, Is.EqualTo(a))` |
| `Assert.NotNull(x)` | `Assert.That(x, Is.Not.Null)` |
| `Assert.Null(x)` | `Assert.That(x, Is.Null)` |
| `Assert.True(x)` | `Assert.That(x, Is.True)` |
| `Assert.False(x)` | `Assert.That(x, Is.False)` |
| `Assert.Empty(x)` | `Assert.That(x, Is.Empty)` |
| `Assert.NotEmpty(x)` | `Assert.That(x, Is.Not.Empty)` |
| `Assert.Contains(x, list)` | `Assert.That(list, Does.Contain(x))` |
| `Assert.Contains(x, pred)` | `Assert.That(list, Has.Some.Matches(pred))` |
| `Assert.ThrowsAsync<T>(...)` | `Assert.ThrowsAsync<T>(...)` |
| `Assert.ThrowsAnyAsync<T>(...)` | `Assert.CatchAsync<T>(...)` |
| `Assert.Same(a, b)` | `Assert.That(b, Is.SameAs(a))` |
| `Assert.DoesNotContain(x, text)` | `Assert.That(text, Does.Not.Contain(x))` |
| Constructor setup | `[SetUp]` method |
| `IDisposable` + `Dispose()` | `[TearDown]` method |

## AAA pattern

```csharp
[Test]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var mock = new Mock<IDependency>();
    mock.Setup(m => m.Method()).ReturnsAsync(expectedValue);

    // Act
    var result = await _sut.MethodUnderTest();

    // Assert
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Message, Does.Contain("expected"));
}
```

## Mocking with Moq

```csharp
var mock = new Mock<IFileReader>();
mock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
mock.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync("file content");
```

## FakeLspServer pattern

For testing LspClient without a real lualsp.exe:

```csharp
var fakeServer = new FakeLspServer();  // Anonymous pipes
var processManager = new MockProcessManager(fakeServer);
var cache = new DiagnosticCache();
var client = new LspClient(processManager, cache, NullLogger<LspClient>.Instance, fileReaderMock.Object);

fakeServer.Start();
await client.EnsureInitializedAsync("C:\\test", config);
```

The `FakeLspServer` responds to `initialize`, `didOpen` (with fake diagnostics), and `shutdown`.

## Meaningful assertions

**Bad:** `Assert.NotEmpty(diagnostics);`

**Good:**
```csharp
Assert.That(diagnostics, Is.Not.Empty);
Assert.That(diagnostics[0].Message, Does.Contain("Frame"));
Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
Assert.That(diagnostics[0].StartLine, Is.EqualTo(0));
```

## Running tests

```powershell
# Unit tests only (fast)
dotnet test src/LuaHelperMcpServer.Tests.Unit

# Integration tests (requires lualsp.exe)
dotnet test src/LuaHelperMcpServer.Tests.Integration

# Specific test
dotnet test src/LuaHelperMcpServer.Tests.Unit --filter "FullyQualifiedName~LspMessageReaderTests"
```

## Integration test conventions

- Use `[SetUp]` to check if lualsp.exe exists; call `Assert.Ignore()` if not found
- Use `[TearDown]` to dispose resources
- Read `LUAHELPER_EXTENSION_PATH` env var for lualsp.exe location
- Test fixtures go in `Fixtures/` directory (copied to output)
