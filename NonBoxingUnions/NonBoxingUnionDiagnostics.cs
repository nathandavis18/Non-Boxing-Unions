using Microsoft.CodeAnalysis;

namespace NonBoxingUnions;

internal static class NonBoxingUnionDiagnostics
{
	internal static readonly DiagnosticDescriptor DuplicateUnionTypeRule = new(
		id: "NBU001",
		title: "Duplicate type in union",
		messageFormat: "NonBoxingUnion '{0}' cannot contain duplicate type '{1}'",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor UnionMustDeclareTypeRule = new(
		id: "NBU002",
		title: "Invalid Union Declaration",
		messageFormat: "NonBoxingUnion '{0}' must declare at least 1 type",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);
}
