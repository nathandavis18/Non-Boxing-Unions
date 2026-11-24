using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NonBoxingUnions.Generators;
using System.Collections.Immutable;

namespace NonBoxingUnions.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NonBoxingUnionAttributeAnalyzer : DiagnosticAnalyzer
{
	private static readonly SymbolDisplayFormat Format = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.None);
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [NonBoxingUnionDiagnostics.RedundantDefaultRule];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(AnalyzeAttributeArgument, SyntaxKind.AttributeArgument);
	}

	private static void AnalyzeAttributeArgument(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not AttributeArgumentSyntax { NameEquals.Name.Identifier.ValueText: GeneratorHelpers.DisableStructPacking } argument)
			return;

		if (argument.Parent?.Parent is not AttributeSyntax attributeSyntax)
			return;

		if (context.SemanticModel.GetTypeInfo(attributeSyntax, context.CancellationToken).Type is not ITypeSymbol namedSymbol)
			return;

		if (namedSymbol.ToDisplayString(Format) != $"{GeneratorHelpers.AttributeNamespace}.{GeneratorHelpers.AttributeName}")
			return;

		Optional<object?> constantValue = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

		if (constantValue is { HasValue: true, Value: false })
		{
			context.ReportDiagnostic(Diagnostic.Create(
				NonBoxingUnionDiagnostics.RedundantDefaultRule,
				argument.GetLocation(),
				GeneratorHelpers.DisableStructPacking,
				"false"));
		}
	}
}
