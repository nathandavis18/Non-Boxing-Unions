using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using NonBoxingUnions;
using NonBoxingUnions.CodeFixProviders;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DuplicateUnionTypeProvider))]
public sealed class DuplicateUnionTypeProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [NonBoxingUnionDiagnostics.DuplicateUnionTypeRule.Id];

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
				title: $"Remove {nodes.Count} duplicate type{(nodes.Count > 1 ? "s" : null)}",
				createChangedDocument: token => RemoveDuplicateTypeAsync(context.Document, root, nodes, token),
				equivalenceKey: nameof(DuplicateUnionTypeProvider)),
			diagnostic);
	}

	private static Task<Document> RemoveDuplicateTypeAsync(
		Document document,
		SyntaxNode root,
		IReadOnlyList<SyntaxNode> targetNodes,
		CancellationToken cancellationToken)
	{
		root = root.RemoveNodes(targetNodes, SyntaxRemoveOptions.KeepNoTrivia)!;

		return Task.FromResult(document.WithSyntaxRoot(root));
	}
}