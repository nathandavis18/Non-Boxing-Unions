using Microsoft.CodeAnalysis;

namespace NonBoxingUnions;

internal static class NonBoxingUnionDiagnostics
{
	internal static readonly DiagnosticDescriptor DuplicateUnionTypeRule = new(
		id: "NBU001",
		title: "Duplicate type in union",
		messageFormat: "NonBoxingUnion '{0}' cannot contain duplicate types",
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

	internal static readonly DiagnosticDescriptor UnionMustBePartialRule = new(
		id: "NBU003",
		title: "Invalid Struct Declaration",
		messageFormat: "NonBoxingUnion '{0}' must be declared as `partial`",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor AllEnclosingTypesMustBePartialRule = new(
		id: "NBU004",
		title: "Invalid Enclosing Type Declaration",
		messageFormat: "The enclosing types of NonBoxingUnion '{0}' must be declared as partial: {1}",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor RedundantDefaultRule = new(
		id: "NBU005",
		title: "Redundant Attribute Parameter Value",
		messageFormat: "Setting '{0}' to its default value '{1}' is redundant",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Hidden,
		isEnabledByDefault: true,
		customTags: [WellKnownDiagnosticTags.Unnecessary]);
}
