using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NonBoxingUnions;

internal readonly record struct ContainingType(string Name, bool IsValueType, bool IsRecord, int Arity);

internal readonly record struct Parameter(string FullyQualifiedName, FieldType Type);

internal readonly record struct EquatableLocation(string FilePath, TextSpan Source, LinePositionSpan Position)
{
	public EquatableLocation(Location location)
	: this(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span)
	{ }

	public Location ToLocation() => Location.Create(FilePath, Source, Position);
}

internal readonly record struct TypeModel(
	string Name,
	string? Namespace,
	string FullName,
	EquatableArray<ContainingType> ContainingTypes,
	EquatableArray<string> DuplicateTypes,
	EquatableLocation Location,
	EquatableArray<Parameter> Parameters,
	bool IsPartial,
	EquatableArray<string> NonPartialEnclosingTypes);

internal enum FieldType
{
	PackedStruct,
	NonPackedStruct,
	ReferenceType
}