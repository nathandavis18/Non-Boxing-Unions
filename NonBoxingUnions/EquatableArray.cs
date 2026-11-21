using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NonBoxingUnions;

internal readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
{
	public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);
	private readonly ImmutableArray<T> _array = array;

	public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;
	public int Count => AsImmutableArray().Length;

	public bool Equals(EquatableArray<T> other) => AsImmutableArray().SequenceEqual(other.AsImmutableArray());
	public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

	public override int GetHashCode()
	{
		int hash = 17;
		foreach (T item in AsImmutableArray())
			hash = (hash * 31) + (item?.GetHashCode() ?? 0);
		return hash;
	}

	public ImmutableArray<T>.Enumerator GetEnumerator() => AsImmutableArray().GetEnumerator();
	IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AsImmutableArray()).GetEnumerator();
}

internal static class EquatableArrayExtensions
{
	public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source) where T : IEquatable<T>
		=> new([.. source]);
}