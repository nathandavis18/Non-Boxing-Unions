using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NonBoxingUnions.Analyzers;
using NonBoxingUnions.CodeFixProviders;
using NonBoxingUnions.Generators;
using NonBoxingUnions.Shared;
using NUnit.Framework;

namespace NonBoxingUnions.Tests;

[TestFixture]
public class NonBoxingUnionAnalyzerAndCodeFixProviderTests
{
	[Test]
	public async Task AnalyzerMarksUnionsWithDuplicateTypes()
	{
		string testCode =
			$$"""
			  namespace NonBoxingUnions.Tests;

			  {|{{NonBoxingUnionDiagnostics.DuplicateUnionTypeRule.Id}}:[NonBoxingUnion<int, double, float, string, int>]
			  public partial struct MyUnion;|}
			  """;

		const string fixedCode =
			"""
			namespace NonBoxingUnions.Tests;

			[NonBoxingUnion<int, double, float, string>]
			public partial struct MyUnion;
			""";

		await VerifyCodeFixProvider<DuplicateUnionTypeProvider>(testCode, fixedCode);
	}

	[Test]
	public async Task AnalyzerMarksUnionsWithDuplicateTypesUsingParams()
	{
		string testCode =
			$$"""
			  namespace NonBoxingUnions.Tests;

			  {|{{NonBoxingUnionDiagnostics.DuplicateUnionTypeRule.Id}}:[NonBoxingUnion(typeof(int), typeof(double), typeof(float), typeof(double))]
			  public partial struct MyUnion;|}
			  """;

		const string fixedCode =
			"""
			namespace NonBoxingUnions.Tests;

			[NonBoxingUnion(typeof(int), typeof(double), typeof(float))]
			public partial struct MyUnion;
			""";

		await VerifyCodeFixProvider<DuplicateUnionTypeProvider>(testCode, fixedCode);
	}

	[Test]
	public async Task AnalyzerMarksUnionsMissingType()
	{
		string testCode =
			$$"""
			  namespace NonBoxingUnions.Tests;

			  {|{{NonBoxingUnionDiagnostics.UnionMustDeclareTypeRule.Id}}:[NonBoxingUnion]
			  public partial struct MyUnion;|}
			  """;

		await VerifyAnalyzer(testCode);
	}

	[Test]
	public async Task AnalyzerMarksUnionsMissingPartialModifier()
	{
		string testCode =
			$$"""
			  namespace NonBoxingUnions.Tests;

			  {|{{NonBoxingUnionDiagnostics.UnionMustBePartialRule.Id}}:[NonBoxingUnion<int, char>]
			  public struct MyUnion;|}
			  """;

		const string fixedCode =
			"""
			namespace NonBoxingUnions.Tests;

			[NonBoxingUnion<int, char>]
			public partial struct MyUnion;
			""";

		await VerifyCodeFixProvider<MissingPartialKeywordProvider>(testCode, fixedCode);
	}

	[Test]
	public async Task AnalyzerMarksUnionsWhoseEnclosingTypesAreMissingPartialModifier()
	{
		string testCode =
			$$"""
			  namespace NonBoxingUnions.Tests;

			  class OuterDummy
			  {
			      class InnerDummy
			      {
			          {|{{NonBoxingUnionDiagnostics.AllEnclosingTypesMustBePartialRule.Id}}:[NonBoxingUnion<int, char>]
			          public partial struct MyUnion;|}
			      }
			  }
			  """;

		const string fixedCode =
			"""
			namespace NonBoxingUnions.Tests;

			partial class OuterDummy
			{
			    partial class InnerDummy
			    {
			        [NonBoxingUnion<int, char>]
			        public partial struct MyUnion;
			    }
			}
			""";

		await VerifyCodeFixProvider<AllEnclosingTypesMustBePartialProvider>(testCode, fixedCode);
	}

	[Test]
	public async Task AnalyzerMarksUnionThatDeclaresRedundantArgument()
	{
		string testCode =
			$$"""
			  namespace NonBoxingUnions.Tests;

			  [NonBoxingUnion<int, char>({|{{NonBoxingUnionDiagnostics.RedundantDefaultRule.Id}}:DisableStructPacking = false|})]
			  public partial struct MyUnion;
			  """;

		const string fixedCode =
			"""
			namespace NonBoxingUnions.Tests;

			[NonBoxingUnion<int, char>]
			public partial struct MyUnion;
			""";

		await VerifyCodeFixProvider<RemoveRedundantAttributeArgumentProvider>(testCode, fixedCode);
	}

	private static async Task VerifyCodeFixProvider<TProvider>(string testCode, string fixedCode) where TProvider : CodeFixProvider, new()
	{
		NonBoxingUnionCodeFixProviderTest<TProvider> test = new()
		{
			TestCode = testCode,
			FixedCode = fixedCode,
			ReferenceAssemblies = Net110,
			MarkupOptions = MarkupOptions.UseFirstDescriptor
		};
		test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;

		await test.RunAsync();
	}

	private static async Task VerifyAnalyzer(string testCode)
	{
		NonBoxingUnionAnalyzerTest test = new()
		{
			TestCode = testCode,
			ReferenceAssemblies = Net110,
			MarkupOptions = MarkupOptions.UseFirstDescriptor
		};
		test.TestBehaviors |= TestBehaviors.SkipGeneratedSourcesCheck;

		await test.RunAsync();
	}
	private static readonly ReferenceAssemblies Net110 = new(
		"net11.0",
		new PackageIdentity("Microsoft.NETCore.App.Ref", "11.0.0"),
		Path.Combine("ref", "net11.0"));

	private sealed class NonBoxingUnionCodeFixProviderTest<TProvider> : CSharpCodeFixTest<NonBoxingUnionAttributeAnalyzer, TProvider, DefaultVerifier> where TProvider : CodeFixProvider, new()
	{
		protected override IEnumerable<Type> GetSourceGenerators() => [typeof(NonBoxingUnionGenerator)];
	}

	private sealed class NonBoxingUnionAnalyzerTest : CSharpAnalyzerTest<NonBoxingUnionAttributeAnalyzer, DefaultVerifier>
	{
		protected override IEnumerable<Type> GetSourceGenerators() => [typeof(NonBoxingUnionGenerator)];
	}
}
