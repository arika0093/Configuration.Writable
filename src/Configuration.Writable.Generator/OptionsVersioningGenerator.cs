using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Configuration.Writable.Generator;

[Generator]
public sealed class OptionsVersioningGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Configuration.Writable.OptionsModelAttribute";
    private const string MetadataInterfaceName = "Configuration.Writable.IGeneratedOptionsMetadata";
    private const string LegacyInterfaceName = "Configuration.Writable.IHasVersion";
    private const string DiagnosticsDocumentationUrl =
        "https://github.com/arika0093/Configuration.Writable/blob/main/src/Configuration.Writable.Generator/README.md";

    private static readonly DiagnosticDescriptor MissingId = new(
        "CWWR001",
        "Options model ID is missing",
        "Options model '{0}' should declare a stable Id",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Warning,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr001"
    );
    private static readonly DiagnosticDescriptor InvalidId = new(
        "CWWR002",
        "Options model ID is invalid",
        "Options model '{0}' has an empty Id",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr002"
    );
    private static readonly DiagnosticDescriptor InvalidVersion = new(
        "CWWR003",
        "Options model version is invalid",
        "Options model '{0}' must declare a Version greater than zero",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr003"
    );
    private static readonly DiagnosticDescriptor DuplicateVersion = new(
        "CWWR004",
        "Options model version is duplicated",
        "Model ID '{0}' has more than one type at version {1}",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr004"
    );
    private static readonly DiagnosticDescriptor MissingPreviousVersion = new(
        "CWWR005",
        "Options model versions are not consecutive",
        "Options model '{0}' at version {1} requires an accessible version {2} with Id '{3}'",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr005"
    );
    private static readonly DiagnosticDescriptor ReservedNameCollision = new(
        "CWWR006",
        "Options model member uses a reserved metadata name",
        "Member '{0}' serializes as reserved schema metadata name '{1}'",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr006"
    );
    private static readonly DiagnosticDescriptor LegacyVersioning = new(
        "CWWR007",
        "IHasVersion is legacy versioning",
        "Options model '{0}' uses IHasVersion; declare Version on OptionsModelAttribute instead",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Warning,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr007"
    );
    private static readonly DiagnosticDescriptor PartialRequired = new(
        "CWWR008",
        "Options model must be partial",
        "Options model '{0}' must be partial so schema metadata can be generated",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr008"
    );
    private static readonly DiagnosticDescriptor UnsupportedModel = new(
        "CWWR009",
        "Versioned options model is unsupported",
        "Versioned options model '{0}' must be a non-static class with an accessible parameterless constructor",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr009"
    );
    private static readonly DiagnosticDescriptor MissingVersion = new(
        "CWWR010",
        "Options model version is missing",
        "Options model '{0}' should explicitly declare Version = 1",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Warning,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr010"
    );
    private static readonly DiagnosticDescriptor MissingMigration = new(
        "CWWR011",
        "Options migration implementation is missing",
        "Options model '{0}' must implement migration from '{1}'",
        "Configuration.Writable.Versioning",
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: DiagnosticsDocumentationUrl + "#cwwr011"
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (productionContext, compilation) => Execute(productionContext, compilation)
        );
    }

    private static void Execute(SourceProductionContext context, Compilation compilation)
    {
        var models = CollectModels(compilation);
        var sourceModels = models.Where(model => model.IsSource).ToList();
        var groups = models
            .Where(model => !string.IsNullOrWhiteSpace(model.Id) && model.Version is not null)
            .GroupBy(model => model.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        foreach (var model in sourceModels)
        {
            ReportModelDiagnostics(context, model);
        }

        foreach (var group in groups)
        {
            foreach (var versions in group.Value.GroupBy(model => model.Version!.Value))
            {
                if (versions.Count() <= 1)
                {
                    continue;
                }

                foreach (var model in versions.Where(model => model.IsSource))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(DuplicateVersion, model.Location, group.Key, versions.Key)
                    );
                }
            }
        }

        foreach (var model in sourceModels)
        {
            ModelInfo? previous = null;
            var hasMigrationImplementation = true;
            if (model.SupportMigration && model.Version is > 1 && model.Id is not null)
            {
                groups.TryGetValue(model.Id, out var candidates);
                previous = candidates?.FirstOrDefault(candidate =>
                    candidate.Version == model.Version - 1
                    && IsAccessibleFromCompilation(candidate.Symbol, compilation)
                );
                if (previous == null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            MissingPreviousVersion,
                            model.Location,
                            model.Symbol.Name,
                            model.Version,
                            model.Version - 1,
                            model.Id
                        )
                    );
                }
                else if (!HasMigrationImplementation(model.Symbol, previous.Symbol))
                {
                    hasMigrationImplementation = false;
                    var properties = ImmutableDictionary<string, string?>
                        .Empty.Add("CurrentTypeName", GetMinimalTypeName(model.Symbol))
                        .Add("PreviousTypeName", GetMinimalTypeName(previous.Symbol))
                        .Add(
                            "CodeFixAvailable",
                            CanAddMigrationImplementation(model.Symbol, previous.Symbol).ToString()
                        )
                        .Add(
                            "PreviousNamespace",
                            previous.Symbol.ContainingNamespace.IsGlobalNamespace
                                ? null
                                : previous.Symbol.ContainingNamespace.ToDisplayString()
                        );
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            MissingMigration,
                            model.Location,
                            properties,
                            model.Symbol.Name,
                            previous.Symbol.Name
                        )
                    );
                }
            }

            if (!model.HasPartialModifier)
            {
                continue;
            }

            context.AddSource(
                GetHintName(model.Symbol),
                SourceText.From(
                    GenerateMetadata(model, previous, hasMigrationImplementation),
                    Encoding.UTF8
                )
            );
        }
    }

    private static bool HasMigrationImplementation(
        INamedTypeSymbol current,
        INamedTypeSymbol previous
    ) =>
        current
            .GetMembers("Migrate")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.IsStatic
                && method.DeclaredAccessibility == Accessibility.Private
                && SymbolEqualityComparer.Default.Equals(method.ReturnType, current)
                && method.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, previous)
                && method.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax() is MethodDeclarationSyntax syntax
                    && syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
                    && (syntax.Body is not null || syntax.ExpressionBody is not null)
                )
            );

    private static bool CanAddMigrationImplementation(
        INamedTypeSymbol current,
        INamedTypeSymbol previous
    ) =>
        !current
            .GetMembers("Migrate")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, previous)
            );

    private static string GetMinimalTypeName(INamedTypeSymbol type)
    {
        if (type.TypeArguments.Length == 0)
        {
            return type.Name;
        }

        return type.Name
            + "<"
            + string.Join(", ", type.TypeArguments.Select(GetMinimalTypeArgumentName))
            + ">";
    }

    private static string GetMinimalTypeArgumentName(ITypeSymbol type) =>
        type is INamedTypeSymbol namedType ? GetMinimalTypeName(namedType) : type.Name;

    private static List<ModelInfo> CollectModels(Compilation compilation)
    {
        var result = new List<ModelInfo>();
        CollectNamespace(compilation.Assembly.GlobalNamespace, compilation, true, result);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            CollectNamespace(assembly.GlobalNamespace, compilation, false, result);
        }
        return result;
    }

    private static void CollectNamespace(
        INamespaceSymbol namespaceSymbol,
        Compilation compilation,
        bool isSource,
        List<ModelInfo> result
    )
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            CollectType(type, compilation, isSource, result);
        }
        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            CollectNamespace(child, compilation, isSource, result);
        }
    }

    private static void CollectType(
        INamedTypeSymbol type,
        Compilation compilation,
        bool isSource,
        List<ModelInfo> result
    )
    {
        var attribute = type.GetAttributes()
            .FirstOrDefault(candidate =>
                candidate.AttributeClass?.ToDisplayString() == AttributeName
            );
        if (attribute != null)
        {
            string? id = null;
            int? version = null;
            var versionSpecified = false;
            var supportMigration = true;
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "Id")
                {
                    id = argument.Value.Value as string;
                }
                else if (argument.Key == "Version")
                {
                    versionSpecified = true;
                    version = argument.Value.Value as int?;
                }
                else if (argument.Key == "SupportMigration")
                {
                    supportMigration = argument.Value.Value as bool? ?? true;
                }
            }
            if (
                isSource
                && attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax
            )
            {
                var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                foreach (var argument in syntax.ArgumentList?.Arguments ?? default)
                {
                    var name = argument.NameEquals?.Name.Identifier.ValueText;
                    var constant = semanticModel.GetConstantValue(argument.Expression);
                    if (!constant.HasValue)
                    {
                        continue;
                    }
                    if (name == "Id")
                    {
                        id = constant.Value as string;
                    }
                    else if (name == "Version")
                    {
                        versionSpecified = true;
                        version = constant.Value as int?;
                    }
                    else if (name == "SupportMigration")
                    {
                        supportMigration = constant.Value as bool? ?? true;
                    }
                }
            }

            var usesLegacyVersion = Implements(type, LegacyInterfaceName);
            if (!versionSpecified && !usesLegacyVersion)
            {
                version = 1;
            }

            result.Add(
                new ModelInfo(
                    type,
                    id,
                    version,
                    versionSpecified,
                    isSource,
                    IsPartial(type),
                    attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                        ?? type.Locations.FirstOrDefault()
                        ?? Location.None,
                    usesLegacyVersion,
                    supportMigration
                )
            );
        }

        foreach (var nested in type.GetTypeMembers())
        {
            CollectType(nested, compilation, isSource, result);
        }
    }

    private static void ReportModelDiagnostics(SourceProductionContext context, ModelInfo model)
    {
        if (model.Id == null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(MissingId, model.Location, model.Symbol.Name)
            );
        }
        else if (string.IsNullOrWhiteSpace(model.Id))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(InvalidId, model.Location, model.Symbol.Name)
            );
        }

        if (model.VersionSpecified && model.Version is <= 0)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(InvalidVersion, model.Location, model.Symbol.Name)
            );
        }
        else if (!model.VersionSpecified)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(MissingVersion, model.Location, model.Symbol.Name)
            );
        }

        if (model.UsesLegacyVersion)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(LegacyVersioning, model.Location, model.Symbol.Name)
            );
        }

        if (!model.HasPartialModifier)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(PartialRequired, model.Location, model.Symbol.Name)
            );
        }

        if (
            model.Version is not null
            && (
                model.Symbol.TypeKind != TypeKind.Class
                || model.Symbol.IsStatic
                || !HasAccessibleParameterlessConstructor(model.Symbol, model.IsSource)
            )
        )
        {
            context.ReportDiagnostic(
                Diagnostic.Create(UnsupportedModel, model.Location, model.Symbol.Name)
            );
        }

        foreach (var member in GetSerializableMembers(model.Symbol))
        {
            foreach (var serializedName in GetSerializedNames(member))
            {
                if (serializedName != "ModelId" && serializedName != "Version")
                {
                    continue;
                }

                if (
                    serializedName == "Version"
                    && model.UsesLegacyVersion
                    && !model.VersionSpecified
                    && member.Name == "Version"
                )
                {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        ReservedNameCollision,
                        member.Locations.FirstOrDefault() ?? model.Location,
                        member.Name,
                        serializedName
                    )
                );
                break;
            }
        }
    }

    private static IEnumerable<ISymbol> GetSerializableMembers(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }
                if (member is IPropertySymbol { IsIndexer: false } or IFieldSymbol)
                {
                    yield return member;
                }
            }
        }
    }

    private static IEnumerable<string> GetSerializedNames(ISymbol member)
    {
        yield return member.Name;
        foreach (var attribute in member.GetAttributes())
        {
            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Value is string value)
                {
                    yield return value;
                }
            }
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Value.Value is string value)
                {
                    yield return value;
                }
            }
        }
    }

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Length > 0
        && type.DeclaringSyntaxReferences.All(reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        );

    private static bool HasAccessibleParameterlessConstructor(
        INamedTypeSymbol type,
        bool isSource
    ) =>
        type.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0
            && (
                constructor.DeclaredAccessibility == Accessibility.Public
                || (
                    isSource
                    && constructor.DeclaredAccessibility
                        is Accessibility.Internal
                            or Accessibility.ProtectedOrInternal
                )
            )
        );

    private static bool IsAccessibleFromCompilation(
        INamedTypeSymbol type,
        Compilation compilation
    ) =>
        SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)
        || type.DeclaredAccessibility == Accessibility.Public;

    private static bool Implements(INamedTypeSymbol type, string interfaceName) =>
        type.AllInterfaces.Any(@interface => @interface.ToDisplayString() == interfaceName);

    private static string GenerateMetadata(
        ModelInfo model,
        ModelInfo? previous,
        bool hasMigrationImplementation
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");

        var namespaceName = model.Symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.Symbol.ContainingNamespace.ToDisplayString();
        if (namespaceName != null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypes = new Stack<INamedTypeSymbol>();
        for (
            var current = model.Symbol.ContainingType;
            current != null;
            current = current.ContainingType
        )
        {
            containingTypes.Push(current);
        }
        foreach (var containingType in containingTypes)
        {
            AppendTypeStart(builder, containingType, null);
        }

        AppendTypeStart(builder, model.Symbol, MetadataInterfaceName);
        builder
            .Append("    string? global::")
            .Append(MetadataInterfaceName)
            .Append(".ModelId => ")
            .Append(model.Id == null ? "null" : SymbolDisplay.FormatLiteral(model.Id, true))
            .AppendLine(";");
        builder
            .Append("    int? global::")
            .Append(MetadataInterfaceName)
            .Append(".Version => ")
            .Append(model.Version?.ToString() ?? "null")
            .AppendLine(";");
        builder
            .Append("    void global::")
            .Append(MetadataInterfaceName)
            .AppendLine(
                ".RegisterMigrations(global::Configuration.Writable.IOptionsMigrationRegistrar registrar)"
            );
        builder.AppendLine("    {");
        if (
            previous != null
            && hasMigrationImplementation
            && model.Id != null
            && model.Version is not null
        )
        {
            var previousType = previous.Symbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );
            var currentType = model.Symbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );
            builder
                .Append("        ((global::")
                .Append(MetadataInterfaceName)
                .Append(")new ")
                .Append(previousType)
                .AppendLine("()).RegisterMigrations(registrar);");
            builder
                .Append("        registrar.Register<")
                .Append(previousType)
                .Append(", ")
                .Append(currentType)
                .Append(">(Migrate, ")
                .Append(SymbolDisplay.FormatLiteral(model.Id, true))
                .Append(", ")
                .Append(previous.Version)
                .Append(", ")
                .Append(model.Version)
                .AppendLine(");");
        }
        builder.AppendLine("    }");

        if (previous != null && hasMigrationImplementation)
        {
            builder
                .Append("    private static partial ")
                .Append(model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append(" Migrate(")
                .Append(previous.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .AppendLine(" source);");
        }

        builder.AppendLine("}");
        foreach (var _ in containingTypes)
        {
            builder.AppendLine("}");
        }
        if (namespaceName != null)
        {
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    private static void AppendTypeStart(
        StringBuilder builder,
        INamedTypeSymbol type,
        string? implementedInterface
    )
    {
        builder.Append(GetAccessibility(type.DeclaredAccessibility));
        if (type.IsStatic)
        {
            builder.Append("static ");
        }
        else
        {
            if (type.IsAbstract)
            {
                builder.Append("abstract ");
            }
            if (type.IsSealed)
            {
                builder.Append("sealed ");
            }
        }
        builder.Append("partial ");
        var typeKeyword = type.TypeKind == TypeKind.Struct ? "struct " : "class ";
        if (type.IsRecord)
        {
            typeKeyword = type.TypeKind == TypeKind.Struct ? "record struct " : "record class ";
        }
        builder.Append(typeKeyword);
        builder.Append(type.Name);
        if (type.TypeParameters.Length > 0)
        {
            builder
                .Append('<')
                .Append(string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)))
                .Append('>');
        }
        if (implementedInterface != null)
        {
            builder.Append(" : global::").Append(implementedInterface);
        }
        builder.AppendLine();
        builder.AppendLine("{");
    }

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.Internal => "internal ",
            Accessibility.Private => "private ",
            Accessibility.Protected => "protected ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            Accessibility.ProtectedAndInternal => "private protected ",
            _ => "",
        };

    private static string GetHintName(INamedTypeSymbol symbol)
    {
        var name = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sanitized = new string(
            name.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray()
        );
        return sanitized + ".OptionsMetadata.g.cs";
    }

    private sealed class ModelInfo(
        INamedTypeSymbol symbol,
        string? id,
        int? version,
        bool versionSpecified,
        bool isSource,
        bool isPartial,
        Location location,
        bool usesLegacyVersion,
        bool supportMigration
    )
    {
        public INamedTypeSymbol Symbol { get; } = symbol;
        public string? Id { get; } = id;
        public int? Version { get; } = version;
        public bool VersionSpecified { get; } = versionSpecified;
        public bool IsSource { get; } = isSource;
        public bool HasPartialModifier { get; } = isPartial;
        public Location Location { get; } = location;
        public bool UsesLegacyVersion { get; } = usesLegacyVersion;
        public bool SupportMigration { get; } = supportMigration;
    }
}
