# Unit Testing Rules (xUnit + Moq + AwesomeAssertions)

- Frameworks: xUnit, Moq, AwesomeAssertions.
- Use Moq for test doubles; do not introduce fake or stub implementations.
- Moq callbacks should only be used when verifying the same behavior is impossible with `Verify`.
- Do not add comments to test code.
- Braces must never be omitted.
- Expression-bodied members are not permitted in tests.

## Naming
- Test class name: `<ClassName>Tests`
- Test namespace mirrors the product namespace with `.Test` inserted before the final segment.
- Test method names use Given-When-Then:
  - `GIVEN_StateOfItem_WHEN_PerformingOperation_THEN_ShouldBeExpectedState`

## Test Class Structure
- The system under test is a readonly field named `_target`.
- `_target` is constructed in the test class constructor.
- Mocks used across tests are private readonly fields created with `Mock.Of<T>()`.
- Mocks local to a single test method should use `new Mock<T>()`.

## Test Data Conventions
- Strings use the property name as the value, not `nameof(...)`.
- Dates use a fixed point in time: `2000-01-01 00:00` with the correct `DateTimeKind`.
- Numeric values must be contextually appropriate.

## Coverage and Access
- Tests must cover 100% of the lines and branches of the implementation.
- Never use reflection to invoke implementation code.
- Cover private or protected methods through normal execution flow only.
- Do not add test-only hooks or methods to production code.
- If coverage gaps exist that cannot be reached through normal flows, prefer a refactor that exposes the behavior through production paths.

## Clarification Policy
- Do not make assumptions. If any referenced code or behavior is unclear, ask for clarification before writing tests.

## Line endings
- Use CRLF line terminators for any files you write or modify.

## Test Execution
- After each behavior-affecting set of changes, follow the test execution instructions in the root `AGENTS.md`.
- If the change is docs-only/report-only/markdown-only and does not affect behavior, test execution is optional unless explicitly requested.

## Anti-Smell Rules
- Do not inspect invocation internals in assertions.
- Prefer `Verify(...)` for Moq assertions when possible.
- If invocation history must be reset between phases, use shared helpers rather than ad-hoc invocation-list manipulation.
- Do not use `Task.Delay(...)` for synchronization or timing in tests.
- Use deterministic waiting primitives instead.

## Pre-Flight Checklist
- [ ] I am using xUnit, Moq, and AwesomeAssertions.
- [ ] No comments will be included in the test code.
- [ ] Class name is `<ClassName>Tests`.
- [ ] Namespace mirrors the product namespace with `.Test` inserted appropriately.
- [ ] Methods follow `GIVEN_..._WHEN_..._THEN_...` naming.
- [ ] `_target` exists as a readonly field and is constructed in the test class constructor.
- [ ] Class-level mocks are `Mock.Of<T>()`; method-local mocks use `new Mock<T>()`.
- [ ] Strings use property names as values; dates use `2000-01-01 00:00` with correct `DateTimeKind`; numbers are sensible.
- [ ] No expression-bodied members; braces are always present.
- [ ] No reflection is used; private logic is covered through normal flows.
- [ ] Planned tests achieve 100% line and branch coverage.
- [ ] Any uncertainties have been raised and clarified.
- [ ] If coverage would require new public or test-only hooks, I have stopped and asked for a refactor approval.
- [ ] No `Task.Delay(...)` is used for test synchronization.
