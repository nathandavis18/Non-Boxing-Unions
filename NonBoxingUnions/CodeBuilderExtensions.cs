using System.Collections.Immutable;
using System.Linq;

namespace NonBoxingUnions;

internal static class CodeBuilderExtensions
{
	private const string GeneratorName = "NonBoxingUnions.Generator";
	private const string VersionNumber = "1.0.0";

	extension (CodeBuilder cb)
	{
		internal void AppendGeneratedCodeAttribute() =>
			cb.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"{GeneratorName}\", \"{VersionNumber}\")]");

		internal void AppendEmbeddedAttribute() =>
			cb.AppendLine("[global::Microsoft.CodeAnalysis.EmbeddedAttribute]");

		internal void AppendAttributeUsageAttribute() =>
			cb.AppendLine("[global::System.AttributeUsage(global::System.AttributeTargets.Struct, AllowMultiple = false)]");

		internal void AppendUnionAttribute() =>
			cb.AppendLine("[global::System.Runtime.CompilerServices.UnionAttribute]");

		internal void AppendTryGetValues(ImmutableArray<Parameter> types)
		{
			foreach (Parameter type in types)
			{
				using (cb.WriteBlock($"public bool TryGetValue({(type.IsValueType ? null : "[global::System.Diagnostics.CodeAnalysis.NotNullWhenAttribute(true)]")} out {type.FullyQualifiedName}{(type.IsValueType ? null : " ? ")} value)"))
				{
					using (cb.WriteBlock($"if (_tag == {type.TagNumber})"))
					{
						cb.AppendLine($"value = {(type.IsValueType ? $"_field{type.TagNumber}" : $"({type.FullyQualifiedName})_refValue!")};");
						cb.AppendLine("return true;");
					}

					cb.AppendLine(
						"""
					
					value = default;
					return false;
					""");
				}
			}
		}

		internal void AppendValueProperty(TypeModel model)
		{
			using (cb.WriteBlock("public object? Value => _tag switch", false))
			{
				int currentTag = 1;
				foreach (Parameter type in model.Parameters.Where(t => t.IsValueType))
				{
					cb.AppendLine($"{type.TagNumber} => _field{type.TagNumber},");
					currentTag = type.TagNumber + 1;
				}

				if (currentTag < model.OnePastHighestTag)
				{
					for (; currentTag < model.OnePastHighestTag - 1; ++currentTag)
						cb.Append($"{currentTag} or ");
					cb.AppendLine($"{currentTag} => _refValue,");
				}

				cb.AppendLine("_ => null");
			}

			cb.AppendLine(";");
		}

		internal void AppendConstructors(ImmutableArray<Parameter> types, string unionName)
		{
			foreach (Parameter type in types)
			{
				using (cb.WriteBlock($"public {unionName}({type.FullyQualifiedName} value)"))
				{
					cb.AppendLine($"_tag = {type.TagNumber};");
					string fieldName = type.IsValueType ? $"_field{type.TagNumber}" : "_refValue";
					cb.AppendLine($"{fieldName} = value;");
				}
			}
		}

		internal void AppendFieldDeclarations(ImmutableArray<Parameter> types)
		{
			cb.AppendLine("private readonly byte _tag;");
			foreach (Parameter type in types.Where(t => t.IsValueType))
				cb.AppendLine($"private readonly {type.FullyQualifiedName} _field{type.TagNumber};");

			if (types.Any(t => !t.IsValueType))
				cb.AppendLine("private readonly object? _refValue;");
		}
	}
}
