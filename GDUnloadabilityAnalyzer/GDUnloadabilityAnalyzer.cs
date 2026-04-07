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
        DiagnosticDescriptors.GDU0001_ThreadStaticField,
        DiagnosticDescriptors.GDU0002_SubscriptionToExternalStaticEvent,
        DiagnosticDescriptors.GDU0003_GCHandleAlloc,
        DiagnosticDescriptors.GDU0004_MarshalGetFunctionPointerForDelegate,
        DiagnosticDescriptors.GDU0005_ThreadPoolRegisterWaitForSingleObject,
        DiagnosticDescriptors.GDU0006_SystemTextJsonSerialization,
        DiagnosticDescriptors.GDU0007_NewtonsoftJsonSerialization,
        DiagnosticDescriptors.GDU0008_XmlSerializerConstruction,
        DiagnosticDescriptors.GDU0009_TypeDescriptorModification,
        DiagnosticDescriptors.GDU0010_ThreadCreation,
        DiagnosticDescriptors.GDU0011_TimerCreation,
        DiagnosticDescriptors.GDU0012_EncodingRegisterProvider);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Category A: Declaration-Level Hazards — Fields
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);

        // Category B: Reference-Escaping API Calls
        context.RegisterOperationAction(AnalyzeEventAssignment, OperationKind.EventAssignment);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

        // Category C: Type-Caching APIs (object creation)
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

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var field = (IFieldSymbol)context.Symbol;

        if (!IsInToolType(field))
            return;

        // GDU0001: [ThreadStatic] field
        if (field.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "System.ThreadStaticAttribute"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GDU0001_ThreadStaticField,
                field.Locations[0],
                field.Name));
        }
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

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.GDU0002_SubscriptionToExternalStaticEvent,
            operation.Syntax.GetLocation(),
            eventSymbol.ContainingType?.ToDisplayString(),
            eventSymbol.Name));
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

        if (containingType == "System.Runtime.InteropServices.GCHandle" && method.Name == "Alloc")
            descriptor = DiagnosticDescriptors.GDU0003_GCHandleAlloc;
        else if (containingType == "System.Runtime.InteropServices.Marshal" && method.Name == "GetFunctionPointerForDelegate")
            descriptor = DiagnosticDescriptors.GDU0004_MarshalGetFunctionPointerForDelegate;
        else if (containingType == "System.Threading.ThreadPool" && method.Name == "RegisterWaitForSingleObject")
            descriptor = DiagnosticDescriptors.GDU0005_ThreadPoolRegisterWaitForSingleObject;
        // Category C: Type-Caching APIs
        else if (containingType == "System.Text.Json.JsonSerializer"
                 && (method.Name == "Serialize" || method.Name == "Deserialize"
                     || method.Name == "SerializeAsync" || method.Name == "DeserializeAsync"
                     || method.Name == "SerializeToDocument" || method.Name == "SerializeToElement"
                     || method.Name == "SerializeToNode" || method.Name == "SerializeToUtf8Bytes"))
            descriptor = DiagnosticDescriptors.GDU0006_SystemTextJsonSerialization;
        else if ((containingType == "Newtonsoft.Json.JsonConvert"
                  && (method.Name == "SerializeObject" || method.Name == "DeserializeObject"))
                 || (containingType == "Newtonsoft.Json.JsonSerializer"
                     && (method.Name == "Serialize" || method.Name == "Deserialize")))
            descriptor = DiagnosticDescriptors.GDU0007_NewtonsoftJsonSerialization;
        else if (containingType == "System.ComponentModel.TypeDescriptor"
                 && (method.Name == "AddAttributes" || method.Name == "AddProvider"
                     || method.Name == "AddProviderTransparent" || method.Name == "Refresh"))
            descriptor = DiagnosticDescriptors.GDU0009_TypeDescriptorModification;
        else if (containingType == "System.Text.Encoding" && method.Name == "RegisterProvider")
            descriptor = DiagnosticDescriptors.GDU0012_EncodingRegisterProvider;

        if (descriptor != null)
        {
            var diagnostic = descriptor.Id == "GDU0009"
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

        if (createdType == "System.Xml.Serialization.XmlSerializer")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GDU0008_XmlSerializerConstruction,
                operation.Syntax.GetLocation()));
        }
        else if (createdType == "System.Threading.Thread")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GDU0010_ThreadCreation,
                operation.Syntax.GetLocation()));
        }
        else if (createdType == "System.Threading.Timer" || createdType == "System.Timers.Timer")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GDU0011_TimerCreation,
                operation.Syntax.GetLocation()));
        }
    }
}
