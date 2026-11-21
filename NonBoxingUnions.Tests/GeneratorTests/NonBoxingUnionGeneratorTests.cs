using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NonBoxingUnions.Tests.GeneratorTests;

[TestFixture]
internal class NonBoxingUnionGeneratorTests
{
	[OneTimeSetUp]
	public void Initialize() => VerifySourceGenerators.Initialize();

	[Test]
	public Task GeneratesExpectedCode()
	{
		string source =
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
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

		Assert.That(diagnostics, Is.Empty);
		Assert.That(outputCompilation.GetDiagnostics(), Is.Empty);

		return Verify(driver);
	}

	[Test]
	public void GeneratorPerformsIncrementalCaching()
	{
		string source =
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

		var result = results2.Results.Single();
		var trackedSteps = result.TrackedSteps;

		var outputSteps = result.TrackedOutputSteps.SelectMany(kvp => kvp.Value).SelectMany(step => step.Outputs);

		Assert.That(outputSteps, Is.Not.Empty);
		foreach (var step in outputSteps)
			Assert.That(step.Reason, Is.EqualTo(IncrementalStepRunReason.Cached).Or.EqualTo(IncrementalStepRunReason.Unchanged));
	}
}
