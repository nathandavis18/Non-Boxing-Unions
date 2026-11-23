using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit;
}

namespace NonBoxingUnions
{
	[Generator]
	internal sealed class NonBoxingUnionGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			context.RegisterPostInitializationOutput(GeneratorHelpers.RegisterPostInitializationOutput);

			RegisterSourceOutput(context, context.SyntaxProvider.ForAttributeWithMetadataName(
					$"{GeneratorHelpers.AttributeNamespace}.{GeneratorHelpers.AttributeName}",
					predicate: static (node, _) => node is StructDeclarationSyntax { Arity: 0 },
					transform: static (ctx, _) =>
					{
						ITypeSymbol[] typeArgs =
						[
							.. ctx.Attributes[0].ConstructorArguments[0].Values
								.Select(c => c.Value as ITypeSymbol)
								.Where(t => t is not null)!
						];

						INamedTypeSymbol typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;
						return GeneratorHelpers.GetTypeModelForUnion(typeArgs, typeSymbol);
					}));

			for (int generic = 1; generic <= 16; ++generic)
			{
				RegisterSourceOutput(context, context.SyntaxProvider.ForAttributeWithMetadataName(
					$"{GeneratorHelpers.AttributeNamespace}.{GeneratorHelpers.AttributeName}`{generic}",
					predicate: static (node, _) => node is StructDeclarationSyntax { Arity: 0 },
					transform: static (ctx, _) =>
					{
						ImmutableArray<ITypeSymbol> typeArgs = ctx.Attributes[0].AttributeClass!.TypeArguments;
						INamedTypeSymbol typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;
						return GeneratorHelpers.GetTypeModelForUnion(typeArgs, typeSymbol);
					}));
			}
		}

		private static void RegisterSourceOutput(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TypeModel> declarations)
		{
			context.RegisterSourceOutput(declarations, static (spc, source) =>
			{
				if (OutputErrorsIfExist(spc, source))
					return;

				CodeBuilder cb = new(source.Namespace);
				foreach (ContainingType type in source.ContainingTypes)
				{
					string parentKind = type.IsRecord ? type.IsValueType ? "record struct" : "record"
						: type.IsValueType ? "struct" : "class";

					cb.AppendLine($"partial {parentKind} {type.Name}");
					cb.AppendLine("{");
					cb.Indent();
				}

				cb.AppendLine("#pragma warning disable CS0618");
				cb.AppendUnionAttribute();
				cb.AppendGeneratedCodeAttribute();
				using (cb.WriteBlock($"readonly partial struct {source.Name} : global::System.Runtime.CompilerServices.IUnion"))
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

				if (source.Parameters.Any(t => t.Type is FieldType.PackableStruct))
				{
					cb.AppendLine();
					UnionMemberHelpers.AppendEnclosedStruct(cb, source);
				}

				spc.AddSource($"{GetFileHintName(source)}", SourceText.From(cb.ToString(), Encoding.UTF8));
			});
		}

		private static string GetFileHintName(TypeModel source) =>
			$"{GeneratorHelpers.GetModelFullyQualifiedName(source)}.g.cs";

		private static bool OutputErrorsIfExist(SourceProductionContext spc, TypeModel source)
		{
			bool result = false;

			if (source.Parameters.IsDefaultOrEmpty)
			{
				spc.ReportDiagnostic(Diagnostic.Create(
					NonBoxingUnionDiagnostics.UnionMustDeclareTypeRule,
					source.Location.ToLocation(),
					source.Name));

				result = true;
			}

			if (source.DuplicateTypes.Count > 0)
			{
				foreach (string duplicate in source.DuplicateTypes)
				{
					spc.ReportDiagnostic(Diagnostic.Create(
						NonBoxingUnionDiagnostics.DuplicateUnionTypeRule,
						source.Location.ToLocation(),
						source.Name,
						duplicate));
				}

				result = true;
			}

			if (!source.IsPartial)
			{
				spc.ReportDiagnostic(Diagnostic.Create(
					NonBoxingUnionDiagnostics.UnionMustBePartialRule,
					source.Location.ToLocation(),
					source.Name));

				result = true;
			}

			if (source.NonPartialEnclosingTypes.Count > 0)
			{
				spc.ReportDiagnostic(Diagnostic.Create(
					NonBoxingUnionDiagnostics.AllEnclosingTypesMustBePartialRule,
					source.Location.ToLocation(),
					source.Name,
					string.Join(", ", source.NonPartialEnclosingTypes)));

				result = true;
			}

			return result;
		}
	}
}