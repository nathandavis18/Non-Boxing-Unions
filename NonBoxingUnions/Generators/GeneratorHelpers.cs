using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NonBoxingUnions.Shared;
using System.Linq;
using System.Text;

namespace NonBoxingUnions.Generators;

internal static class GeneratorHelpers
{
	internal static void RegisterPostInitializationOutput(IncrementalGeneratorPostInitializationContext ctx)
	{
		ctx.AddEmbeddedAttributeDefinition();

		CodeBuilder cb = new(Constants.AttributeNamespace);

		cb.AppendLine("""
					  /// <summary>
					  /// Represents a union with the types specified in this attribute's ctor.
					  /// Can only be applied to structs, and will mark them as <see langword="readonly" />
					  /// </summary>
					  """);

		// Disable the "Parameter '_' is unread" warning. I know it is unread, but I can read the types from the ctor data.
		cb.AppendLine("#pragma warning disable CS9113");
		cb.AppendEmbeddedAttribute();
		cb.AppendGeneratedCodeAttribute();
		cb.AppendAttributeUsageAttribute();
		cb.AppendLine(
			$$"""
			internal sealed class {{Constants.AttributeName}}(params global::System.Type[] _) : global::System.Attribute
			{
			    /// <summary>
			    /// When <see langword="true"/>, prevents this union from packing unmanaged types.
			    /// Defaults to <see langword="false"/>.
			    /// </summary>
			    /// <remarks>
			    /// Disabling may improve speed for small unions at the cost of an increase in memory usage.
			    /// Consider benchmarking performance before disabling.
			    /// </remarks>
			    [global::System.ComponentModel.DefaultValue(false)]
			    public bool {{Constants.DisableStructPacking}} { get; init; } = false;
			}
			""");
			
		cb.AppendLine("#pragma warning restore CS9113");

		for (int generic = 1; generic <= 16; generic++)
		{
			cb.AppendLine();
			string typeParams = string.Join(", ", Enumerable.Range(1, generic).Select(i => $"T{i}"));
			cb.AppendLine("""
						  /// <summary>
						  /// Represents a union with the generic types specified on this attribute.
						  /// Can only be applied to structs, and will mark them as <see langword="readonly" />
						  /// </summary>
						  """);
			cb.AppendEmbeddedAttribute();
			cb.AppendGeneratedCodeAttribute();
			cb.AppendAttributeUsageAttribute();
			cb.AppendLine(
				$$"""
				internal sealed class {{Constants.AttributeName}}<{{typeParams}}> : global::System.Attribute
				{
				    /// <summary>
				    /// When <see langword="true"/>, prevents this union from packing unmanaged types.
				    /// Defaults to <see langword="false"/>.
				    /// </summary>
				    /// <remarks>
				    /// Disabling may improve speed for small unions at the cost of an increase in memory usage.
				    /// Consider benchmarking performance before disabling.
				    /// </remarks>
				    [global::System.ComponentModel.DefaultValue(false)]
				    public bool {{Constants.DisableStructPacking}} { get; init; } = false;
				}
				""");
		}

		ctx.AddSource($"{Constants.AttributeName}.g.cs", SourceText.From(cb.ToString(), Encoding.UTF8));
	}

	internal static string GetModelFullyQualifiedName(TypeModel source)
	{
		StringBuilder sb = new();

		if (!string.IsNullOrEmpty(source.Namespace))
			sb.Append($"{source.Namespace}.");

		foreach (ContainingType type in source.ContainingTypes)
			sb.Append($"{type.FullName.Replace('<', '{').Replace('>', '}')}.");

		sb.Append(source.FullName.Replace('<', '{').Replace('>', '}'));

		return sb.ToString();
	}
}