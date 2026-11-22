namespace NonBoxingUnions;

internal static class CodeBuilderExtensions
{
	private const string GeneratorName = "NonBoxingUnions.Generator";
	private const string VersionNumber = "1.1.0";

	extension (CodeBuilder cb)
	{
		internal void AppendGeneratedCodeAttribute() =>
			cb.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCode(\"{GeneratorName}\", \"{VersionNumber}\")]");

		internal void AppendEmbeddedAttribute() =>
			cb.AppendLine("[global::Microsoft.CodeAnalysis.EmbeddedAttribute]");

		internal void AppendAttributeUsageAttribute() =>
			cb.AppendLine("[global::System.AttributeUsage(global::System.AttributeTargets.Struct, AllowMultiple = false)]");

		internal void AppendUnionAttribute() =>
			cb.AppendLine("[global::System.Runtime.CompilerServices.UnionAttribute]");
	}
}
