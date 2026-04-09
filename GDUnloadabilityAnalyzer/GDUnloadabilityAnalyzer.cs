using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace GDUnloadabilityAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GDUnloadabilityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.GDU0001_SubscriptionToExternalStaticEvent,
        DiagnosticDescriptors.GDU0002_GCHandleAlloc,
        DiagnosticDescriptors.GDU0003_ThreadPoolRegisterWaitForSingleObject,
        DiagnosticDescriptors.GDU0004_SystemTextJsonSerialization,
        DiagnosticDescriptors.GDU0005_NewtonsoftJsonSerialization,
        DiagnosticDescriptors.GDU0006_TypeDescriptorModification,
        DiagnosticDescriptors.GDU0007_ThreadCreation,
        DiagnosticDescriptors.GDU0008_TimerCreation,
        DiagnosticDescriptors.GDU0009_EncodingRegisterProvider,
        DiagnosticDescriptors.GDU0010_TaskRun,
        DiagnosticDescriptors.GDU0011_ThreadPoolQueueUserWorkItem);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Category A: Reference-Escaping API Calls
        context.RegisterOperationAction(AnalyzeEventAssignment, OperationKind.EventAssignment);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

        // Category B/C: Type-Caching APIs and Thread/Timer (object creation)
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    private static bool IsInToolType(ISymbol symbol)
    {
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        while (type != null)
        {
            if (type.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() == "Godot.ToolAttribute"))
                return true;
            type = type.ContainingType;
        }
        return false;
    }

    private static void AnalyzeEventAssignment(OperationAnalysisContext context)
    {
        if (!IsInToolType(context.ContainingSymbol))
            return;

        var operation = (IEventAssignmentOperation)context.Operation;

        // Only flag additions, not removals
        if (!operation.Adds)
            return;

        if (!(operation.EventReference is IEventReferenceOperation eventRef))
            return;

        var eventSymbol = eventRef.Event;

        // Must be a static event
        if (!eventSymbol.IsStatic)
            return;

        // Must be from an external assembly
        if (SymbolEqualityComparer.Default.Equals(
                eventSymbol.ContainingAssembly, context.Compilation.Assembly))
            return;

        // Only flag events from assemblies guaranteed to be in the root ALC
        // (framework/BCL/Godot). Project/package references loaded in the
        // same collectible ALC are safe because they unload together.
        if (!IsRootAlcAssembly(eventSymbol.ContainingAssembly))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.GDU0001_SubscriptionToExternalStaticEvent,
            operation.Syntax.GetLocation(),
            eventSymbol.ContainingType?.ToDisplayString(),
            eventSymbol.Name));
    }

    private static bool IsRootAlcAssembly(IAssemblySymbol assembly)
    {
        var name = assembly?.Name;
        if (name == null) return false;

        return name == "mscorlib"
               || name == "netstandard"
               || name == "System"
               || name.StartsWith("System.")
               || name.StartsWith("Microsoft.")
               || name == "GodotSharp"
               || name == "GodotSharpEditor"
               || name.StartsWith("Godot.");
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (!IsInToolType(context.ContainingSymbol))
            return;

        var operation = (IInvocationOperation)context.Operation;
        var method = operation.TargetMethod;
        var containingType = method.ContainingType?.ToDisplayString();

        if (containingType == null)
            return;

        DiagnosticDescriptor? descriptor = null;

        // Category A: Reference-Escaping API Calls
        if (containingType == "System.Runtime.InteropServices.GCHandle" && method.Name == "Alloc")
            descriptor = DiagnosticDescriptors.GDU0002_GCHandleAlloc;
        else if (containingType == "System.Threading.ThreadPool" && method.Name == "RegisterWaitForSingleObject")
            descriptor = DiagnosticDescriptors.GDU0003_ThreadPoolRegisterWaitForSingleObject;
        // Category B: Type-Caching APIs
        else if (containingType == "System.Text.Json.JsonSerializer"
                 && (method.Name == "Serialize" || method.Name == "Deserialize"
                     || method.Name == "SerializeAsync" || method.Name == "DeserializeAsync"
                     || method.Name == "SerializeToDocument" || method.Name == "SerializeToElement"
                     || method.Name == "SerializeToNode" || method.Name == "SerializeToUtf8Bytes"))
            descriptor = DiagnosticDescriptors.GDU0004_SystemTextJsonSerialization;
        else if ((containingType == "Newtonsoft.Json.JsonConvert"
                  && (method.Name == "SerializeObject" || method.Name == "DeserializeObject"))
                 || (containingType == "Newtonsoft.Json.JsonSerializer"
                     && (method.Name == "Serialize" || method.Name == "Deserialize")))
            descriptor = DiagnosticDescriptors.GDU0005_NewtonsoftJsonSerialization;
        else if (containingType == "System.ComponentModel.TypeDescriptor"
                 && (method.Name == "AddAttributes" || method.Name == "AddProvider"
                     || method.Name == "AddProviderTransparent" || method.Name == "Refresh"))
            descriptor = DiagnosticDescriptors.GDU0006_TypeDescriptorModification;
        else if (containingType == "System.Text.Encoding" && method.Name == "RegisterProvider")
            descriptor = DiagnosticDescriptors.GDU0009_EncodingRegisterProvider;
        // Category C: Task/ThreadPool work items
        else if (containingType == "System.Threading.Tasks.Task" && method.Name == "Run")
            descriptor = DiagnosticDescriptors.GDU0010_TaskRun;
        else if (containingType == "System.Threading.ThreadPool" && method.Name == "QueueUserWorkItem")
            descriptor = DiagnosticDescriptors.GDU0011_ThreadPoolQueueUserWorkItem;

        if (descriptor != null)
        {
            var diagnostic = descriptor.Id == "GDU0006"
                ? Diagnostic.Create(descriptor, operation.Syntax.GetLocation(), method.Name)
                : Diagnostic.Create(descriptor, operation.Syntax.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        if (!IsInToolType(context.ContainingSymbol))
            return;

        var operation = (IObjectCreationOperation)context.Operation;
        var createdType = operation.Type?.ToDisplayString();

        if (createdType == "System.Threading.Thread")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GDU0007_ThreadCreation,
                operation.Syntax.GetLocation()));
        }
        else if (createdType == "System.Threading.Timer" || createdType == "System.Timers.Timer")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GDU0008_TimerCreation,
                operation.Syntax.GetLocation()));
        }
    }
}
