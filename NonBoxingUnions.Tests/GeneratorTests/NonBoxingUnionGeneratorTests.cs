using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using NonBoxingUnions.Analyzers;
using NonBoxingUnions.Generators;
using NUnit.Framework;
using System.Runtime.CompilerServices;
using System.Text;

namespace NonBoxingUnions.Tests.GeneratorTests;

[TestFixture]
internal class NonBoxingUnionGeneratorTests
{
	[Test]
	public async Task GeneratesExpectedCode()
	{
		const string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			#pragma warning disable CS0649
			struct Dummy
			{
			    public string Field1 = "";

				public Dummy() {}
			}
			#pragma warning restore CS0649

			[NonBoxingUnion<int, string, double, Dummy>]
			partial struct MyUnion;
			""";


		await VerifyGenerator(source, "MyUnion");
	}

	[Test]
	public async Task GenericTypedUnionGeneratesCorrectly()
	{
		const string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion<int, string, double>]
			partial struct MyUnion<T1, T2, T3, T4>
				where T1 : struct
				where T2 : class
				where T3 : unmanaged;
			""";

		await VerifyGenerator(source, "MyUnion{T1, T2, T3, T4}");
	}

	[Test]
	public async Task DisableStructPackingMakesAllValueTypesGetTheirOwnField()
	{
		const string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion<int, string, double>(DisableStructPacking = true)]
			partial struct MyUnion;
			""";

		await VerifyGenerator(source, "MyUnion");
	}

	[Test]
	public async Task GeneratorUsesLargerTypeWhenUnionHasMoreThan255Types()
	{
		string dummyStructs = string.Join("\n", Enumerable.Range(1, 256).Select(i => $"public struct Dummy{i};"));
		string typeParams = string.Join(", ", Enumerable.Range(1, 256).Select(i => $"typeof(Dummy{i})"));
		string source =
			$"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion({typeParams})]
			public partial struct MyUnion;
			
			{dummyStructs}
			""";

		await VerifyGenerator(source, "MyUnion");
	}

	[Test]
	public void GeneratorPerformsIncrementalCaching()
	{
		const string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion<int, string, double>]
			public partial struct MyUnion;
			""";

		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location));

		CSharpCompilation compilation = CSharpCompilation.Create(
			assemblyName: "NonBoxingUnions.Tests",
			syntaxTrees: [syntaxTree],
			references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		NonBoxingUnionGenerator generator = new();
		GeneratorDriver driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		GeneratorDriverRunResult results = driver.GetRunResult();

		CSharpCompilation updatedCompilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Dummy;"));
		driver = driver.RunGeneratorsAndUpdateCompilation(updatedCompilation, out _, out _);

		GeneratorDriverRunResult results2 = driver.GetRunResult();

		GeneratorRunResult result = results2.Results.Single();
		IncrementalStepRunReason[] outputSteps = result.TrackedOutputSteps.SelectMany(kvp => kvp.Value).SelectMany(step => step.Outputs.Select(o => o.Reason)).ToArray();

		Assert.That(outputSteps, Is.Not.Empty);
		foreach (IncrementalStepRunReason step in outputSteps)
			Assert.That(step, Is.EqualTo(IncrementalStepRunReason.Cached).Or.EqualTo(IncrementalStepRunReason.Unchanged));
	}

	private static async Task VerifyGenerator(string testCode, string expectedHintName, [CallerMemberName] string fileExpectedOutput = "")
	{

		string embeddedAttributeText = (await File.ReadAllTextAsync("GeneratorTests/VerifiedOutput/EmbeddedAttribute.verified.cs")).ReplaceLineEndings("\r\n");
		string nonBoxingUnionAttributeText = await File.ReadAllTextAsync("GeneratorTests/VerifiedOutput/NonBoxingUnionAttribute.verified.cs");
		string expectedOutput = await File.ReadAllTextAsync($"GeneratorTests/VerifiedOutput/{fileExpectedOutput}.verified.cs");

		NonBoxingUnionGeneratorTest test = new()
		{
			TestState =
			{
				Sources = { testCode },
				GeneratedSources =
				{
					(typeof(NonBoxingUnionGenerator), "Microsoft.CodeAnalysis.EmbeddedAttribute.cs", SourceText.From(embeddedAttributeText, Encoding.UTF8)),
					(typeof(NonBoxingUnionGenerator), "NonBoxingUnionAttribute.g.cs", SourceText.From(nonBoxingUnionAttributeText, Encoding.UTF8)),
					(typeof(NonBoxingUnionGenerator), $"NonBoxingUnions.Tests.GeneratorTests.{expectedHintName}.g.cs", SourceText.From(expectedOutput, Encoding.UTF8)),
				}
			},
			ReferenceAssemblies = Net110,
		};

		await test.RunAsync();
	}

	private static readonly ReferenceAssemblies Net110 = new(
		"net11.0",
		new PackageIdentity("Microsoft.NETCore.App.Ref", "11.0.0"),
		Path.Combine("ref", "net11.0"));

	private sealed class NonBoxingUnionGeneratorTest : CSharpSourceGeneratorTest<NonBoxingUnionAttributeAnalyzer, DefaultVerifier>
	{
		protected override IEnumerable<Type> GetSourceGenerators() => [typeof(NonBoxingUnionGenerator)];
	}
}
