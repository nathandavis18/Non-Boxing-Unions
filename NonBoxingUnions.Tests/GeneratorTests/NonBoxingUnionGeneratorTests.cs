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

		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location));

		CSharpCompilation compilation = CSharpCompilation.Create(
			assemblyName: "NonBoxingUnions.Tests",
			references: references,
			syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		NonBoxingUnionGenerator generator = new();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

		Assert.That(diagnostics, Is.Empty);
		Assert.That(outputCompilation.GetDiagnostics(), Is.Empty);

		return Verify(driver);
	}

	[Test]
	public Task DisableStructPackingMakesAllValueTypesGetTheirOwnField()
	{
		string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion<int, string, double>(DisableStructPacking = true)]
			partial struct MyUnion;
			""";

		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location));

		CSharpCompilation compilation = CSharpCompilation.Create(
			assemblyName: "NonBoxingUnions.Tests",
			references: references,
			syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		NonBoxingUnionGenerator generator = new();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

		Assert.That(diagnostics, Is.Empty);
		Assert.That(outputCompilation.GetDiagnostics(), Is.Empty);

		return Verify(driver);
	}

	[Test]
	public Task GeneratorUsesLargerTypeWhenUnionHasMoreThan255Types()
	{
		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location));

		CSharpCompilation compilation = CSharpCompilation.Create(
			assemblyName: "NonBoxingUnions.Tests",
			references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		for (int i = 1; i <= 256; ++i)
			compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText($"public struct Dummy{i};"));

		string typeParams = string.Join(", ", Enumerable.Range(1, 256).Select(i => $"typeof(Dummy{i})"));
		string source =
			$"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion({typeParams})]
			public partial struct MyUnion;
			""";

		compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));

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

	[Test]
	public void GeneratorMarksUnionsWithDuplicateTypes()
	{
		string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion<int, double, float, string, int>]
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
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

		Assert.That(diagnostics, Has.One.Matches<Diagnostic>(d => d!.Descriptor == NonBoxingUnionDiagnostics.DuplicateUnionTypeRule));
	}

	[Test]
	public void GeneratorMarksUnionsWithDuplicateTypesUsingParams()
	{
		string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion(typeof(int), typeof(double), typeof(float), typeof(double))]
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
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

		Assert.That(diagnostics, Has.One.Matches<Diagnostic>(d => d!.Descriptor == NonBoxingUnionDiagnostics.DuplicateUnionTypeRule));
	}

	[Test]
	public void GeneratorMarksUnionsMissingType()
	{
		string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion]
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
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

		Assert.That(diagnostics, Has.One.Matches<Diagnostic>(d => d!.Descriptor == NonBoxingUnionDiagnostics.UnionMustDeclareTypeRule));
	}

	[Test]
	public void GeneratorMarksUnionsMissingPartialModifier()
	{
		string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			[NonBoxingUnion<int, char>]
			public struct MyUnion;
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
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

		Assert.That(diagnostics, Has.One.Matches<Diagnostic>(d => d!.Descriptor == NonBoxingUnionDiagnostics.UnionMustBePartialRule));
	}

	[Test]
	public void GeneratorMarksUnionsWhoseEnclosingTypesAreMissingPartialModifier()
	{
		string source =
			"""
			namespace NonBoxingUnions.Tests.GeneratorTests;

			class OuterDummy
			{
				class InnerDummy
				{
					[NonBoxingUnion<int, char>]
					public struct MyUnion;
				}
			}
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
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

		Assert.That(diagnostics, Has.One.Matches<Diagnostic>(d => d!.Descriptor == NonBoxingUnionDiagnostics.AllEnclosingTypesMustBePartialRule));
		Assert.That(diagnostics, Has.One.Matches<Diagnostic>(d => d!.GetMessage().Contains("OuterDummy") && d!.GetMessage().Contains("InnerDummy")));
	}
}
