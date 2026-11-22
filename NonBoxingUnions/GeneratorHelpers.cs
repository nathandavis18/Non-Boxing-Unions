using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NonBoxingUnions;

internal static class GeneratorHelpers
{
	internal const string AttributeNamespace = "NonBoxingUnions";
	internal const string AttributeName = "NonBoxingUnionAttribute";

	private static readonly SymbolDisplayFormat Format =
		SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

	internal static void RegisterPostInitializationOutput(IncrementalGeneratorPostInitializationContext ctx)
	{
		ctx.AddEmbeddedAttributeDefinition();

		CodeBuilder cb = new($"namespace {AttributeNamespace};");

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
		cb.AppendLine($"internal sealed class {AttributeName}(params global::System.Type[] _) : global::System.Attribute;");
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
			cb.AppendLine($"internal sealed class {AttributeName}<{typeParams}> : global::System.Attribute;");
		}

		ctx.AddSource($"{AttributeName}.g.cs", SourceText.From(cb.ToString(), Encoding.UTF8));
	}

	internal static TypeModel GetTypeModelForUnion(IReadOnlyList<ITypeSymbol> types, INamedTypeSymbol typeSymbol)
	{
		HashSet<ITypeSymbol> seen = new(SymbolEqualityComparer.Default);
		List<string> duplicateTypes = [];

		foreach (ITypeSymbol type in types)
		{
			if (!seen.Add(type))
				duplicateTypes.Add(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
		}

		// Grab the location of the attribute (or struct identifier) to underline in the IDE
		Location location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;

		IEnumerable<ITypeSymbol> valueTypes = types.Where(t => t.IsValueType);
		IEnumerable<ITypeSymbol> otherTypes = types.Where(t => !t.IsValueType);

		int tagNumber = 1;
		List<Parameter> parameters = valueTypes.Select(p =>
			new Parameter(p.ToDisplayString(Format), p.IsValueType, tagNumber++)).ToList();

		parameters =
		[
			.. parameters,
			.. otherTypes.Select(t => new Parameter(t.ToDisplayString(Format), false, tagNumber++))
		];

		string? typeNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
			? null
			: $"namespace {typeSymbol.ContainingNamespace.ToDisplayString()};";

		Stack<ContainingType> containingTypes = new();
		INamedTypeSymbol? current = typeSymbol.ContainingType;
		Stack<string> nonPartialEnclosingTypes = new();
		while (current is not null)
		{
			if (!IsSymbolPartial(current))
				nonPartialEnclosingTypes.Push($"'{current.Name}'");

			containingTypes.Push(new ContainingType(current.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), current.IsValueType, current.IsRecord));
			current = current.ContainingType;
		}

		return new TypeModel(
			typeSymbol.Name,
			typeNamespace,
			containingTypes.ToEquatableArray(),
			duplicateTypes.ToEquatableArray(),
			new EquatableLocation(location),
			parameters.ToEquatableArray(),
			tagNumber,
			IsSymbolPartial(typeSymbol),
			nonPartialEnclosingTypes.ToEquatableArray());
	}

	private static bool IsSymbolPartial(INamedTypeSymbol symbol) =>
		symbol.DeclaringSyntaxReferences
			.Select(s => s.GetSyntax())
			.OfType<BaseTypeDeclarationSyntax>()
			.Any(s => s.Modifiers.Any(SyntaxKind.PartialKeyword));
}