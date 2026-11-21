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
		private static readonly DiagnosticDescriptor DuplicateUnionTypeRule = new(
			id: "NBU001",
			title: "Duplicate type in union",
			messageFormat: "NonBoxingUnion '{0}' cannot contain duplicate type '{1}'",
			category: "NonBoxingUnion",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		private static readonly DiagnosticDescriptor UnionMustDeclareType = new(
			id: "NBU002",
			title: "Invalid Union Declaration",
			messageFormat: "NonBoxingUnion '{0}' must declare at least 1 type",
			category: "NonBoxingUnion",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);


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
				if (source.Parameters.IsDefaultOrEmpty)
				{
					spc.ReportDiagnostic(Diagnostic.Create(
						UnionMustDeclareType,
						source.Location.ToLocation(),
						source.Name));

					return;
				}

				if (source.DuplicateTypes.Count > 0)
				{
					foreach (string duplicate in source.DuplicateTypes)
					{
						spc.ReportDiagnostic(Diagnostic.Create(
							DuplicateUnionTypeRule,
							source.Location.ToLocation(),
							source.Name,
							duplicate));
					}

					return;
				}

				CodeBuilder cb = new(source.Namespace);
				foreach (ContainingType type in source.ContainingTypes)
				{
					string parentKind = type.IsRecord ? type.IsValueType ? "record struct" : "record"
						: type.IsValueType ? "struct" : "class";

					cb.AppendLine($"partial {parentKind} {type.Name}");
					cb.AppendLine("{");
					cb.Indent();
				}

				cb.AppendUnionAttribute();
				cb.AppendGeneratedCodeAttribute();
				using (cb.WriteBlock($"readonly partial struct {source.Name} : global::System.Runtime.CompilerServices.IUnion"))
				{
					UnionMemberHelpers.AppendFieldDeclarations(cb, source.Parameters.AsImmutableArray());
					cb.AppendLine();
					UnionMemberHelpers.AppendConstructors(cb, source.Parameters.AsImmutableArray(), source.Name);
					cb.AppendLine();
					UnionMemberHelpers.AppendHasValue(cb, source.OnePastHighestTag);
					cb.AppendLine();
					UnionMemberHelpers.AppendValueProperty(cb, source);
					cb.AppendLine();
					UnionMemberHelpers.AppendTryGetValues(cb, source.Parameters.AsImmutableArray());
				}

				foreach (ContainingType _ in source.ContainingTypes)
				{
					cb.UnIndent();
					cb.AppendLine("}");
				}

				spc.AddSource($"{source.Name}.g.cs", SourceText.From(cb.ToString(), Encoding.UTF8));
			});
		}
	}
}