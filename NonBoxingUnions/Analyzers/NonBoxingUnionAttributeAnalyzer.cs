using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NonBoxingUnions.Shared;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NonBoxingUnions.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NonBoxingUnionAttributeAnalyzer : DiagnosticAnalyzer
{
	private static readonly SymbolDisplayFormat Format = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.None);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
	[
		NonBoxingUnionDiagnostics.DuplicateUnionTypeRule, 
		NonBoxingUnionDiagnostics.UnionMustDeclareTypeRule,
		NonBoxingUnionDiagnostics.UnionMustBePartialRule,
		NonBoxingUnionDiagnostics.AllEnclosingTypesMustBePartialRule,
		NonBoxingUnionDiagnostics.RedundantDefaultRule,
	];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(AnalyzeAttributeArgument, SyntaxKind.AttributeArgument);
		context.RegisterSymbolAction(AnalyzeAttributeForErrors, SymbolKind.NamedType);
	}

	private static void AnalyzeAttributeArgument(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not AttributeArgumentSyntax { NameEquals.Name.Identifier.ValueText: Constants.DisableStructPacking } argument)
			return;

		if (argument.Parent?.Parent is not AttributeSyntax attributeSyntax)
			return;

		if (context.SemanticModel.GetTypeInfo(attributeSyntax, context.CancellationToken).Type is not ITypeSymbol namedSymbol)
			return;

		if (namedSymbol.ToDisplayString(Format) != $"{Constants.AttributeNamespace}.{Constants.AttributeName}")
			return;

		Optional<object?> constantValue = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

		if (constantValue is { HasValue: true, Value: false })
		{
			context.ReportDiagnostic(Diagnostic.Create(
				NonBoxingUnionDiagnostics.RedundantDefaultRule,
				argument.GetLocation(),
				Constants.DisableStructPacking,
				"false"));
		}
	}

	private static void AnalyzeAttributeForErrors(SymbolAnalysisContext context)
	{
		if (context.Symbol is not INamedTypeSymbol type)
			return;

		AttributeData? attribute = null;
		foreach (AttributeData tmp in type.GetAttributes())
		{
			if (tmp.AttributeClass?.ContainingNamespace?.Name == Constants.AttributeNamespace && tmp.AttributeClass?.Name == Constants.AttributeName)
			{
				attribute = tmp;
				break;
			}
		}

		if (attribute is null)
			return;

		TypeModel model = Helpers.GetTypeModelForUnion(attribute, type, false);
		if (ModelVerification.IsModelValid(model, out IReadOnlyList<DiagnosticData> diagnostics))
			return;

		foreach (DiagnosticData diagnostic in diagnostics)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				diagnostic.Descriptor,
				type.DeclaringSyntaxReferences.First().GetSyntax().GetLocation(),
				diagnostic.AdditionalLocations,
				diagnostic.MessageFormatArguments));
		}
	}
}
