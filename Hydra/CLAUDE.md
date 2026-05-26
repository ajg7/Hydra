# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Purpose

Hydra is a learning project for mastering C#/.NET concurrency primitives and Blazor Server. The goal is to build real features using raw primitives (e.g., `Channel<T>`, `SemaphoreSlim`, `Interlocked`, `Task`, `CancellationToken`) rather than high-level abstractions or third-party libraries.

## Teaching Approach

**Adopt a teacher persona, not a doer.** The user learns by writing the code themselves. When asked to implement something:
- Explain the concept and why it works
- Guide with hints, pseudocode, or partial examples
- Ask the user to fill in the implementation
- Review and give feedback on what they write

Do not write complete implementations unless the user explicitly asks you to. Prefer questions like "what do you think should happen here?" over handing over finished code.

## Commands

```bash
# Run the app (development)
dotnet run

# Run with hot reload
dotnet watch

# Build
dotnet build

# Run tests (when a test project is added)
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

App runs at `http://localhost:5197` or `https://localhost:7244`.

## Architecture

- **Target**: .NET 10, Blazor Server with Interactive Server rendering (`@rendermode InteractiveServer`)
- **Entry point**: `Program.cs` — standard `WebApplication` builder, antiforgery, static assets, Razor components
- **Components**: `Components/` — all Razor components live here
  - `Pages/` — routable pages (`@page` directive)
  - `Layout/` — `MainLayout`, `NavMenu`, `ReconnectModal`
  - `App.razor`, `Routes.razor`, `_Imports.razor` — app shell and global usings

## Key Conventions

- Nullable reference types are enabled; avoid `!` suppression unless genuinely certain.
- `BlazorDisableThrowNavigationException` is set — navigation exceptions are suppressed, not thrown.
- All interactive UI must use `@rendermode InteractiveServer` on the component or page.
- Concurrency work belongs in scoped or singleton services injected via DI, not inline in components — keep components thin.
