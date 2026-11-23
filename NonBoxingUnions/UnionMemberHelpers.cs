using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace NonBoxingUnions;

internal static class UnionMemberHelpers
{
	private const string PackableFieldIdentifier = "_packedField";
	private const string RefTypeEnumIdentifier = "__RefType__";
	private const string RefTypeFieldIdentifier = "_refTypeField";
	private const string TagEnumIdentifier = "Tag";
	private const string TagFieldIdentifier = "_tag";
	private const string UninitializedTagIdentifier = "__Uninitialized__";

	internal static void AppendFieldDeclarations(CodeBuilder cb, TypeModel model)
	{
		if (model.Parameters.Any(t => t.Type is FieldType.ReferenceType))
			cb.AppendLine($"private readonly object? {RefTypeFieldIdentifier};");

		if (model.Parameters.Any(t => t.Type is FieldType.PackableStruct))
			cb.AppendLine($"private readonly {GetPackedStructIdentifier(model)} {PackableFieldIdentifier};");

		foreach (Parameter type in model.Parameters.Where(t => t.Type is FieldType.NonPackableStruct))
			cb.AppendLine($"private readonly {type.FullyQualifiedName} {GetFieldName(type)};");

		cb.AppendLine($"private readonly {TagEnumIdentifier} {TagFieldIdentifier};");
	}

	internal static void AppendConstructors(CodeBuilder cb, TypeModel model)
	{
		foreach (Parameter type in model.Parameters)
		{
			using (cb.WriteBlock($"public {model.Name}({type.FullyQualifiedName} value)"))
			{
				cb.AppendLine($"{TagFieldIdentifier} = {TagEnumIdentifier}.{GetEnumIdentifier(type)};");
				if (type.Type is FieldType.PackableStruct)
					cb.AppendLine($"{PackableFieldIdentifier} = new {GetPackedStructIdentifier(model)}(value);");
				else
					cb.AppendLine($"{GetFieldName(type)} = value;");
			}
		}
	}

	internal static void AppendValueProperty(CodeBuilder cb, TypeModel model)
	{
		using (cb.WriteBlock($"public object? Value => {TagFieldIdentifier} switch", false))
		{
			foreach (Parameter type in model.Parameters.Where(t => t.Type is FieldType.PackableStruct or FieldType.NonPackableStruct))
				cb.AppendLine($"{TagEnumIdentifier}.{GetEnumIdentifier(type)} => {GetFieldName(type)},");

			if (model.Parameters.Any(p => p.Type is FieldType.ReferenceType))
				cb.AppendLine($"{TagEnumIdentifier}.{RefTypeEnumIdentifier} => {RefTypeFieldIdentifier},");

			cb.AppendLine("_ => null");
		}

		cb.AppendLine(";");
	}

	internal static void AppendHasValue(CodeBuilder cb) =>
		cb.AppendLine($"public bool HasValue => _tag is not {TagEnumIdentifier}.{UninitializedTagIdentifier};");

	internal static void AppendTryGetValues(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		foreach (Parameter type in types)
		{
			using (cb.WriteBlock($"public bool TryGetValue({(type.Type is FieldType.ReferenceType ? "[global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] " : null)}out {type.FullyQualifiedName}{(type.Type is FieldType.ReferenceType ? "?" : null)} value)"))
			{
				using (cb.WriteBlock($"if ({TagFieldIdentifier} is {TagEnumIdentifier}.{GetEnumIdentifier(type)})"))
				{
					if (type.Type is FieldType.ReferenceType)
					{
						cb.AppendLine($"value = {RefTypeFieldIdentifier} as {type.FullyQualifiedName};");
						cb.AppendLine($"return value is not null;");
					}
					else
					{
						cb.AppendLine($"value = {GetFieldName(type)};");
						cb.AppendLine("return true;");
					}					
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

	internal static void AppendEnumDeclaration(CodeBuilder cb, ImmutableArray<Parameter> types)
	{
		// Each struct gets its own tag, plus a None tag and a tag for reference types when there is at least 1 reference type.
		int uniqueTags = types.Count(t => t.Type is FieldType.NonPackableStruct or FieldType.PackableStruct) 
			+ (types.Any(t => t.Type is FieldType.ReferenceType) ? 2 : 1);

		using (cb.WriteBlock($"private enum {TagEnumIdentifier} : {GetMinimumRequiredSizeTypeForTagIdentifer(uniqueTags)}"))
		{
			cb.AppendLine($"{UninitializedTagIdentifier} = 0,");

			for (int i = 0; i < types.Length; ++i)
			{
				if (types[i].Type is FieldType.ReferenceType)
					continue;

				cb.AppendLine($"{GetIdentifierName(types[i].FullyQualifiedName)},");
			}

			if (types.Any(t => t.Type is FieldType.ReferenceType))
				cb.AppendLine($"{RefTypeEnumIdentifier}");
		}
	}

	internal static void AppendEnclosedStruct(CodeBuilder cb, TypeModel model)
	{
		cb.AppendLine("[global::System.Obsolete(\"Internal generated type. Do not use directly.\")]");
		cb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
		cb.AppendLine("[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Explicit)]");
		using (cb.WriteBlock($"internal readonly struct {GetPackedStructIdentifier(model)}"))
		{
			const string fieldOffset = "[global::System.Runtime.InteropServices.FieldOffset(0)]";
			foreach (Parameter type in model.Parameters.Where(t => t.Type is FieldType.PackableStruct))
				cb.AppendLine($"{fieldOffset} public readonly {type.FullyQualifiedName} {GetIdentifierName(type.FullyQualifiedName)};");

			cb.AppendLine();

			foreach (Parameter type in model.Parameters.Where(t => t.Type is FieldType.PackableStruct))
				cb.AppendLine($"public {GetPackedStructIdentifier(model)}({type.FullyQualifiedName} value) => {GetIdentifierName(type.FullyQualifiedName)} = value;");
		}
	}

	private static string GetPackedStructIdentifier(TypeModel model) =>
		$"__NonBoxingUnion_Generated_{GetIdentifierName(GeneratorHelpers.GetModelFullyQualifiedName(model), prefix: null)}_PackedStruct";

	private static string GetFieldName(Parameter field)
	{
		if (field.Type is FieldType.ReferenceType)
			return RefTypeFieldIdentifier;
		if (field.Type is FieldType.PackableStruct)
			return $"{PackableFieldIdentifier}.{GetIdentifierName(field.FullyQualifiedName)}";
		return GetIdentifierName(field.FullyQualifiedName, prefix: '_', convertToCamelCase: true, forcePrefix: true);
	}

	private static string GetEnumIdentifier(Parameter field)
	{
		if (field.Type is FieldType.ReferenceType)
			return RefTypeEnumIdentifier;
		return GetIdentifierName(field.FullyQualifiedName);
	}

	private static string GetIdentifierName(string fullyQualifiedName, char? prefix = '@', bool convertToCamelCase = false, bool forcePrefix = false)
	{
		char[] chars = fullyQualifiedName.ToCharArray();

		if (convertToCamelCase)
			chars[0] = char.ToLower(chars[0]);

		for (int i = 0; i < chars.Length; ++i)
		{
			if (!SyntaxFacts.IsIdentifierPartCharacter(chars[i]))
				chars[i] = '_';
		}

		string replaced = new(chars);
		return forcePrefix || replaced == fullyQualifiedName ? $"{prefix}{replaced}" : replaced;
	}

	private static string GetMinimumRequiredSizeTypeForTagIdentifer(int identifierCount) => identifierCount switch
	{
		> 0 and <= byte.MaxValue => "byte",
		> byte.MaxValue and <= ushort.MaxValue => "ushort",
		> ushort.MaxValue and <= int.MaxValue => "int",
		_ => throw new ArgumentOutOfRangeException(nameof(identifierCount))
	};
}
