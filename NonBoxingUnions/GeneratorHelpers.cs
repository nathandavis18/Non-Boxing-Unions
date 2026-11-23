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

		string? typeNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
			? null
			: typeSymbol.ContainingNamespace.ToDisplayString();

		Stack<ContainingType> containingTypes = new();
		INamedTypeSymbol? current = typeSymbol.ContainingType;
		Stack<string> nonPartialEnclosingTypes = new();
		while (current is not null)
		{
			if (!IsSymbolPartial(current))
				nonPartialEnclosingTypes.Push($"'{current.Name}'");

			containingTypes.Push(new ContainingType(current.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), current.IsValueType, current.IsRecord, current.Arity));
			current = current.ContainingType;
		}

		return new TypeModel(
			typeSymbol.Name,
			typeNamespace,
			typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
			containingTypes.ToEquatableArray(),
			duplicateTypes.ToEquatableArray(),
			new EquatableLocation(location),
			types.Select(t => new Parameter(t.ToDisplayString(Format), GetFieldType(t))).ToEquatableArray(),
			IsSymbolPartial(typeSymbol),
			nonPartialEnclosingTypes.ToEquatableArray());
	}

	internal static string GetModelFullyQualifiedName(TypeModel source)
	{
		StringBuilder sb = new();

		if (!string.IsNullOrEmpty(source.Namespace))
			sb.Append($"{source.Namespace}.");

		foreach (ContainingType type in source.ContainingTypes)
			sb.Append($"{type.Name.Replace('<', '{').Replace('>', '}')}.");

		sb.Append(source.FullName.Replace('<', '{').Replace('>', '}'));

		return sb.ToString();
	}

	private static bool IsSymbolPartial(INamedTypeSymbol symbol) =>
		symbol.DeclaringSyntaxReferences
			.Select(s => s.GetSyntax())
			.OfType<BaseTypeDeclarationSyntax>()
			.Any(s => s.Modifiers.Any(SyntaxKind.PartialKeyword));

	private static FieldType GetFieldType(ITypeSymbol symbol)
	{
		if (!symbol.IsValueType)
			return FieldType.ReferenceType;
		return symbol.IsUnmanagedType ? FieldType.PackableStruct : FieldType.NonPackableStruct;
	}
}