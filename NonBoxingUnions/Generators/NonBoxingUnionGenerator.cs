using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NonBoxingUnions.Shared;
using System.Linq;
using System.Text;

namespace NonBoxingUnions.Generators;

[Generator]
internal sealed class NonBoxingUnionGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(GeneratorHelpers.RegisterPostInitializationOutput);

		RegisterSourceOutput(context, context.SyntaxProvider.ForAttributeWithMetadataName(
				$"{Constants.AttributeNamespace}.{Constants.AttributeName}",
				predicate: static (node, _) => node is StructDeclarationSyntax,
				transform: static (ctx, _) =>
				{
					AttributeData attribute = ctx.Attributes[0];
					INamedTypeSymbol typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;

					bool disableStructPacking = (bool?)attribute.NamedArguments.FirstOrDefault(n => n.Key == Constants.DisableStructPacking).Value.Value ?? false;
					return Helpers.GetTypeModelForUnion(attribute, typeSymbol, disableStructPacking);
				}));

		for (int generic = 1; generic <= 16; ++generic)
		{
			RegisterSourceOutput(context, context.SyntaxProvider.ForAttributeWithMetadataName(
				$"{Constants.AttributeNamespace}.{Constants.AttributeName}`{generic}",
				predicate: static (node, _) => node is StructDeclarationSyntax,
				transform: static (ctx, _) =>
				{
					AttributeData attribute = ctx.Attributes[0];
					INamedTypeSymbol typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;

					bool disableStructPacking = (bool?)attribute.NamedArguments.FirstOrDefault(n => n.Key == Constants.DisableStructPacking).Value.Value ?? false;
					return Helpers.GetTypeModelForUnion(attribute, typeSymbol, disableStructPacking);
				}));
		}
	}

	private static void RegisterSourceOutput(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TypeModel> declarations)
	{
		context.RegisterSourceOutput(declarations, static (spc, source) =>
		{
			if (!ModelVerification.IsModelValid(source, out _))
				return;

			CodeBuilder cb = new(source.Namespace);
			foreach (ContainingType type in source.ContainingTypes)
			{
				string parentKind = type.IsRecord ? type.IsValueType ? "record struct" : "record"
					: type.IsValueType ? "struct" : "class";

				cb.AppendLine($"partial {parentKind} {type.FullName}");
				cb.AppendLine("{");
				cb.Indent();
			}

			cb.AppendLine("#pragma warning disable CS0618");
			cb.AppendUnionAttribute();
			cb.AppendGeneratedCodeAttribute();
			using (cb.WriteBlock($"readonly partial struct {source.FullName} : global::System.Runtime.CompilerServices.IUnion"))
			{
				UnionMemberHelpers.AppendFieldDeclarations(cb, source);
				cb.AppendLine();
				UnionMemberHelpers.AppendConstructors(cb, source);
				cb.AppendLine();
				UnionMemberHelpers.AppendHasValue(cb);
				cb.AppendLine();
				UnionMemberHelpers.AppendValueProperty(cb, source);
				cb.AppendLine();
				UnionMemberHelpers.AppendTryGetValues(cb, source.Parameters.AsImmutableArray());
				cb.AppendLine();
				UnionMemberHelpers.AppendEnumDeclaration(cb, source.Parameters.AsImmutableArray());
			}
			cb.AppendLine("#pragma warning restore CS0618");

			foreach (ContainingType _ in source.ContainingTypes)
			{
				cb.UnIndent();
				cb.AppendLine("}");
			}

			if (source.Parameters.Any(t => t.Type is FieldType.PackedStruct))
			{
				cb.AppendLine();
				UnionMemberHelpers.AppendEnclosedStruct(cb, source);
			}

			spc.AddSource($"{GetFileHintName(source)}", SourceText.From(cb.ToString(), Encoding.UTF8));
		});
	}

	private static string GetFileHintName(TypeModel source) =>
		$"{GeneratorHelpers.GetModelFullyQualifiedName(source)}.g.cs";
}