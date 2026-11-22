using System.Collections.Immutable;
using System.Linq;

namespace NonBoxingUnions;

internal static class UnionMemberHelpers
{
	private const string ValueTypeIdentifier = "_field";
	private const string RefTypeIdentifier = "_refValue";
	private const string TagIdentifier = "_tag";
	private static string GetFieldName(bool isValueType, int tagNumber) =>
		isValueType ? $"{ValueTypeIdentifier}{tagNumber}" : RefTypeIdentifier;

	internal static void AppendFieldDeclarations(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		cb.AppendLine($"private readonly byte {TagIdentifier};");
		foreach (Parameter type in types.Where(t => t.IsValueType))
			cb.AppendLine($"private readonly {type.FullyQualifiedName} {GetFieldName(true, type.TagNumber)};");

		if (types.Any(t => !t.IsValueType))
			cb.AppendLine($"private readonly object? {RefTypeIdentifier};");
	}

	internal static void AppendConstructors(CodeBuilder cb, ImmutableArray<Parameter> types, string unionName)
	{
		foreach (Parameter type in types)
		{
			using (cb.WriteBlock($"public {unionName}({type.FullyQualifiedName} value)"))
			{
				cb.AppendLine($"{TagIdentifier} = {type.TagNumber};");
				cb.AppendLine($"{GetFieldName(type.IsValueType, type.TagNumber)} = value;");
			}
		}
	}

	internal static void AppendValueProperty(CodeBuilder cb, TypeModel model)
	{
		using (cb.WriteBlock($"public object? Value => {TagIdentifier} switch", false))
		{
			int currentTag = 1;
			foreach (Parameter type in model.Parameters.Where(t => t.IsValueType))
			{
				cb.AppendLine($"{type.TagNumber} => {GetFieldName(true, type.TagNumber)},");
				currentTag = type.TagNumber + 1;
			}

			if (currentTag < model.OnePastHighestTag)
			{
				for (; currentTag < model.OnePastHighestTag - 1; ++currentTag)
					cb.Append($"{currentTag} or ");
				cb.AppendLine($"{currentTag} => {RefTypeIdentifier},");
			}

			cb.AppendLine("_ => null");
		}

		cb.AppendLine(";");
	}

	internal static void AppendHasValue(CodeBuilder cb, int maxTagPlusOne) =>
		cb.AppendLine($"public bool HasValue => _tag > 0 && _tag < {maxTagPlusOne};");

	internal static void AppendTryGetValues(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		foreach (Parameter type in types)
		{
			using (cb.WriteBlock($"public bool TryGetValue({(type.IsValueType ? null : "[global::System.Diagnostics.CodeAnalysis.NotNullWhenAttribute(true)] ")}out {type.FullyQualifiedName}{(type.IsValueType ? null : "?")} value)"))
			{
				using (cb.WriteBlock($"if ({TagIdentifier} == {type.TagNumber})"))
				{
					if (type.IsValueType)
						cb.AppendLine($"value = {GetFieldName(true, type.TagNumber)};");
					else
						cb.AppendLine($"value = ({type.FullyQualifiedName}){RefTypeIdentifier}!;");
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
}
