using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Configuration.Writable.Generator;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MigrationCodeFixProvider)), Shared]
public sealed class MigrationCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "CWWR011";

    public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var root = await context
            .Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        var declaration = root
            ?.FindToken(diagnostic.Location.SourceSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        if (
            declaration is null
            || !diagnostic.Properties.TryGetValue("CurrentTypeName", out var currentTypeName)
            || !diagnostic.Properties.TryGetValue("PreviousTypeName", out var previousTypeName)
            || !diagnostic.Properties.TryGetValue("CodeFixAvailable", out var codeFixAvailable)
            || currentTypeName is null
            || previousTypeName is null
            || !bool.TryParse(codeFixAvailable, out var canAddMigration)
            || !canAddMigration
        )
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Implement options migration",
                cancellationToken =>
                    AddMigrationAsync(
                        context.Document,
                        declaration,
                        previousTypeName,
                        diagnostic.Properties.TryGetValue("PreviousNamespace", out var value)
                            ? value
                            : null,
                        cancellationToken
                    ),
                nameof(MigrationCodeFixProvider)
            ),
            diagnostic
        );
    }

    private static async Task<Document> AddMigrationAsync(
        Document document,
        TypeDeclarationSyntax declaration,
        string previousTypeName,
        string? previousNamespace,
        CancellationToken cancellationToken
    )
    {
        var currentTypeName =
            declaration.Identifier.ValueText + declaration.TypeParameterList?.ToString();
        previousTypeName = GetShortTypeName(previousTypeName, previousNamespace);
        var method = SyntaxFactory
            .MethodDeclaration(SyntaxFactory.ParseTypeName(currentTypeName), "Migrate")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword)
            )
            .AddParameterListParameters(
                SyntaxFactory
                    .Parameter(SyntaxFactory.Identifier("source"))
                    .WithType(SyntaxFactory.ParseTypeName(previousTypeName))
            )
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory
                        .ThrowStatement(
                            SyntaxFactory
                                .ObjectCreationExpression(
                                    SyntaxFactory.ParseTypeName("NotImplementedException")
                                )
                                .WithArgumentList(SyntaxFactory.ArgumentList())
                        )
                        .WithLeadingTrivia(
                            SyntaxFactory.Comment("// TODO: Implement migration."),
                            SyntaxFactory.ElasticCarriageReturnLineFeed
                        )
                )
            )
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newDeclaration = declaration.AddMembers(method);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNode(declaration, newDeclaration);
        if (
            !string.IsNullOrEmpty(previousNamespace)
            && declaration.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>()?.Name.ToString()
                != previousNamespace
            && newRoot is CompilationUnitSyntax compilationUnit
            && !compilationUnit.Usings.Any(usingDirective =>
                usingDirective.Name?.ToString() == previousNamespace
            )
        )
        {
            newRoot = compilationUnit.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(previousNamespace!))
            );
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static string GetShortTypeName(string typeName, string? namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return typeName.StartsWith("global::") ? typeName[8..] : typeName;
        }

        var globalPrefix = "global::" + namespaceName + ".";
        if (typeName.StartsWith(globalPrefix))
        {
            return typeName[globalPrefix.Length..];
        }

        var namespacePrefix = namespaceName + ".";
        return typeName.StartsWith(namespacePrefix)
            ? typeName[namespacePrefix.Length..]
            : typeName;
    }
}
