using NUnit.Framework;

namespace NonBoxingUnions.Tests;

[TestFixture]
public partial class NonBoxingUnionTests
{
	[Test]
	public void NonBoxingUnionGetsIntValue()
	{
		MyUnion x = 5;
		
		Assert.That(x.TryGetValue(out int y), Is.True);
		Assert.That(y, Is.EqualTo(5));
	}

	[Test]
	public void NonBoxingUnionGetsDoubleValue()
	{
		MyUnion x = 5d;

		Assert.That(x.TryGetValue(out double y), Is.True);
		Assert.That(y, Is.EqualTo(5));
	}

	[Test]
	public void NonBoxingUnionGetsFloatValue()
	{
		MyUnion x = 5f;

		Assert.That(x.TryGetValue(out float y), Is.True);
		Assert.That(y, Is.EqualTo(5));
	}

	[Test]
	public void NonBoxingUnionFailsWhenTypesAreNotTheSame()
	{
		MyUnion x = 5f;

		Assert.That(x.TryGetValue(out int _), Is.False);
		Assert.That(x.TryGetValue(out double _), Is.False);
	}

	[Test]
	public void HasValueReturnsTrueWhenNotDefault()
	{
		MyUnion x = 5;
		MyUnion y = 5d;
		MyUnion z = 5f;
		MyUnion fail = new();

		Assert.That(x.HasValue, Is.True);
		Assert.That(y.HasValue, Is.True);
		Assert.That(z.HasValue, Is.True);
		Assert.That(fail.HasValue, Is.False);
	}

	[Test]
	public void ValueReturnsCorrectValue()
	{
		MyUnion x = 10;

		Assert.That(x.Value, Is.EqualTo(10));
		Assert.That(x.Value, Is.TypeOf<int>());
		Assert.That(x.Value, Is.Not.TypeOf<double>());
		Assert.That(x.Value, Is.Not.TypeOf<float>());
	}

	[Test]
	public void ParamsNonBoxingUnionGetsIntValue()
	{
		ParamsUnion x = 5;

		Assert.That(x.TryGetValue(out int y), Is.True);
		Assert.That(y, Is.EqualTo(5));
	}

	[Test]
	public void ParamsNonBoxingUnionGetsDoubleValue()
	{
		ParamsUnion x = 5d;

		Assert.That(x.TryGetValue(out double y), Is.True);
		Assert.That(y, Is.EqualTo(5));
	}

	[Test]
	public void ParamsNonBoxingUnionGetsFloatValue()
	{
		ParamsUnion x = 5f;

		Assert.That(x.TryGetValue(out float y), Is.True);
		Assert.That(y, Is.EqualTo(5));
	}

	[Test]
	public void ParamsNonBoxingUnionFailsWhenTypesAreNotTheSame()
	{
		ParamsUnion x = 5f;

		Assert.That(x.TryGetValue(out int _), Is.False);
		Assert.That(x.TryGetValue(out double _), Is.False);
	}

	[Test]
	public void ParamsHasValueReturnsTrueWhenNotDefault()
	{
		ParamsUnion x = 5;
		ParamsUnion y = 5d;
		ParamsUnion z = 5f;
		ParamsUnion fail = new();

		Assert.That(x.HasValue, Is.True);
		Assert.That(y.HasValue, Is.True);
		Assert.That(z.HasValue, Is.True);
		Assert.That(fail.HasValue, Is.False);
	}

	[Test]
	public void ParamsValueReturnsCorrectValue()
	{
		ParamsUnion x = 10;

		Assert.That(x.Value, Is.EqualTo(10));
		Assert.That(x.Value, Is.TypeOf<int>());
		Assert.That(x.Value, Is.Not.TypeOf<double>());
		Assert.That(x.Value, Is.Not.TypeOf<float>());
	}

	[NonBoxingUnion<int, double, float>]
	private partial struct MyUnion;

	[NonBoxingUnion(typeof(int), typeof(string), typeof(float), typeof(double))]
	private partial struct ParamsUnion;
}
