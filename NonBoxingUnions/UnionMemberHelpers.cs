using System;
using System.Collections.Immutable;
using System.Linq;

namespace NonBoxingUnions;

internal static class UnionMemberHelpers
{
	private const string StructIdentifier = "PackedStruct";
	private const string StructFieldIdentifier = "Field";
	private const string ValueTypeIdentifier = "_valueTypeField";
	private const string RefTypeIdentifier = "_refTypeField";
	private const string TagIdentifier = "_tag";
	private static string GetFieldName(bool isValueType, int tagNumber) =>
		isValueType ? $"{ValueTypeIdentifier}.{StructFieldIdentifier}{tagNumber}" : RefTypeIdentifier;

	internal static void AppendFieldDeclarations(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		if (types.Any(t => t.IsValueType))
		{
			AppendEnclosedStruct(cb, types);
			cb.AppendLine();
		}

		if (types.Any(t => !t.IsValueType))
			cb.AppendLine($"private readonly object? {RefTypeIdentifier};");

		if (types.Any(t => t.IsValueType))
			cb.AppendLine($"private readonly {StructIdentifier} {ValueTypeIdentifier};");

		cb.AppendLine($"private readonly {GetMinimumRequiredSizeTypeForTagIdentifer(types.Length)} {TagIdentifier};");
	}

	internal static void AppendConstructors(CodeBuilder cb, ImmutableArray<Parameter> types, string unionName)
	{
		foreach (Parameter type in types)
		{
			using (cb.WriteBlock($"public {unionName}({type.FullyQualifiedName} value)"))
			{
				cb.AppendLine($"{TagIdentifier} = {type.TagNumber};");
				if (type.IsValueType)
					cb.AppendLine($"{ValueTypeIdentifier} = new {StructIdentifier}(value);");
				else
					cb.AppendLine($"{RefTypeIdentifier} = value;");
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
			using (cb.WriteBlock($"public bool TryGetValue({(type.IsValueType ? null : "[global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] ")}out {type.FullyQualifiedName}{(type.IsValueType ? null : "?")} value)"))
			{
				using (cb.WriteBlock($"if ({TagIdentifier} == {type.TagNumber})"))
				{
					if (type.IsValueType)
						cb.AppendLine($"value = {GetFieldName(true, type.TagNumber)};");
					else
						cb.AppendLine($"value = ({type.FullyQualifiedName}){RefTypeIdentifier}!;");
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

	private static void AppendEnclosedStruct(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		cb.AppendLine("[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Explicit)]");
		using (cb.WriteBlock($"private readonly struct {StructIdentifier}"))
		{
			const string fieldOffset = "[global::System.Runtime.InteropServices.FieldOffset(0)]";
			foreach (Parameter type in types.Where(t => t.IsValueType))
				cb.AppendLine($"{fieldOffset} public readonly {type.FullyQualifiedName} {StructFieldIdentifier}{type.TagNumber};");

			cb.AppendLine();

			foreach (Parameter type in types.Where(t => t.IsValueType))
				cb.AppendLine($"public {StructIdentifier}({type.FullyQualifiedName} value) => {StructFieldIdentifier}{type.TagNumber} = value;");
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
