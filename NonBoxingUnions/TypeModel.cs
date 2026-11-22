using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NonBoxingUnions;

internal readonly record struct ContainingType(string Name, bool IsValueType, bool IsRecord);

internal readonly record struct Parameter(string FullyQualifiedName, bool IsValueType, int TagNumber);

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
	EquatableArray<ContainingType> ContainingTypes,
	EquatableArray<string> DuplicateTypes,
	EquatableLocation Location,
	EquatableArray<Parameter> Parameters,
	int OnePastHighestTag,
	bool IsPartial,
	EquatableArray<string> NonPartialEnclosingTypes);