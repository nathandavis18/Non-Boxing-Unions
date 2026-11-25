using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace NonBoxingUnions.Shared;

internal static class Helpers
{
	private static readonly SymbolDisplayFormat Format =
		SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

	internal static TypeModel GetTypeModelForUnion(AttributeData attribute, INamedTypeSymbol typeSymbol, bool disableStructPacking)
	{
		(ITypeSymbol, Location)[] types =
		[
			.. GetTypesFromAttributeData(attribute),
			.. typeSymbol.TypeArguments.Select(t => (t, t.Locations.FirstOrDefault() ?? Location.None))
		];
		HashSet<ITypeSymbol> seen = new(SymbolEqualityComparer.Default);
		List<DuplicateType> duplicateTypes = [];

		foreach ((ITypeSymbol type, Location location) in types)
		{
			if (!seen.Add(type))
				duplicateTypes.Add(new DuplicateType(
					type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
					new EquatableLocation(location)));
		}

		string? typeNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
			? null
			: typeSymbol.ContainingNamespace.ToDisplayString();

		Stack<ContainingType> containingTypes = new();
		INamedTypeSymbol? current = typeSymbol.ContainingType;
		while (current is not null)
		{
			containingTypes.Push(new ContainingType(
				current.Name,
				current.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
				current.IsValueType,
				current.IsRecord,
				current.Arity,
				IsSymbolPartial(current),
				new EquatableLocation(current.Locations.FirstOrDefault() ?? Location.None)));

			current = current.ContainingType;
		}

		return new TypeModel(
			typeSymbol.Name,
			typeNamespace,
			typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
			containingTypes.ToEquatableArray(),
			duplicateTypes.ToEquatableArray(),
			new EquatableLocation(typeSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation()),
			types.Select(t => new Parameter(t.Item1.ToDisplayString(Format), GetFieldType(t.Item1, disableStructPacking))).ToEquatableArray(),
			IsSymbolPartial(typeSymbol));
	}

	private static IEnumerable<(ITypeSymbol, Location)> GetTypesFromAttributeData(AttributeData attributeData)
	{
		AttributeSyntax attributeSyntax = (AttributeSyntax)attributeData.ApplicationSyntaxReference!.GetSyntax();
		if (attributeData.ConstructorArguments.Length > 0)
		{
			return attributeData.ConstructorArguments[0].Values.Where(kvp => kvp.Value is ITypeSymbol)
				.Zip(attributeSyntax.ArgumentList!.Arguments.Where(a => a.Expression is TypeOfExpressionSyntax), (ctor, @typeof) => ((ITypeSymbol)ctor.Value!, @typeof.GetLocation()));
		}

		GenericNameSyntax genericName = attributeSyntax.Name.DescendantNodesAndSelf().OfType<GenericNameSyntax>().First();
		return attributeData.AttributeClass!.TypeArguments
			.Zip(genericName.TypeArgumentList.Arguments, (argument, generic) => (argument, generic.GetLocation()));
	}

	private static bool IsSymbolPartial(INamedTypeSymbol symbol) =>
		symbol.DeclaringSyntaxReferences
			.Select(s => s.GetSyntax())
			.OfType<BaseTypeDeclarationSyntax>()
			.Any(s => s.Modifiers.Any(SyntaxKind.PartialKeyword));

	private static FieldType GetFieldType(ITypeSymbol symbol, bool disableStructPacking)
	{
		if (symbol is ITypeParameterSymbol genericType)
		{
			if (genericType.HasReferenceTypeConstraint)
				return FieldType.ReferenceType;
			if (genericType.HasValueTypeConstraint)
				return FieldType.NonPackedStruct;
			return FieldType.GenericType;
		}

		if (!symbol.IsValueType)
			return FieldType.ReferenceType;
		if (disableStructPacking)
			return FieldType.NonPackedStruct;
		return symbol.IsUnmanagedType ? FieldType.PackedStruct : FieldType.NonPackedStruct;
	}
}
