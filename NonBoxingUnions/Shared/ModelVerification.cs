using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace NonBoxingUnions.Shared;

internal readonly record struct DiagnosticData(DiagnosticDescriptor Descriptor, IEnumerable<Location>? AdditionalLocations = null, params string[] MessageFormatArguments);

internal static class ModelVerification
{
	internal static bool IsModelValid(TypeModel source, out IReadOnlyList<DiagnosticData> diagnostics)
	{
		List<DiagnosticData> tmpDiagnostics = [];

		if (source.Parameters.IsDefaultOrEmpty)
		{
			tmpDiagnostics.Add(new DiagnosticData(
				NonBoxingUnionDiagnostics.UnionMustDeclareTypeRule,
				MessageFormatArguments: [source.Name]));
		}

		if (source.DuplicateTypes.Count > 0)
		{
			tmpDiagnostics.Add(new DiagnosticData(
				NonBoxingUnionDiagnostics.DuplicateUnionTypeRule,
				source.DuplicateTypes.Select(t => t.Location.ToLocation()),
				source.Name));
		}

		if (!source.IsPartial)
		{
			tmpDiagnostics.Add(new DiagnosticData(
				NonBoxingUnionDiagnostics.UnionMustBePartialRule,
				MessageFormatArguments: [source.Name]));
		}

		if (source.ContainingTypes.Any(t => !t.IsPartial))
		{
			tmpDiagnostics.Add(new DiagnosticData(
				NonBoxingUnionDiagnostics.AllEnclosingTypesMustBePartialRule,
				source.ContainingTypes.Where(t => !t.IsPartial).Select(t => t.Location.ToLocation()),
				source.Name,
				string.Join(",", source.ContainingTypes.Where(t => !t.IsPartial).Select(t => t.Name))));
		}

		diagnostics = tmpDiagnostics;
		return diagnostics.Count == 0;
	}
}
