using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NonBoxingUnions;

internal readonly record struct Parameter(string FullyQualifiedName, bool IsValueType, int TagNumber);

internal readonly record struct TypeModel(
	string Name,
	string? Namespace,
	Stack<INamedTypeSymbol> ContainingTypes,
	List<string> DuplicateTypes,
	Location Location,
	ImmutableArray<Parameter> Parameters,
	int OnePastHighestTag);