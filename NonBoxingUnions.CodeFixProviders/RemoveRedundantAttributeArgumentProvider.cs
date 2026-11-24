using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace NonBoxingUnions.CodeFixProviders;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveRedundantAttributeArgumentProvider))]
internal sealed class RemoveRedundantAttributeArgumentProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [NonBoxingUnionDiagnostics.RedundantDefaultRule.Id];

	public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		Diagnostic diagnostic = context.Diagnostics[0];
		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);

		AttributeArgumentSyntax? argument = node.FirstAncestorOrSelf<AttributeArgumentSyntax>();
		if (argument is null)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Remove redundant attribute argument",
				createChangedDocument: token => RemoveArgumentAsync(context.Document, root, argument, token),
				equivalenceKey: nameof(RemoveRedundantAttributeArgumentProvider)),
			diagnostic);
	}

	private static Task<Document> RemoveArgumentAsync(
		Document document,
		SyntaxNode root,
		AttributeArgumentSyntax argument,
		CancellationToken cancellationToken)
	{
		if (argument.Parent is not AttributeArgumentListSyntax argumentList)
			return Task.FromResult(document);

		SyntaxNode newRoot;

		if (argumentList.Arguments.Count == 1 && argumentList.Parent is AttributeSyntax attributeSyntax)
		{
			AttributeSyntax newAttribute = attributeSyntax.WithArgumentList(null);
			newRoot = root.ReplaceNode(attributeSyntax, newAttribute);
		}
		else
		{
			SeparatedSyntaxList<AttributeArgumentSyntax> newArguments = argumentList.Arguments.Remove(argument);
			AttributeArgumentListSyntax newArgumentList = argumentList.WithArguments(newArguments);
			newRoot = root.ReplaceNode(argumentList, newArgumentList);
		}

		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}
}
