using Microsoft.CodeAnalysis;

namespace NonBoxingUnions;

internal static class NonBoxingUnionDiagnostics
{
	internal const string DuplicateUnionTypeID = "NBU001";
	internal const string UnionMustDeclareTypeID = "NBU002";
	internal const string UnionMustBePartialID = "NBU003";
	internal const string AllEnclosingTypesMustBePartialID = "NBU004";
	internal const string RedundantDefaultID = "NBU005";

	internal static readonly DiagnosticDescriptor DuplicateUnionTypeRule = new(
		id: DuplicateUnionTypeID,
		title: "Duplicate type in union",
		messageFormat: "NonBoxingUnion '{0}' cannot contain duplicate type '{1}'",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor UnionMustDeclareTypeRule = new(
		id: UnionMustDeclareTypeID,
		title: "Invalid Union Declaration",
		messageFormat: "NonBoxingUnion '{0}' must declare at least 1 type",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor UnionMustBePartialRule = new(
		id: UnionMustBePartialID,
		title: "Invalid Struct Declaration",
		messageFormat: "NonBoxingUnion '{0}' must be declared as `partial`",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor AllEnclosingTypesMustBePartialRule = new(
		id: AllEnclosingTypesMustBePartialID,
		title: "Invalid Enclosing Type Declaration",
		messageFormat: "The following enclosing types of NonBoxingUnion '{0}' must be declared as partial: {1}",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor RedundantDefaultRule = new(
		id: RedundantDefaultID,
		title: "Redundant Attribute Parameter Value",
		messageFormat: "Setting '{0}' to its default value '{1}' is redundant",
		category: "NonBoxingUnion",
		defaultSeverity: DiagnosticSeverity.Hidden,
		isEnabledByDefault: true,
		customTags: [WellKnownDiagnosticTags.Unnecessary]);
}
