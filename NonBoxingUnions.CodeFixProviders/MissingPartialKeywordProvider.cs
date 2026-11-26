using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NonBoxingUnions.Shared;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace NonBoxingUnions.CodeFixProviders;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingPartialKeywordProvider))]
internal sealed class MissingPartialKeywordProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [NonBoxingUnionDiagnostics.UnionMustBePartialRule.Id];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		Diagnostic diagnostic = context.Diagnostics[0];
		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);

		StructDeclarationSyntax? structDeclaration = node.FirstAncestorOrSelf<StructDeclarationSyntax>();
		if (structDeclaration is null)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Add 'partial' modifier",
				createChangedDocument: token => AddPartialModifierAsync(context.Document, root, structDeclaration, token),
				equivalenceKey: nameof(MissingPartialKeywordProvider)),
			diagnostic);
	}

	private static Task<Document> AddPartialModifierAsync(
		Document document,
		SyntaxNode root,
		StructDeclarationSyntax structDeclaration,
		CancellationToken _)
	{
		if (structDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
			return Task.FromResult(document);

		SyntaxToken partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword);
		StructDeclarationSyntax newStruct = structDeclaration.AddModifiers(partialToken);

		SyntaxNode newRoot = root.ReplaceNode(structDeclaration, newStruct);
		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}
}