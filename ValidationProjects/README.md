# Validation Projects

Empirical test suite that validates which code patterns actually prevent `AssemblyLoadContext` unloading on .NET 10.

## Architecture

- **PluginHost** — Console app that loads a plugin DLL into a collectible `AssemblyLoadContext`, invokes `Execute()`, unloads the ALC, then checks `WeakReference.IsAlive` after 10 GC cycles.
- **Plugins/** — One project per test case. Each contains a `TestCase` class with a parameterless `Execute()` method that exercises a single pattern.

## Running

```powershell
# Build and run all plugins
.\run-all.ps1

# Run a single plugin
dotnet run --project PluginHost -- Plugins\Plugin.GCHandle\bin\Debug\net10.0\Plugin.GCHandle.dll
```

## Results

See [**RESULTS.md**](RESULTS.md) for the full results table and root-cause analysis.
