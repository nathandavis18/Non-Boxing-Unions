using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit;
}

namespace NonBoxingUnion
{
	[Generator]
	public class NonBoxingUnionGenerator : IIncrementalGenerator
	{
		private static readonly SymbolDisplayFormat Format =
			SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle
				.Omitted);

		private static readonly DiagnosticDescriptor DuplicateUnionTypeRule = new(
			id: "NBU001",
			title: "Duplicate type in union",
			messageFormat: "Union '{0}' cannot contain duplicate type '{1}'",
			category: "NonBoxingUnion",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(ctx =>
			{
				var sb = new StringBuilder();
				sb.AppendLine("namespace NonBoxingUnions;");

				sb.AppendLine($$"""
								{{'\n'}}
								/// <summary>
								/// Represents a union with the types specified in this attribute's ctor.
								/// Can only be applied to structs, and will mark them as <see langword="readonly" />
								/// </summary>
								""");

				sb.AppendLine("[System.AttributeUsage(System.AttributeTargets.Struct, AllowMultiple = false)]");
				sb.AppendLine(
					"public sealed class NonBoxingUnionAttribute(params System.Type[] _) : System.Attribute;");

				for (var generic = 1; generic <= 16; generic++)
				{
					var typeParams = string.Join(", ", Enumerable.Range(1, generic).Select(i => $"T{i}"));
					sb.AppendLine($$"""
									{{'\n'}}
									/// <summary>
									/// Represents a union with the generic types specified on this attribute.
									/// Can only be applied to structs, and will mark them as <see langword="readonly" />
									/// </summary>
									""");
					sb.AppendLine("[System.AttributeUsage(System.AttributeTargets.Struct, AllowMultiple = false)]");
					sb.AppendLine($"public sealed class NonBoxingUnionAttribute<{typeParams}> : System.Attribute;");
				}

				var formattedCode = CSharpSyntaxTree.ParseText(sb.ToString()).GetRoot().NormalizeWhitespace()
					.ToFullString();
				ctx.AddSource("NonBoxingUnionAttributes.g.cs", SourceText.From(formattedCode, Encoding.UTF8));
			});

			var paramsDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
				"NonBoxingUnions.NonBoxingUnionAttribute",
				predicate: static (node, _) => node is StructDeclarationSyntax { Arity: 0 },
				transform: static (ctx, _) =>
				{
					var typeArgs = ctx.Attributes[0].ConstructorArguments[0].Values
						.Select(c => c.Value as ITypeSymbol)
						.Where(t => t is not null)
						.ToArray();

					var typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;
					return GetTypeModelForUnion(typeArgs, typeSymbol);
				});

			RegisterSourceOutput(paramsDeclarations);

			for (var generic = 1; generic <= 16; ++generic)
			{
				RegisterSourceOutput(context.SyntaxProvider.ForAttributeWithMetadataName(
					$"NonBoxingUnions.NonBoxingUnionAttribute`{generic}",
					predicate: static (node, _) => node is StructDeclarationSyntax { Arity: 0 },
					transform: static (ctx, _) =>
					{
						var typeArgs = ctx.Attributes[0].AttributeClass!.TypeArguments;
						var typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;
						return GetTypeModelForUnion(typeArgs, typeSymbol);
					}));
			}

			return;

			void RegisterSourceOutput(IncrementalValuesProvider<TypeModel> declarations)
			{
				context.RegisterSourceOutput(declarations, static (spc, source) =>
				{
					if (source.DuplicateTypes.Count > 0)
					{
						foreach (var duplicate in source.DuplicateTypes)
						{
							spc.ReportDiagnostic(Diagnostic.Create(
								DuplicateUnionTypeRule,
								source.Location,
								source.Name,
								duplicate));
						}

						return;
					}

					var sb = new StringBuilder();
					sb.AppendLine("#nullable enable");
					sb.AppendLine(source.Namespace);
					foreach (var type in source.ContainingTypes)
					{
						var parentKind = type.IsRecord ? type.IsValueType ? "record struct" : "record"
							: type.IsValueType ? "struct" : "class";
						var parentName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

						sb.AppendLine($"partial {parentKind} {parentName}");
						sb.AppendLine("{");
					}

					sb.AppendLine("[global::System.Runtime.CompilerServices.UnionAttribute]");
					sb.AppendLine(
						$"readonly partial struct {source.Name} : global::System.Runtime.CompilerServices.IUnion");
					sb.AppendLine("{");
					sb.AppendLine(GetValueTypeDeclarations(source.Parameters));
					sb.AppendLine(GetUnionConstructors(source.Parameters, source.Name));
					sb.AppendLine($"public bool HasValue => _tag > 0 && _tag < {source.OnePastHighestTag};");
					sb.AppendLine(GetValueProperty(source));
					sb.AppendLine(GetTryGetValues(source.Parameters));
					sb.AppendLine("}");

					foreach (var _ in source.ContainingTypes)
						sb.AppendLine("}");

					var formattedCode = CSharpSyntaxTree.ParseText(sb.ToString()).GetRoot().NormalizeWhitespace()
						.ToFullString();

					spc.AddSource($"{source.Name}.g.cs", SourceText.From(formattedCode, Encoding.UTF8));
				});
			}
		}

		private static TypeModel GetTypeModelForUnion(IReadOnlyList<ITypeSymbol> types, INamedTypeSymbol typeSymbol)
		{
			// Check for duplicate types using SymbolEqualityComparer
			var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
			var duplicateTypes = new List<string>();

			foreach (var type in types)
			{
				if (!seen.Add(type))
					duplicateTypes.Add(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
			}

			// Grab the location of the attribute (or struct identifier) to underline in the IDE
			var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;

			var valueTypes = types.Where(t => t.IsValueType);
			var otherTypes = types.Where(t => !t.IsValueType);

			var tagNumber = 1;
			var parameters = valueTypes.Select(p =>
				new Parameter(p.ToDisplayString(Format), p.IsValueType, tagNumber++)).ToList();

			parameters =
			[
				.. parameters,
				.. otherTypes.Select(t => new Parameter(t.ToDisplayString(Format), false, tagNumber++))
			];

			var typeNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
				? null
				: $"namespace {typeSymbol.ContainingNamespace.ToDisplayString()};";

			var containingTypes = new Stack<INamedTypeSymbol>();
			var current = typeSymbol.ContainingType;
			while (current is not null)
			{
				containingTypes.Push(current);
				current = current.ContainingType;
			}

			return new TypeModel(
				typeSymbol.Name,
				typeNamespace,
				containingTypes,
				duplicateTypes,
				location,
				parameters.ToImmutableArray(),
				tagNumber);
		}

		private static string GetValueTypeDeclarations(ImmutableArray<Parameter> types)
		{
			var stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("private readonly byte _tag;");
			foreach (var type in types.Where(t => t.IsValueType))
				stringBuilder.AppendLine($"private readonly {type.FullyQualifiedName} _field{type.TagNumber};");

			if (types.Any(t => !t.IsValueType))
				stringBuilder.AppendLine("private readonly object? _refValue;");

			return stringBuilder.ToString();
		}

		private static string GetUnionConstructors(ImmutableArray<Parameter> types, string unionName)
		{
			var stringBuilder = new StringBuilder();
			foreach (var type in types)
			{
				stringBuilder.Append($$"""
									   public {{unionName}}({{type.FullyQualifiedName}} value)
									   {
									       _tag = {{type.TagNumber}};
									       {{(type.IsValueType ? $"_field{type.TagNumber}" : "_refValue")}} = value;
									   }

									   """);
			}

			return stringBuilder.ToString();
		}

		private static string GetValueProperty(TypeModel model)
		{
			var stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("public object? Value => _tag switch");
			stringBuilder.AppendLine("{");

			var currentTag = 1;
			foreach (var type in model.Parameters.Where(t => t.IsValueType))
			{
				stringBuilder.AppendLine($"{type.TagNumber} => _field{type.TagNumber},");
				currentTag = type.TagNumber + 1;
			}

			if (currentTag < model.OnePastHighestTag)
			{
				for (; currentTag < model.OnePastHighestTag - 1; ++currentTag)
					stringBuilder.Append($"{currentTag} or ");
				stringBuilder.AppendLine($"{currentTag} => _refValue,");
			}

			stringBuilder.AppendLine("_ => null");
			stringBuilder.Append("};");

			return stringBuilder.ToString();
		}

		private static string GetTryGetValues(ImmutableArray<Parameter> types)
		{
			var stringBuilder = new StringBuilder();
			foreach (var type in types)
			{
				stringBuilder.Append($$"""
									   public bool TryGetValue({{(type.IsValueType ? null : "[global::System.Diagnostics.CodeAnalysis.NotNullWhenAttribute(true)]")}} out {{type.FullyQualifiedName}}{{(type.IsValueType ? null : "?")}} value)
									   {
									       if (_tag == {{type.TagNumber}})
									       {
									           value = {{(type.IsValueType ? $"_field{type.TagNumber}" : $"({type.FullyQualifiedName})_refValue!")}};
									           return true;
									       }
									       
									       value = default;
									       return false;
									   }

									   """);
			}

			return stringBuilder.ToString();
		}

		private readonly record struct Parameter(string FullyQualifiedName, bool IsValueType, int TagNumber);

		private readonly record struct TypeModel(
			string Name,
			string? Namespace,
			Stack<INamedTypeSymbol> ContainingTypes,
			List<string> DuplicateTypes,
			Location Location,
			ImmutableArray<Parameter> Parameters,
			int OnePastHighestTag);
	}
}