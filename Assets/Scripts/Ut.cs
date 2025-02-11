﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rnd = UnityEngine.Random;
//Code from Timwi
static class Ut
{


    /// <summary>
    ///     Given a set of values and a function that returns true when given this set, will efficiently remove items from
    ///     this set which are not essential for making the function return true. The relative order of items is
    ///     preserved. This method cannot generally guarantee that the result is optimal, but for some types of functions
    ///     the result will be guaranteed optimal.</summary>
    /// <typeparam name="T">
    ///     Type of the values in the set.</typeparam>
    /// <param name="items">
    ///     The set of items to reduce.</param>
    /// <param name="test">
    ///     The function that examines the set. Must always return the same value for the same set.</param>
    /// <param name="breadthFirst">
    ///     A value selecting a breadth-first or a depth-first approach. Depth-first is best at quickly locating a single
    ///     value which will be present in the final required set. Breadth-first is best at quickly placing a lower bound
    ///     on the total number of individual items in the required set.</param>
    /// <returns>
    ///     A hopefully smaller set of values that still causes the function to return true.</returns>
    public static IEnumerable<T> ReduceRequiredSet<T>(IEnumerable<T> items, Func<ReduceRequiredSetState<T>, bool> test, bool breadthFirst = false)
    {
        var state = new ReduceRequiredSetStateInternal<T>(items);

        while (state.AnyPartitions)
        {
            var rangeToSplit = breadthFirst ? state.LargestRange : state.SmallestRange;
            int mid = (rangeToSplit.Item1 + rangeToSplit.Item2) / 2;
            var split1 = new Range(rangeToSplit.Item1, mid);
            var split2 = new Range(mid + 1, rangeToSplit.Item2);

            state.ApplyTemporarySplit(rangeToSplit, split1);
            if (test(state))
            {
                state.RemoveRange(rangeToSplit);
                state.AddRange(split1);
                continue;
            }
            state.ApplyTemporarySplit(rangeToSplit, split2);
            if (test(state))
            {
                state.RemoveRange(rangeToSplit);
                state.AddRange(split2);
                continue;
            }
            state.ResetTemporarySplit();
            state.RemoveRange(rangeToSplit);
            state.AddRange(split1);
            state.AddRange(split2);
        }

        state.ResetTemporarySplit();
        return state.SetToTest;
    }
    /// <summary>
    /// Returns the number of times a given <typeparamref name="T"/> <paramref name="item"/> appears in <paramref name="collection"/>.
    /// </summary>
    /// <param name="collection"> The collection from which count is taken from.</param>
    /// <param name="item">The item that is counted.</param>
    public static int CountOf<T>(this IEnumerable<T> collection, T item)
    {
        if (collection == null)
            throw new ArgumentNullException("collection");
        if (item == null)
            throw new ArgumentNullException("item");
        return collection.Count(x => x.Equals(item));
    }

    /// <summary>
    ///     Encapsulates the state of the <see cref="Ut.ReduceRequiredSet"/> algorithm and exposes statistics about it.</summary>
    public abstract class ReduceRequiredSetState<T>
    {
        /// <summary>Internal; do not use.</summary>
        protected List<Range> Ranges;
        /// <summary>Internal; do not use.</summary>
        protected List<T> Items;
        /// <summary>Internal; do not use.</summary>
        protected Range ExcludedRange, IncludedRange;

        /// <summary>
        ///     Enumerates every item that is known to be in the final required set. "Definitely" doesn't mean that there
        ///     exists no subset resulting in "true" without these members. Rather, it means that the algorithm will
        ///     definitely return these values, and maybe some others too.</summary>
        public IEnumerable<T> DefinitelyRequired { get { return Ranges.Where(r => r.Item1 == r.Item2).Select(r => Items[r.Item1]); } }
        /// <summary>
        ///     Gets the current number of partitions containing uncertain items. The more of these, the slower the
        ///     algorithm will converge from here onwards.</summary>
        public int PartitionsCount { get { return Ranges.Count - Ranges.Count(r => r.Item1 == r.Item2); } }
        /// <summary>
        ///     Gets the number of items in the smallest partition. This is the value that is halved upon a successful
        ///     depth-first iteration.</summary>
        public int SmallestPartitionSize { get { return Ranges.Where(r => r.Item1 != r.Item2).Min(r => r.Item2 - r.Item1 + 1); } }
        /// <summary>
        ///     Gets the number of items in the largest partition. This is the value that is halved upon a successful
        ///     breadth-first iteration.</summary>
        public int LargestPartitionSize { get { return Ranges.Max(r => r.Item2 - r.Item1 + 1); } }
        /// <summary>Gets the total number of items about which the algorithm is currently undecided.</summary>
        public int ItemsRemaining { get { return Ranges.Where(r => r.Item1 != r.Item2).Sum(r => r.Item2 - r.Item1 + 1); } }

        /// <summary>Gets the set of items for which the function should be evaluated in the current step.</summary>
        public IEnumerable<T> SetToTest
        {
            get
            {
                var ranges = Ranges.AsEnumerable();
                if (ExcludedRange != null)
                    ranges = ranges.Where(r => r != ExcludedRange);
                if (IncludedRange != null)
                    ranges = ranges.Concat(new[] { IncludedRange });
                return ranges
                    .SelectMany(range => Enumerable.Range(range.Item1, range.Item2 - range.Item1 + 1))
                    .OrderBy(x => x)
                    .Select(i => Items[i]);
            }
        }
    }

    internal sealed class Range : IEquatable<Range>
    {
        public int Item1 { get; private set; }
        public int Item2 { get; private set; }

        public Range(int item1, int item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public bool Equals(Range obj)
        {
            return obj != null && obj.Item1 == Item1 && obj.Item2 == Item2;
        }

        public override bool Equals(object obj)
        {
            return obj is Range && Equals((Range)obj);
        }

        public override int GetHashCode()
        {
            return 24567847 * Item1 + Item2;
        }
    }

    internal sealed class ReduceRequiredSetStateInternal<T> : ReduceRequiredSetState<T>
    {
        public ReduceRequiredSetStateInternal(IEnumerable<T> items)
        {
            Items = items.ToList();
            Ranges = new List<Range>();
            Ranges.Add(new Range(0, Items.Count - 1));
        }

        public bool AnyPartitions { get { return Ranges.Any(r => r.Item1 != r.Item2); } }
        public Range LargestRange { get { return Ranges.MaxElement(t => t.Item2 - t.Item1); } }
        public Range SmallestRange { get { return Ranges.Where(r => r.Item1 != r.Item2).MinElement(t => t.Item2 - t.Item1); } }

        public void AddRange(Range range) { Ranges.Add(range); }
        public void RemoveRange(Range range) { if (!Ranges.Remove(range)) throw new InvalidOperationException("Ut.ReduceRequiredSet has a bug. Code: 826432"); }

        public void ResetTemporarySplit()
        {
            ExcludedRange = IncludedRange = null;
        }
        public void ApplyTemporarySplit(Range rangeToSplit, Range splitRange)
        {
            ExcludedRange = rangeToSplit;
            IncludedRange = splitRange;
        }
    }

    /// <summary>
    ///     Returns the first element from the input sequence for which the value selector returns the smallest value.</summary>
    /// <exception cref="InvalidOperationException">
    ///     The input collection is empty.</exception>
    public static T MinElement<T, TValue>(this IEnumerable<T> source, Func<T, TValue> valueSelector) where TValue : IComparable<TValue>
    {
        return minMaxElement(source, valueSelector, default(T), true, true).Value;
    }

    /// <summary>
    ///     Returns the first element from the input sequence for which the value selector returns the largest value.</summary>
    /// <exception cref="InvalidOperationException">
    ///     The input collection is empty.</exception>
    public static T MaxElement<T, TValue>(this IEnumerable<T> source, Func<T, TValue> valueSelector) where TValue : IComparable<TValue>
    {
        return minMaxElement(source, valueSelector, default(T), false, true).Value;
    }

    private static KeyValuePair<int?, T> minMaxElement<T, TValue, TResult>(IEnumerable<T> source, Func<T, TValue> valueSelector, TResult defaultValue, bool min, bool doThrow) where TValue : IComparable<TValue>
    {
        if (source == null)
            throw new ArgumentNullException("source");
        if (valueSelector == null)
            throw new ArgumentNullException("valueSelector");

        using (var enumerator = source.GetEnumerator())
        {
            if (!enumerator.MoveNext())
            {
                if (doThrow)
                    throw new InvalidOperationException("source contains no elements.");
                return new KeyValuePair<int?, T>(null, default(T));
            }
            T minMaxElem = enumerator.Current;
            TValue minMaxValue = valueSelector(minMaxElem);
            int minMaxIndex = 0;
            int curIndex = 0;
            while (enumerator.MoveNext())
            {
                curIndex++;
                TValue value = valueSelector(enumerator.Current);
                if (min ? (value.CompareTo(minMaxValue) < 0) : (value.CompareTo(minMaxValue) > 0))
                {
                    minMaxValue = value;
                    minMaxElem = enumerator.Current;
                    minMaxIndex = curIndex;
                }
            }
            return new KeyValuePair<int?, T>(minMaxIndex, minMaxElem);
        }
    }
}