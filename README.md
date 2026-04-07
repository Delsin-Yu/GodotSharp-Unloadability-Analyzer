# GDUnloadabilityAnalyzer

A Roslyn analyzer that detects code patterns in Godot .NET projects which prevent `AssemblyLoadContext` unloading — the mechanism Godot uses for C# hot-reload in the editor.

## Problem

When the Godot editor reloads C# assemblies (e.g. after a rebuild), it unloads the old `AssemblyLoadContext` and creates a new one. Certain code patterns create GC roots or global registrations that keep the old assembly alive, causing:

- Memory leaks on every rebuild
- Editor instability
- Failure to pick up code changes without restarting the editor

## Diagnostic Rules

| ID | Category | Description |
|----|----------|-------------|
| GDU0001 | Declaration-Level | `[ThreadStatic]` field |
| GDU0002 | Reference-Escaping | Subscription to external static event |
| GDU0003 | Reference-Escaping | `GCHandle.Alloc` usage |
| GDU0004 | Reference-Escaping | `Marshal.GetFunctionPointerForDelegate` usage |
| GDU0005 | Reference-Escaping | `ThreadPool.RegisterWaitForSingleObject` usage |
| GDU0006 | Type-Caching | `System.Text.Json` serialization |
| GDU0007 | Type-Caching | `Newtonsoft.Json` serialization |
| GDU0008 | Type-Caching | `XmlSerializer` construction |
| GDU0009 | Type-Caching | `TypeDescriptor` modification |
| GDU0010 | Thread/Timer | `Thread` creation |
| GDU0011 | Thread/Timer | `Timer` creation |
| GDU0012 | Global Registration | `Encoding.RegisterProvider` usage |

## Usage

Add a `ProjectReference` to your Godot project's `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="path\to\GDUnloadabilityAnalyzer.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

All 12 diagnostics are enabled by default as warnings. Suppress individual rules via `.editorconfig` or `#pragma warning disable`:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.GDU0001.severity = none
```

## Limitations

- **Compiled packages only expose metadata.** The analyzer operates on source code via Roslyn syntax/semantic analysis. Code inside pre-compiled NuGet packages or referenced assemblies cannot be inspected — if a third-party library internally uses patterns like `GCHandle.Alloc` or `new Thread(...)`, this analyzer will not detect them.
- **Only `[Tool]`-annotated types are analyzed.** Diagnostics are only reported for types (or types nested within types) marked with `[Tool]`, since only those types execute inside the Godot editor where ALC unloading applies.
- **No data-flow or inter-procedural analysis.** The analyzer checks for direct API usage at the call site. Indirect usage through helper methods, reflection, or dynamic dispatch is not detected.

We welcome community contributions of additional unloadability-hazard patterns. If you encounter a case that this analyzer does not cover, please open an issue with a minimal reproduction.

## References

- [Debugging assembly unloadability issues](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability#troubleshoot-unloadability-issues)
- [godot#78513 — ALC unloading regression](https://github.com/godotengine/godot/issues/78513)
- [godot-proposals#11819](https://github.com/godotengine/godot-proposals/issues/11819)
