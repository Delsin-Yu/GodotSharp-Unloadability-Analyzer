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

## References

- [Debugging assembly unloadability issues](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability#troubleshoot-unloadability-issues)
- [godot#78513 — ALC unloading regression](https://github.com/godotengine/godot/issues/78513)
- [godot-proposals#11819](https://github.com/godotengine/godot-proposals/issues/11819)
