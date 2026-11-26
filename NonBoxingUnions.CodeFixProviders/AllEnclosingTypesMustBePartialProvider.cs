using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NonBoxingUnions.Shared;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NonBoxingUnions.CodeFixProviders;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AllEnclosingTypesMustBePartialProvider))]
internal class AllEnclosingTypesMustBePartialProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [NonBoxingUnionDiagnostics.AllEnclosingTypesMustBePartialRule.Id];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		Diagnostic diagnostic = context.Diagnostics[0];
		List<SyntaxNode> nodes = [];
		foreach (Location location in diagnostic.AdditionalLocations)
		{
			if (root.FindNode(location.SourceSpan) is { } node)
				nodes.Add(node);
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				title: $"Add 'partial' modifier to {nodes.Count} place{(nodes.Count > 1 ? "s" : null)}",
				createChangedDocument: token => AddPartialModifierAsync(context.Document, root, nodes, token),
				equivalenceKey: nameof(AllEnclosingTypesMustBePartialProvider)),
			diagnostic);
	}

	private static Task<Document> AddPartialModifierAsync(
		Document document,
		SyntaxNode root,
		IReadOnlyList<SyntaxNode> nodes,
		CancellationToken _)
	{
		SyntaxToken partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword);
		root = root.ReplaceNodes(
			nodes.Distinct(),
			(_, newNode) =>
			{
				if (newNode is TypeDeclarationSyntax typeDeclaration && !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
					return typeDeclaration.AddModifiers(partialToken);
				return newNode;
			});

		return Task.FromResult(document.WithSyntaxRoot(root));
	}
}
