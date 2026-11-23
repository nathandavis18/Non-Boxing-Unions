using System;
using System.Collections.Immutable;
using System.Linq;

namespace NonBoxingUnions;

internal static class UnionMemberHelpers
{
	private const string PackableStructIdentifier = "PackedStruct";
	private const string PackableFieldIdentifier = "_packedField";
	private const string RefTypeIdentifier = "_refTypeField";
	private const string TagIdentifier = "_tag";

	internal static void AppendFieldDeclarations(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		if (types.Any(t => t.Type is FieldType.PackableStruct))
		{
			AppendEnclosedStruct(cb, types);
			cb.AppendLine();
		}

		if (types.Any(t => t.Type is FieldType.ReferenceType))
			cb.AppendLine($"private readonly object? {RefTypeIdentifier};");

		if (types.Any(t => t.Type is FieldType.PackableStruct))
			cb.AppendLine($"private readonly {PackableStructIdentifier} {PackableFieldIdentifier};");

		foreach (Parameter type in types.Where(t => t.Type is FieldType.NonPackableStruct))
			cb.AppendLine($"private readonly {type.FullyQualifiedName} {GetFieldName(type)};");

		cb.AppendLine($"private readonly {GetMinimumRequiredSizeTypeForTagIdentifer(types.Length)} {TagIdentifier};");
	}

	internal static void AppendConstructors(CodeBuilder cb, ImmutableArray<Parameter> types, string unionName)
	{
		foreach (Parameter type in types)
		{
			using (cb.WriteBlock($"public {unionName}({type.FullyQualifiedName} value)"))
			{
				cb.AppendLine($"{TagIdentifier} = {type.TagNumber};");
				if (type.Type is FieldType.PackableStruct)
					cb.AppendLine($"{PackableFieldIdentifier} = new {PackableStructIdentifier}(value);");
				else
					cb.AppendLine($"{GetFieldName(type)} = value;");
			}
		}
	}

	internal static void AppendValueProperty(CodeBuilder cb, TypeModel model)
	{
		using (cb.WriteBlock($"public object? Value => {TagIdentifier} switch", false))
		{
			int currentTag = 1;
			foreach (Parameter type in model.Parameters.Where(t => t.Type is FieldType.PackableStruct or FieldType.NonPackableStruct))
			{
				cb.AppendLine($"{type.TagNumber} => {GetFieldName(type)},");
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
			using (cb.WriteBlock($"public bool TryGetValue({(type.Type is FieldType.ReferenceType ? "[global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] " : null)}out {type.FullyQualifiedName}{(type.Type is FieldType.ReferenceType ? "?" : null)} value)"))
			{
				using (cb.WriteBlock($"if ({TagIdentifier} == {type.TagNumber})"))
				{
					if (type.Type is FieldType.ReferenceType)
						cb.AppendLine($"value = ({type.FullyQualifiedName}){RefTypeIdentifier}!;");
					else
						cb.AppendLine($"value = {GetFieldName(type)};");
					cb.AppendLine("return true;");
				}

				cb.AppendLine();
				cb.AppendLine(
					"""
					value = default;
					return false;
					""");
			}
		}
	}

	private static string GetFieldName(Parameter field)
	{
		if (field.Type is FieldType.ReferenceType)
			return RefTypeIdentifier;
		if (field.Type is FieldType.PackableStruct)
			return $"{PackableFieldIdentifier}.{GetIdentifierName(field.FullyQualifiedName)}";
		return $"_{ConvertToCamelCase(GetIdentifierName(field.FullyQualifiedName))}";
	}

	private static string ConvertToCamelCase(string pascalCase)
	{
		char[] chars = pascalCase.ToCharArray();
		chars[0] = char.ToLowerInvariant(chars[0]);
		return new string(chars);
	}

	private static string GetIdentifierName(string fullyQualifiedName)
	{
		string replaced = fullyQualifiedName.Replace(".", "");
		return replaced == fullyQualifiedName ? $"@{replaced}" : replaced;
	}

	private static void AppendEnclosedStruct(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		cb.AppendLine("[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Explicit)]");
		using (cb.WriteBlock($"private readonly struct {PackableStructIdentifier}"))
		{
			const string fieldOffset = "[global::System.Runtime.InteropServices.FieldOffset(0)]";
			foreach (Parameter type in types.Where(t => t.Type is FieldType.PackableStruct))
				cb.AppendLine($"{fieldOffset} public readonly {type.FullyQualifiedName} {GetIdentifierName(type.FullyQualifiedName)};");

			cb.AppendLine();

			foreach (Parameter type in types.Where(t => t.Type is FieldType.PackableStruct))
				cb.AppendLine($"public {PackableStructIdentifier}({type.FullyQualifiedName} value) => {GetIdentifierName(type.FullyQualifiedName)} = value;");
		}
	}

	private static string GetMinimumRequiredSizeTypeForTagIdentifer(int identifierCount) => identifierCount switch
	{
		> 0 and <= byte.MaxValue => "byte",
		> byte.MaxValue and <= ushort.MaxValue => "ushort",
		> ushort.MaxValue and <= int.MaxValue => "int",
		_ => throw new ArgumentOutOfRangeException(nameof(identifierCount))
	};
}
