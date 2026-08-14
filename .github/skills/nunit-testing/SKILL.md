# Skill: NUnit Testing — Patterns & Conventions

Use when writing, fixing, or running tests in this project.

## Test project separation

| Project | Tests | Rules |
|---|---|---|
| `Tests.Unit` | Service-level unit tests | No filesystem, no real processes, no network |
| `Tests.Integration` | End-to-end tests | Real lualsp.exe, real files, real processes |

## Assertions with Shouldly

All assertions use [Shouldly](https://docs.shouldly.org/) (4.3.0, BSD-3-Clause) — never
`Assert.That`/`Assert.AreEqual` constraint syntax. One `using Shouldly;` per file.

| NUnit classic | Shouldly |
|---|---|
| `Assert.That(a, Is.EqualTo(b))` | `a.ShouldBe(b)` |
| `Assert.That(a, Is.Not.EqualTo(b))` | `a.ShouldNotBe(b)` |
| `Assert.That(x, Is.Null)` | `x.ShouldBeNull()` |
| `Assert.That(x, Is.Not.Null)` | `x.ShouldNotBeNull()` |
| `Assert.That(x, Is.True/False)` | `x.ShouldBeTrue()/ShouldBeFalse()` |
| `Assert.That(coll, Is.Empty)` | `coll.ShouldBeEmpty()` |
| `Assert.That(coll, Is.Not.Empty)` | `coll.ShouldNotBeEmpty()` |
| `Assert.That(list, Has.Count.EqualTo(n))` | `list.Count.ShouldBe(n)` |
| `Assert.That(text, Does.Contain(s))` | `text.ShouldContain(s, Case.Sensitive)` |
| `Assert.That(text, Does.Not.Contain(s))` | `text.ShouldNotContain(s, Case.Sensitive)` |
| `Assert.That(text, Does.Contain(s).IgnoreCase)` | `text.ShouldContain(s, Case.Insensitive)` |
| `Assert.That(result, Does.Match(pattern))` | `result.ShouldMatch(pattern)` (standard .NET regex, not implicitly anchored) |
| `Assert.That(dict, Does.ContainKey(k))` | `dict.ShouldContainKey(k)` |
| `Assert.That(a, Is.SameAs(b))` | `a.ShouldBeSameAs(b)` |
| `Assert.CatchAsync<T>(async () => ...)` | `await Should.ThrowAsync<T>(async () => ...)` — MUST be awaited |

Notes:
- Shouldly 4.x `ShouldContain`/`ShouldStartWith`/`ShouldEndWith` default to
  case-insensitive — always pass `Case.Sensitive`/`Case.Insensitive` explicitly.
- `ShouldBe` on collections is order-sensitive and element-wise; `ShouldHaveCount`
  is v5-only — use `Count.ShouldBe(n)` on 4.x.
- `Should.ThrowAsync<T>` catches exact type and derived types (like NUnit's `CatchAsync`).
- `Assert.Ignore` is **forbidden** in the integration project (per the test
  plan). If a required binary (`lualsp.exe`, server dll) is missing, the test
  must **fail** with a clear message via `Assert.Fail`. This supersedes the old
  "skip gracefully" guidance — CI provisions binaries via `fetch-lualsp.ps1`.

## Mocking with NSubstitute + AutoFixture

- **NSubstitute 5.3.0** — behavior stubbing and verification.
- **AutoFixture 4.18.1** (+ `AutoFixture.AutoNSubstitute`) — creates substitutes
  automatically for interface dependencies.

```csharp
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;

private static readonly IFixture Fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

// AutoFixture creates an auto-mocked substitute for any interface:
var fileReader = Fixture.Create<IFileReader>();

// Configure it with NSubstitute:
fileReader.FileExists(Arg.Any<string>()).Returns(true);
fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("file content");
```

Verification (MUST `await` async members):

```csharp
await fileReader.DidNotReceive().ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
```

## AAA pattern

```csharp
[Test]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var dependency = Fixture.Create<IDependency>();
    dependency.Method().Returns(expectedValue);

    // Act
    var result = await _sut.MethodUnderTest();

    // Assert
    result.ShouldNotBeNull();
    result.Message.ShouldContain("expected", Case.Sensitive);
}
```

## FakeLspServer pattern

For testing LspClient without a real lualsp.exe:

```csharp
var fakeServer = new FakeLspServer();  // Anonymous pipes
var processManager = new MockProcessManager(fakeServer);
var cache = new DiagnosticCache();
var fileReader = Fixture.Create<IFileReader>();
var client = new LspClient(processManager, cache, NullLogger<LspClient>.Instance, fileReader);

fakeServer.Start();
await client.EnsureInitializedAsync("C:\\test", config);
```

The `FakeLspServer` responds to `initialize`, `didOpen` (with fake diagnostics), and `shutdown`.

## Meaningful assertions

**Bad:** `diagnostics.ShouldNotBeEmpty();`

**Good:**
```csharp
diagnostics.ShouldNotBeEmpty();
diagnostics[0].Message.ShouldContain("Frame", Case.Insensitive);
diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
diagnostics[0].StartLine.ShouldBe(0);
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

- **No `Assert.Ignore`** — integration tests must **fail** if a required binary
  is missing (see `.github/docs/test-plan-luahelper-mcp-server.md` §6.2 for
  binary resolution order). CI provisions `lualsp.exe` via `fetch-lualsp.ps1`.
- Use `[SetUp]` to resolve binaries via `IntegrationTestFixture`; call
  `Assert.Fail("lualsp.exe not found. Run .github/tools/fetch-lualsp.ps1 first.")`
  if missing.
- Use `[TearDown]` to dispose resources
- Test fixtures go in `Fixtures/` directory (copied to output); each fixture has
  a golden `.expected.json` (exact diagnostics) that is updated together with
  the fixture when `lualsp.exe` is upgraded
- Assert against **golden/exact** expected values via `GoldenAssert.JsonEquals`,
  never fuzzy tolerances
- MCP-layer end-to-end tests spawn the real server binary via `McpStdioClient`
  (newline-delimited JSON-RPC over real stdio — NOT `Content-Length` framing)