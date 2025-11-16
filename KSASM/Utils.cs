
using System;
using System.Collections.Generic;

namespace KSASM
{
  public interface IRange<T> where T : IRange<T>, allows ref struct
  {
    public int Offset { get; }
    public int Length { get; }

    public T Slice(int offset, int length);
    public bool TryMerge(T next, out T merged);
  }

  public ref struct SpanRange<T> : IRange<SpanRange<T>>
  {
    public int Offset { get; set; }
    public Span<T> Span;

    public int Length => Span.Length;

    public SpanRange<T> Slice(int offset, int length)
    {
      if (length == 0)
        return new() { Offset = offset, Span = Span<T>.Empty };
      var sstart = offset - Offset;
      return new()
      {
        Offset = offset,
        Span = Span[sstart..(sstart + length)],
      };
    }

    public bool TryMerge(SpanRange<T> next, out SpanRange<T> merged)
    {
      merged = default;
      return false;
    }
  }

  public class RangeList<T> where T : IRange<T>
  {
    private List<T> ranges = [];
    private List<T> prevRanges = [];

    public void AddRange(T newRange)
    {
      (ranges, prevRanges) = (prevRanges, ranges);
      ranges.Clear();

      var newEnd = newRange.Offset + newRange.Length;
      var added = false;

      foreach (var range in prevRanges)
      {
        var end = range.Offset + range.Length;
        if (end <= newRange.Offset)
        {
          // before new range
          AppendRange(range);
          continue;
        }
        if (range.Offset >= newEnd)
        {
          // after new range
          if (!added)
          {
            AppendRange(newRange);
            added = true;
          }
          AppendRange(range);
          continue;
        }
        if (newRange.Offset > range.Offset)
        {
          // take start chunk
          AppendRange(range.Slice(range.Offset, newRange.Offset - range.Offset));
        }
        AppendRange(newRange);
        added = true;
        if (newEnd < end)
        {
          // take end chunk
          AppendRange(range.Slice(newEnd, end - newEnd));
        }
      }
      if (!added)
        AppendRange(newRange);
    }

    private void AppendRange(T range)
    {
      if (ranges.Count > 0 && ranges[^1].TryMerge(range, out var merged))
        ranges[^1] = merged;
      else
        ranges.Add(range);
    }

    public int Count => this.ranges.Count;
    public T this[int index] => this.ranges[index];

    public OverlapIterator<T2> Overlap<T2>(T2 other) where T2 : IRange<T2>, allows ref struct =>
      new(this, FindIndex(other.Offset), other);

    private int FindIndex(int offset)
    {
      var lo = 0;
      var hi = ranges.Count - 1;
      while (lo <= hi)
      {
        var mid = lo + ((hi - lo) >> 1);
        var range = ranges[mid];
        if (offset < range.Offset)
          hi = mid - 1;
        else if (offset >= range.Offset + range.Length)
          lo = mid + 1;
        else
          return mid;
      }
      return ranges.Count;
    }

    public ref struct OverlapIterator<T2>
      where T2 : IRange<T2>, allows ref struct
    {
      private readonly RangeList<T> list;
      private int index;
      private T2 rem;

      public OverlapIterator(RangeList<T> list, int index, T2 rem)
      {
        this.list = list;
        this.index = index;
        this.rem = rem;
      }

      public bool Next(out T range, out T2 other)
      {
        if (index >= list.Count || rem.Length == 0)
        {
          range = default;
          other = default;
          return false;
        }
        range = list[index++];
        var start = Math.Max(range.Offset, rem.Offset);
        var end = Math.Min(range.Offset + range.Length, rem.Offset + rem.Length);
        if (end <= start)
        {
          range = default;
          other = default;
          return false;
        }
        range = range.Slice(start, end - start);
        other = rem.Slice(start, end - start);
        rem = rem.Slice(end, rem.Offset + rem.Length - end);
        return true;
      }
    }
  }
}