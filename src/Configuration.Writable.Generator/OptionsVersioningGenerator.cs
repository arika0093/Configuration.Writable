using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
    private const string MigrationInterfaceName = "Configuration.Writable.IOptionsMigration";
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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sourceModels = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, cancellationToken) =>
                    SourceModelInfo.Create(attributeContext, cancellationToken)
            )
            .Collect()
            .Select(static (models, _) => new EquatableArray<SourceModelInfo>(models));
        var referencedModels = context.CompilationProvider.Select(
            static (compilation, cancellationToken) =>
                CollectReferencedModels(compilation, cancellationToken)
        );

        context.RegisterSourceOutput(
            sourceModels.Combine(referencedModels),
            static (productionContext, models) =>
                Execute(productionContext, models.Left, models.Right)
        );
    }

    private static void Execute(
        SourceProductionContext context,
        EquatableArray<SourceModelInfo> sourceModels,
        EquatableArray<ReferencedModelInfo> referencedModels
    )
    {
        var models = sourceModels
            .Select(static model => new ModelReference(
                model.Id,
                model.SchemaVersion,
                model.FullName,
                model.Name,
                model.Namespace,
                true
            ))
            .Concat(
                referencedModels.Select(static model => new ModelReference(
                    model.Id,
                    model.Version,
                    model.FullName,
                    model.Name,
                    model.Namespace,
                    model.IsPublic
                ))
            );
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

                foreach (
                    var model in sourceModels.Where(model =>
                        model.Id == group.Key && model.SchemaVersion == versions.Key
                    )
                )
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DuplicateVersion,
                            model.DiagnosticLocation.ToLocation(),
                            group.Key,
                            versions.Key
                        )
                    );
                }
            }
        }

        foreach (var model in sourceModels)
        {
            ModelReference? previous = null;
            if (model.SupportMigration && model.SchemaVersion is > 1 && model.Id is not null)
            {
                groups.TryGetValue(model.Id, out var candidates);
                previous = candidates?.FirstOrDefault(candidate =>
                    candidate.Version == model.SchemaVersion - 1 && candidate.IsSourceOrPublic
                );
                if (previous == null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            MissingPreviousVersion,
                            model.DiagnosticLocation.ToLocation(),
                            model.Name,
                            model.SchemaVersion,
                            model.SchemaVersion - 1,
                            model.Id
                        )
                    );
                }
            }

            if (!model.ModelIsPartial)
            {
                continue;
            }

            context.AddSource(
                model.HintName,
                SourceText.From(GenerateMetadata(model, previous), Encoding.UTF8)
            );
        }
    }

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

    private static List<ModelInfo> CollectModels(
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var result = new List<ModelInfo>();
        CollectNamespace(
            compilation.Assembly.GlobalNamespace,
            compilation,
            true,
            result,
            cancellationToken
        );
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectNamespace(
                assembly.GlobalNamespace,
                compilation,
                false,
                result,
                cancellationToken
            );
        }
        return result;
    }

    private static void CollectNamespace(
        INamespaceSymbol namespaceSymbol,
        Compilation compilation,
        bool isSource,
        List<ModelInfo> result,
        CancellationToken cancellationToken
    )
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectType(type, compilation, isSource, result, cancellationToken);
        }
        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectNamespace(child, compilation, isSource, result, cancellationToken);
        }
    }

    private static void CollectType(
        INamedTypeSymbol type,
        Compilation compilation,
        bool isSource,
        List<ModelInfo> result,
        CancellationToken cancellationToken
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
                cancellationToken.ThrowIfCancellationRequested();
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
                && attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken)
                    is AttributeSyntax syntax
            )
            {
                var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                foreach (var argument in syntax.ArgumentList?.Arguments ?? default)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = argument.NameEquals?.Name.Identifier.ValueText;
                    var constant = semanticModel.GetConstantValue(
                        argument.Expression,
                        cancellationToken
                    );
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
                    attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
                        ?? type.Locations.FirstOrDefault()
                        ?? Location.None,
                    usesLegacyVersion,
                    supportMigration
                )
            );
        }

        foreach (var nested in type.GetTypeMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectType(nested, compilation, isSource, result, cancellationToken);
        }
    }

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Length > 0
        && type.DeclaringSyntaxReferences.All(reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        );

    private static bool Implements(INamedTypeSymbol type, string interfaceName) =>
        type.AllInterfaces.Any(@interface => @interface.ToDisplayString() == interfaceName);

    private static string GenerateMetadata(SourceModelInfo model, ModelReference? previous)
    {
        var builder = new IndentedStringBuilder();
        builder.AppendLine(
            """
            // <auto-generated/>
            #nullable enable
            """
        );

        var namespaceName = model.Namespace;
        if (namespaceName != null)
        {
            builder.AppendLine($"namespace {namespaceName}");
            builder.AppendLine("{");
            builder.IncreaseIndent();
        }

        foreach (var containingType in model.ContainingTypeDeclarations)
        {
            AppendTypeStart(builder, containingType, null);
            builder.IncreaseIndent();
        }

        var implementedInterfaces = new List<string> { MetadataInterfaceName };
        if (previous is not null && model.Id is not null && model.SchemaVersion is not null)
        {
            implementedInterfaces.Add(
                $"{MigrationInterfaceName}<{previous.FullName}, {model.FullName}>"
            );
        }

        AppendTypeStart(builder, model.TypeDeclaration, implementedInterfaces);
        builder.IncreaseIndent();
        builder.AppendLine(
            $$"""
            string? global::{{MetadataInterfaceName}}.ModelId => {{model.IdLiteral}};
            int? global::{{MetadataInterfaceName}}.Version => {{model.VersionValue}};
            void global::{{MetadataInterfaceName}}.RegisterMigrations(global::Configuration.Writable.IOptionsMigrationRegistrar registrar)
            {
            """
        );
        builder.IncreaseIndent();
        if (previous != null && model.Id != null && model.SchemaVersion is not null)
        {
            builder.AppendLine(
                $$"""
                ((global::{{MetadataInterfaceName}})new {{previous.FullName}}()).RegisterMigrations(registrar);
                registrar.Register<{{previous.FullName}}, {{model.FullName}}>(Migrate, {{model.IdLiteral}}, {{previous.Version}}, {{model.SchemaVersion}});
                """
            );
        }
        builder.DecreaseIndent();
        builder.AppendLine("}");

        builder.DecreaseIndent();
        builder.AppendLine("}");
        foreach (var _ in model.ContainingTypeDeclarations)
        {
            builder.DecreaseIndent();
            builder.AppendLine("}");
        }
        if (namespaceName != null)
        {
            builder.DecreaseIndent();
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    private static void AppendTypeStart(
        IndentedStringBuilder builder,
        string typeDeclaration,
        IReadOnlyList<string>? implementedInterfaces
    )
    {
        builder.AppendLine(
            implementedInterfaces is null || implementedInterfaces.Count == 0
                ? $"{typeDeclaration}\n{{"
                : $"{typeDeclaration} : {string.Join(", ", implementedInterfaces.Select(interfaceName => $"global::{interfaceName}"))}\n{{"
        );
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

    private static EquatableArray<ReferencedModelInfo> CollectReferencedModels(
        Compilation compilation,
        CancellationToken cancellationToken
    ) =>
        new(
            CollectModels(compilation, cancellationToken)
                .Where(static model => !model.IsSource)
                .Select(static model => new ReferencedModelInfo(
                    model.Id,
                    model.Version,
                    model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    model.Symbol.Name,
                    model.Symbol.ContainingNamespace.IsGlobalNamespace
                        ? null
                        : model.Symbol.ContainingNamespace.ToDisplayString(),
                    model.Symbol.DeclaredAccessibility == Accessibility.Public
                ))
        );

    private static void ReportModelDiagnostics(
        SourceProductionContext context,
        SourceModelInfo model
    )
    {
        foreach (var diagnostic in model.Diagnostics)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    diagnostic.Id switch
                    {
                        "CWWR001" => MissingId,
                        "CWWR002" => InvalidId,
                        "CWWR003" => InvalidVersion,
                        "CWWR007" => LegacyVersioning,
                        "CWWR008" => PartialRequired,
                        "CWWR009" => UnsupportedModel,
                        _ => MissingVersion,
                    },
                    diagnostic.Location.ToLocation(),
                    diagnostic.GetArguments()
                )
            );
        }
    }

    private sealed record SourceModelInfo(
        string? Id,
        int? SchemaVersion,
        bool SupportMigration,
        bool ModelIsPartial,
        string Name,
        string MinimalName,
        string FullName,
        string? Namespace,
        string TypeDeclaration,
        EquatableArray<string> ContainingTypeDeclarations,
        string HintName,
        string IdLiteral,
        string VersionValue,
        DiagnosticLocation DiagnosticLocation,
        EquatableArray<GeneratorDiagnostic> Diagnostics
    )
    {
        public static SourceModelInfo Create(
            GeneratorAttributeSyntaxContext context,
            CancellationToken cancellationToken
        )
        {
            var type = (INamedTypeSymbol)context.TargetSymbol;
            var attribute = context.Attributes[0];
            string? id = null;
            int? version = null;
            var versionSpecified = false;
            var supportMigration = true;
            foreach (var argument in attribute.NamedArguments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (argument.Key == "Id")
                    id = argument.Value.Value as string;
                else if (argument.Key == "Version")
                {
                    versionSpecified = true;
                    version = argument.Value.Value as int?;
                }
                else if (argument.Key == "SupportMigration")
                    supportMigration = argument.Value.Value as bool? ?? true;
            }

            var usesLegacyVersion = Implements(type, LegacyInterfaceName);
            if (!versionSpecified && !usesLegacyVersion)
                version = 1;

            var location = DiagnosticLocation.Create(
                attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
                    ?? type.Locations.FirstOrDefault()
                    ?? Microsoft.CodeAnalysis.Location.None
            );
            var diagnostics = new List<GeneratorDiagnostic>();
            if (id is null)
                diagnostics.Add(new("CWWR001", location, type.Name));
            else if (string.IsNullOrWhiteSpace(id))
                diagnostics.Add(new("CWWR002", location, type.Name));
            if (versionSpecified && version is <= 0)
                diagnostics.Add(new("CWWR003", location, type.Name));
            else if (!versionSpecified)
                diagnostics.Add(new("CWWR010", location, type.Name));
            if (usesLegacyVersion)
                diagnostics.Add(new("CWWR007", location, type.Name));
            if (
                type.DeclaringSyntaxReferences.Length == 0
                || !type.DeclaringSyntaxReferences.All(reference =>
                    reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax declaration
                    && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                )
            )
                diagnostics.Add(new("CWWR008", location, type.Name));
            if (
                version is not null
                && (
                    type.TypeKind != TypeKind.Class
                    || type.IsStatic
                    || !HasAccessibleParameterlessConstructor(type, true)
                )
            )
                diagnostics.Add(new("CWWR009", location, type.Name));

            var containingTypes = new Stack<string>();
            for (
                var current = type.ContainingType;
                current is not null;
                current = current.ContainingType
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                containingTypes.Push(GetTypeDeclaration(current));
            }

            return new(
                id,
                version,
                supportMigration,
                type.DeclaringSyntaxReferences.Length > 0
                    && type.DeclaringSyntaxReferences.All(reference =>
                        reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax declaration
                        && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                    ),
                type.Name,
                GetMinimalTypeName(type),
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                type.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : type.ContainingNamespace.ToDisplayString(),
                GetTypeDeclaration(type),
                new EquatableArray<string>(containingTypes),
                GetHintName(type),
                id is null ? "null" : SymbolDisplay.FormatLiteral(id, true),
                version?.ToString() ?? "null",
                location,
                new EquatableArray<GeneratorDiagnostic>(diagnostics)
            );
        }

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

        private static string GetTypeDeclaration(INamedTypeSymbol type)
        {
            var kind = "class ";
            if (type.IsRecord)
            {
                kind = type.TypeKind == TypeKind.Struct ? "record struct " : "record class ";
            }
            else if (type.TypeKind == TypeKind.Struct)
            {
                kind = "struct ";
            }

            var modifiers = "";
            if (type.IsStatic)
            {
                modifiers = "static ";
            }
            else if (type.IsAbstract)
            {
                modifiers = "abstract ";
            }
            else if (type.IsSealed)
            {
                modifiers = "sealed ";
            }

            var typeParameters = "";
            if (type.TypeParameters.Length > 0)
            {
                typeParameters =
                    "<"
                    + string.Join(
                        ", ",
                        type.TypeParameters.Select(static parameter => parameter.Name)
                    )
                    + ">";
            }
            return GetAccessibility(type.DeclaredAccessibility)
                + modifiers
                + "partial "
                + kind
                + type.Name
                + typeParameters;
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
                name.Select(character => char.IsLetterOrDigit(character) ? character : '_')
                    .ToArray()
            );
            return sanitized + ".OptionsMetadata.g.cs";
        }
    }

    private sealed record ModelReference(
        string? Id,
        int? Version,
        string FullName,
        string Name,
        string? Namespace,
        bool IsSourceOrPublic
    );

    private sealed record ReferencedModelInfo(
        string? Id,
        int? Version,
        string FullName,
        string Name,
        string? Namespace,
        bool IsPublic
    );

    private sealed record GeneratorDiagnostic(
        string Id,
        DiagnosticLocation Location,
        string Argument1,
        string? Argument2 = null
    )
    {
        public object[] GetArguments() => Argument2 is null ? [Argument1] : [Argument1, Argument2];
    }

    private sealed record DiagnosticLocation(
        string Path,
        int Start,
        int Length,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter
    )
    {
        public static DiagnosticLocation Create(Location location)
        {
            var lineSpan = location.GetLineSpan();
            return new(
                location.SourceTree?.FilePath ?? "",
                location.SourceSpan.Start,
                location.SourceSpan.Length,
                lineSpan.StartLinePosition.Line,
                lineSpan.StartLinePosition.Character,
                lineSpan.EndLinePosition.Line,
                lineSpan.EndLinePosition.Character
            );
        }

        public Location ToLocation() =>
            Location.Create(
                Path,
                new TextSpan(Start, Length),
                new LinePositionSpan(
                    new LinePosition(StartLine, StartCharacter),
                    new LinePosition(EndLine, EndCharacter)
                )
            );
    }
}
