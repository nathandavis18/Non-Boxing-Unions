using System.Runtime.CompilerServices;

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
	public void ValueReturnsCorrectValueType()
	{
		MyUnion x = 10;

		Assert.That(x.Value, Is.EqualTo(10));
		Assert.That(x.Value, Is.TypeOf<int>());
		Assert.That(x.Value, Is.Not.TypeOf<double>());
		Assert.That(x.Value, Is.Not.TypeOf<float>());
	}

	[Test]
	public void NonBoxingUnionDoesNotCreateHeapAllocation()
	{
		MyUnion x = 10;

		// Warm up the JIT
		x.TryGetValue(out int _);

		// Calculate GC allocations made
		long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

		// Assign to double since all 3 types can be implicitly cast to double at compile time
		double y = x switch 
		{
			int i => i,
			double d => d,
			float f => f
		};

		long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

		Assert.That(bytesAfter - bytesBefore, Is.EqualTo(0));
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

	[Test]
	public void ParamsNonBoxingUnionDoesNotCreateHeapAllocation()
	{
		ParamsUnion x = 10;

		// Warm up the JIT
		x.TryGetValue(out int _);

		// Calculate GC allocations made
		long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

		// Assign to double since all 3 types can be implicitly cast to double at compile time
		double y = x switch
		{
			int i => i,
			double d => d,
			float f => f
		};

		long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

		Assert.That(bytesAfter - bytesBefore, Is.EqualTo(0));
	}

	// Packs the int/byte/char/short/sbyte/ushort into a struct where all the fields live at the same memory
	// Since they all live at the same memory, the size of the packed enclosing struct is the size of its largest type (int)
	// Then there is an extra byte field for tracking which value is currently held by the union, which puts us to 5 bytes.
	// 5 bytes isn't word aligned, so the compiler automatically pads the struct with 3 extra bytes, putting the total to 8.
	// Even with padding, the union struct's total size is 8 bytes, which is less than if each type had its own field.
	// Without packing, this struct would optimally be 16 bytes, though with improper padding it could be even bigger.
	[Test]
	public void UnionPacksStruct() => Assert.That(Unsafe.SizeOf<PackingTest>(), Is.EqualTo(8));

	[NonBoxingUnion<int, double, float>]
	private partial struct MyUnion;

	[NonBoxingUnion(typeof(int), typeof(float), typeof(double))]
	private partial struct ParamsUnion;

	[NonBoxingUnion<int, byte, char, short, sbyte, ushort>]
	private partial struct PackingTest;
}
