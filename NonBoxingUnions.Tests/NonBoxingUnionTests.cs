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

	[Test]
	public void NonDisjointTypesInUnionAreResolvedCorrectly()
	{
		NonDisjointTypesUnion x = new Test();
		Assert.That(x.TryGetValue(out ITest1? _), Is.True);
		Assert.That(x.TryGetValue(out ITest2? _), Is.True);
		Assert.That(x.TryGetValue(out Test? _), Is.True);
		Assert.That(x.TryGetValue(out int _), Is.False);

		x = (ITest1)new Test();
		Assert.That(x.TryGetValue(out ITest1? _), Is.True);
		Assert.That(x.TryGetValue(out ITest2? _), Is.True);
		Assert.That(x.TryGetValue(out Test? _), Is.True);
		Assert.That(x.TryGetValue(out int _), Is.False);

		x = (ITest2)new Test();
		Assert.That(x.TryGetValue(out ITest1? _), Is.True);
		Assert.That(x.TryGetValue(out ITest2? _), Is.True);
		Assert.That(x.TryGetValue(out Test? _), Is.True);
		Assert.That(x.TryGetValue(out int _), Is.False);

		x = 3;
		Assert.That(x.TryGetValue(out ITest1? _), Is.False);
		Assert.That(x.TryGetValue(out ITest2? _), Is.False);
		Assert.That(x.TryGetValue(out Test? _), Is.False);
		Assert.That(x.TryGetValue(out int _), Is.True);
	}

	[Test]
	public void UnionsOnGenericTypesWorkAsExpected()
	{
		GenericUnion<int, TestDummy, ITest2, Test> u1 = new Test(); // Only matches the generic type, since it is the unconstrained generic type so it has its own tag identifier.

		Assert.That(u1.TryGetValue(out ITest1? _), Is.False);
		Assert.That(u1.TryGetValue(out ITest2? _), Is.False);
		Assert.That(u1.TryGetValue(out int _), Is.False);
		Assert.That(u1.TryGetValue(out double _), Is.False);
		Assert.That(u1.TryGetValue(out short _), Is.False);
		Assert.That(u1.TryGetValue(out TestDummy _), Is.False);
		Assert.That(u1.TryGetValue(out Test? _), Is.True);

		GenericUnion<int, TestDummy, ITest2, char> u2 = (ITest2)new Test(); // Matches ITest1 and ITest2, since T3 (ITest2) is constrained to reference types.

		Assert.That(u2.TryGetValue(out ITest1? _), Is.True);
		Assert.That(u2.TryGetValue(out ITest2? _), Is.True);
		Assert.That(u2.TryGetValue(out int _), Is.False);
		Assert.That(u2.TryGetValue(out double _), Is.False);
		Assert.That(u2.TryGetValue(out short _), Is.False);
		Assert.That(u2.TryGetValue(out TestDummy _), Is.False);
		Assert.That(u2.TryGetValue(out char _), Is.False);

		GenericUnion<int, TestDummy, Test, ITest2> u3 = new Test(); // Matches Test and ITest1. ITest2 is the unconstrained generic type, so it has its own tag identifier.

		Assert.That(u3.TryGetValue(out ITest1? _), Is.True);
		Assert.That(u3.TryGetValue(out ITest2? _), Is.False);
		Assert.That(u3.TryGetValue(out int _), Is.False);
		Assert.That(u3.TryGetValue(out double _), Is.False);
		Assert.That(u3.TryGetValue(out short _), Is.False);
		Assert.That(u3.TryGetValue(out TestDummy _), Is.False);
		Assert.That(u3.TryGetValue(out Test? _), Is.True);

		GenericUnion<int, TestDummy, Test, ITest2> u4 = new TestDummy();

		Assert.That(u4.TryGetValue(out ITest1? _), Is.False);
		Assert.That(u4.TryGetValue(out ITest2? _), Is.False);
		Assert.That(u4.TryGetValue(out int _), Is.False);
		Assert.That(u4.TryGetValue(out double _), Is.False);
		Assert.That(u4.TryGetValue(out short _), Is.False);
		Assert.That(u4.TryGetValue(out TestDummy _), Is.True);
		Assert.That(u4.TryGetValue(out Test? _), Is.False);
	}

	// Packs the int/byte/char/short/sbyte/ushort into a struct where all the fields live at the same memory
	// Since they all live at the same memory, the size of the packed enclosing struct is the size of its largest type (int)
	// Then there is an extra byte field for tracking which value is currently held by the union, which puts us to 5 bytes.
	// 5 bytes isn't word aligned, so the compiler automatically pads the struct with 3 extra bytes, putting the total to 8.
	// Even with padding, the union struct's total size is 8 bytes, which is less than if each type had its own field.
	// Without packing, this struct would optimally be 16 bytes, though with improper padding it could be even bigger.
	[Test]
	public void UnionPacksStruct() => Assert.That(Unsafe.SizeOf<PackingTest>(), Is.EqualTo(8));

	// Same as above, with the addition of some reference types.
	// When at least 1 reference type is added, a single `object` field is added, which adds another 8 bytes to the union.
	[Test]
	public void UnionPacksStructWithReferenceTypes() => Assert.That(Unsafe.SizeOf<PackingWithReferenceTypeTest>(), Is.EqualTo(16));

	// 8 bytes from the packed double/int/short, 8 bytes for the TestDummy struct, 1 byte for the tag identifier, and 7 bytes of padding.
	// If the managed struct was packed this would only be 16 bytes (8 bytes for packed struct + 1 byte for tag + 7 bytes of padding).
	// But since managed structs can't be packed it gets its own space, increasing the size to 24.
	[Test]
	public void UnionDoesNotPackNonPackableStructs() => Assert.That(Unsafe.SizeOf<DoesNotPackNonPackableStructs>(), Is.EqualTo(24));

	#region TypeDeclarations
	[NonBoxingUnion<int, double, float>]
	private partial struct MyUnion;

	[NonBoxingUnion(typeof(int), typeof(float), typeof(double))]
	private partial struct ParamsUnion;

	[NonBoxingUnion<int, byte, char, short, sbyte, ushort>]
	private partial struct PackingTest;

	[NonBoxingUnion<int, byte, char, short, sbyte, ushort, string, List<int>, char[]>]
	private partial struct PackingWithReferenceTypeTest;

	[NonBoxingUnion<int, ITest1, ITest2, Test>]
	private partial struct NonDisjointTypesUnion;

	[NonBoxingUnion<double, int, short, TestDummy>]
	private partial struct DoesNotPackNonPackableStructs;

	[NonBoxingUnion<double, int, short, ITest1>]
	private partial struct GenericUnion<T1, T2, T3, T4>
		where T1 : unmanaged
		where T2 : struct
		where T3 : class;

	private interface ITest1;
	private interface ITest2;
	private class Test : ITest1, ITest2;
	private struct TestDummy
	{
#pragma warning disable CS0649
		public string Field1;
#pragma warning restore CS0649
	}
	#endregion // TypeDeclarations
}
