# Code Generation Rules

## Expectations
- Code must be technically precise, unambiguous, and avoid bad practices.
- Keep code consistent with the coding standards below.
- Follow Microsoft's official best practices for C#, Razor, and JavaScript.
- Adhere to SOLID and DRY principles.
- Avoid security vulnerabilities and common pitfalls.
- Write clean, self-documenting, readable code; use inline comments only where needed.
- Always include XML documentation on public methods with `<summary>`, `<param>`, and `<returns>` tags.
- Structure error and exception messages clearly, with correct grammar and punctuation.
- Design thoughtfully with proper async usage, memory safety, and dependency injection.
- Prioritize maintainability, testability, and scalability.

## Coding Standards

### Naming
- Use PascalCase for classes, records, structs, methods, properties, and public fields.
- Use `_camelCase` for private fields and private constants.
- Use PascalCase for public constants.
- Use camelCase for local variables and private methods.
- Interfaces must begin with `I`.

### Formatting
- Braces on a new line and never omitted.
- Use blank lines where appropriate to improve readability.
- Expression-bodied members are allowed only for get-only properties; methods must use block bodies.
- Member order:
  1. Constants
  2. Static properties/fields
  3. Private fields
  4. Private properties
  5. Public fields
  6. Public properties
  7. Constructor
  8. Public instance methods
  9. Private instance methods
  10. Public static methods
  11. Private static methods

### Coding Practices
- Use `var` wherever possible unless it harms clarity.
- Enable and properly use nullable reference types.
- Always specify access modifiers, even when the default applies.
- Use `async` only when needed; append `Async` only if a synchronous counterpart exists.
- Prefer LINQ for simple operations; use loops for complex logic.
- Do not use exceptions for flow control.
- Do not use fire-and-forget tasks (discarded `Task`/`ValueTask`, `async void`, or `Task.Run` without awaiting).
  - If an async operation must be triggered from a synchronous API or event, marshal via `InvokeAsync(...)` and await it, or refactor to an async lifecycle method.
  - If a background task is unavoidable, add explicit exception handling and a clear shutdown or cancellation path.

### Design
- Use constructor injection only, unless absolutely necessary.
- Static methods and classes are acceptable when appropriate.
- Avoid partial classes in user code unless generated.
- Use `record` for data-only objects.
- Do not use positional record syntax.
- For records and classes with constructors, declare explicit properties and constructor bodies.
- Constructor parameter names must use `camelCase`.
- Extension methods are permitted and should follow standard naming conventions.

### Documentation
- XML documentation comments are required on all public methods:
  - Include `<summary>`, `<param>` (if applicable), and `<returns>` (when needed).
- Use inline comments sparingly and only to explain complex or non-obvious logic.
- Place attributes one per line.
- Only one type per file; the file name must match the type.
  - Exception: multiple generic variants of the same type may share a file if small and strongly related.

## Line endings
- Use CRLF line terminators for any files you write or modify.

## Enforcement
- Generate C# code that follows these standards exactly.
- If existing code does not follow these rules, call it out explicitly before proceeding.

## Pre-flight checklist
- [ ] Standards here are applied to all generated code.
- [ ] Nullable reference types are enabled and used correctly.
- [ ] Public methods include XML docs with proper tags.
- [ ] Braces are never omitted; no expression-bodied methods (except get-only properties).
- [ ] Async usage is justified; `Async` suffix only when a sync counterpart exists.
- [ ] Member order matches the specified list.
- [ ] Access modifiers are explicit everywhere.
- [ ] LINQ is used for simple operations; loops for complex logic.
- [ ] No exceptions are used for flow control.
- [ ] Design follows DI, SOLID, DRY, and avoids security pitfalls.
- [ ] Positional records are not used; explicit properties and constructors are used and constructor parameters are camelCase.
