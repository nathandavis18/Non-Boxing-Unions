using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NonBoxingUnions.Generators;

internal static class GeneratorHelpers
{
	internal const string AttributeNamespace = "NonBoxingUnions";
	internal const string AttributeName = "NonBoxingUnionAttribute";
	internal const string DisableStructPacking = "DisableStructPacking";

	private static readonly SymbolDisplayFormat Format =
		SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

	internal static void RegisterPostInitializationOutput(IncrementalGeneratorPostInitializationContext ctx)
	{
		ctx.AddEmbeddedAttributeDefinition();

		CodeBuilder cb = new(AttributeNamespace);

		cb.AppendLine("""
					  /// <summary>
					  /// Represents a union with the types specified in this attribute's ctor.
					  /// Can only be applied to structs, and will mark them as <see langword="readonly" />
					  /// </summary>
					  """);

		// Disable the "Parameter '_' is unread" warning. I know it is unread, but I can read the types from the ctor data.
		cb.AppendLine("#pragma warning disable CS9113");
		cb.AppendEmbeddedAttribute();
		cb.AppendGeneratedCodeAttribute();
		cb.AppendAttributeUsageAttribute();
		cb.AppendLine(
			$$"""
			internal sealed class {{AttributeName}}(params global::System.Type[] _) : global::System.Attribute
			{
			    /// <summary>
			    /// When <see langword="true"/>, prevents this union from packing unmanaged types.
			    /// Defaults to <see langword="false"/>.
			    /// </summary>
			    /// <remarks>
			    /// Disabling may improve speed for small unions at the cost of an increase in memory usage.
			    /// Consider benchmarking performance before disabling.
			    /// </remarks>
			    [global::System.ComponentModel.DefaultValue(false)]
			    public bool {{DisableStructPacking}} { get; init; } = false;
			}
			""");
			
		cb.AppendLine("#pragma warning restore CS9113");

		for (int generic = 1; generic <= 16; generic++)
		{
			cb.AppendLine();
			string typeParams = string.Join(", ", Enumerable.Range(1, generic).Select(i => $"T{i}"));
			cb.AppendLine("""
						  /// <summary>
						  /// Represents a union with the generic types specified on this attribute.
						  /// Can only be applied to structs, and will mark them as <see langword="readonly" />
						  /// </summary>
						  """);
			cb.AppendEmbeddedAttribute();
			cb.AppendGeneratedCodeAttribute();
			cb.AppendAttributeUsageAttribute();
			cb.AppendLine(
				$$"""
				internal sealed class {{AttributeName}}<{{typeParams}}> : global::System.Attribute
				{
				    /// <summary>
				    /// When <see langword="true"/>, prevents this union from packing unmanaged types.
				    /// Defaults to <see langword="false"/>.
				    /// </summary>
				    /// <remarks>
				    /// Disabling may improve speed for small unions at the cost of an increase in memory usage.
				    /// Consider benchmarking performance before disabling.
				    /// </remarks>
				    [global::System.ComponentModel.DefaultValue(false)]
				    public bool {{DisableStructPacking}} { get; init; } = false;
				}
				""");
		}

		ctx.AddSource($"{AttributeName}.g.cs", SourceText.From(cb.ToString(), Encoding.UTF8));
	}

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
			new EquatableLocation(typeSymbol.Locations.FirstOrDefault() ?? Location.None),
			types.Select(t => new Parameter(t.Item1.ToDisplayString(Format), GetFieldType(t.Item1, disableStructPacking))).ToEquatableArray(),
			IsSymbolPartial(typeSymbol));
	}

	internal static string GetModelFullyQualifiedName(TypeModel source)
	{
		StringBuilder sb = new();

		if (!string.IsNullOrEmpty(source.Namespace))
			sb.Append($"{source.Namespace}.");

		foreach (ContainingType type in source.ContainingTypes)
			sb.Append($"{type.FullName.Replace('<', '{').Replace('>', '}')}.");

		sb.Append(source.FullName.Replace('<', '{').Replace('>', '}'));

		return sb.ToString();
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