
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KSASM
{
  public class FieldWrapper<T, F>(string name)
  {
    public readonly FieldInfo Field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    public F Get(T obj) => (F)Field.GetValue(obj);
    public void Set(T obj, F val) => Field.SetValue(obj, val);
  }

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

  public class FixedWidthRangeTree<T>(int width) where T : struct, IRange<T>
  {
    private readonly int width = width;
    private readonly AppendBuffer<Node> nodes = new();

    public void Add(T range)
    {
      if (range.Offset < 0 || range.Offset + range.Length > width)
        throw new IndexOutOfRangeException($"{range.Offset}+{range.Length} not in [0,{width})");

      if (nodes.Length == 0)
        nodes.Add(new() { Parent = -1, Left = -1, Right = -1 });

      AddInternal(ref range, 0, 0, width);

      void check(int index, int min, int max)
      {
        ref var node = ref nodes[index];
        if (node.Leaf is T leaf && leaf.Offset + leaf.Length is int end && (leaf.Offset != min || end != max))
          throw new InvalidOperationException($"{index} [{leaf.Offset:X6}-{end:X6}] != [{min:X6}:{max:X6}] ({range.Offset:X6}-{range.Offset + range.Length:X6})");
        var mid = (min + max) >> 1;
        if (node.Left != -1) check(node.Left, min, mid);
        if (node.Right != -1) check(node.Right, mid, max);
      }
      check(0, 0, width);
    }

    private void AddInternal(ref readonly T range, int idx, int min, int max)
    {
      var start = range.Offset;
      var end = start + range.Length;

      if (start < min || end > max)
        throw new InvalidOperationException();

      ref var node = ref nodes[idx];

      if (start == min && end == max)
      {
        node.Leaf = range;
        return;
      }

      var mid = (min + max) >> 1;

      // slice existing leaf into children
      if (node.Leaf is T cur)
      {
        node.Leaf = null;
        if (start > min)
        {
          var send = Math.Min(start, mid);
          var scur = cur.Slice(min, send - min);
          AddInternal(ref scur, idx, min, max);
        }
        if (end < mid)
        {
          var scur = cur.Slice(end, mid - end);
          AddInternal(ref scur, idx, min, max);
        }
        if (start > mid)
        {
          var scur = cur.Slice(mid, start - mid);
          AddInternal(ref scur, idx, min, max);
        }
        if (end < max)
        {
          var sstart = Math.Max(end, mid);
          var scur = cur.Slice(sstart, max - sstart);
          AddInternal(ref scur, idx, min, max);
        }
      }

      if (start < mid)
      {
        if (node.Left == -1)
        {
          node.Left = nodes.Length;
          nodes.Add(new() { Parent = idx, Left = -1, Right = -1 });
        }
        if (end <= mid)
          AddInternal(in range, node.Left, min, mid);
        else
        {
          var lrange = range.Slice(start, mid - start);
          AddInternal(ref lrange, node.Left, min, mid);
        }
      }
      if (end > mid)
      {
        if (node.Right == -1)
        {
          node.Right = nodes.Length;
          nodes.Add(new() { Parent = idx, Left = -1, Right = -1 });
        }
        if (start >= mid)
          AddInternal(in range, node.Right, mid, max);
        else
        {
          var rrange = range.Slice(mid, end - mid);
          AddInternal(ref rrange, node.Right, mid, max);
        }
      }
    }

    private struct Node
    {
      public int Parent;
      public int Left;
      public int Right;
      public T? Leaf;
    }

    public OverlapIterator<T2> Overlap<T2>(T2 other) where T2 : struct, IRange<T2>, allows ref struct =>
      new(this, other);

    private struct WalkState
    {
      public int Index;
      public int Min;
      public int Max;
    }
    private readonly WalkState[] stack = new WalkState[32];

    public ref struct OverlapIterator<T2>(FixedWidthRangeTree<T> list, T2 rem)
      where T2 : IRange<T2>, allows ref struct
    {
      private readonly FixedWidthRangeTree<T> list = list;
      private readonly Span<WalkState> stack = list.stack;
      private int stackIndex = -2;
      private T2 rem = rem;
      private bool keep = false;

      public bool Next(out T range, out T2 other)
      {
        if (!keep)
          NextLeaf();
        keep = false;
        if (stackIndex == -1)
        {
          range = default;
          other = default;
          return false;
        }
        ref var n = ref stack[stackIndex];
        range = list.nodes[stack[stackIndex].Index].Leaf.Value;
        var start = Math.Max(range.Offset, rem.Offset);
        var rend = rem.Offset + rem.Length;
        var end = Math.Min(range.Offset + range.Length, rend);
        if (range.Offset != n.Min || range.Offset + range.Length != n.Max)
          throw new InvalidOperationException();
        if (end <= start)
        {
          range = default;
          other = default;
          return false;
        }
        var initrem = rem;
        rem = rem.Slice(end, rend - end);

        while (range.Offset + range.Length <= rem.Offset + rem.Length)
        {
          NextLeaf();
          if (stackIndex == -1)
            break;
          var nrange = list.nodes[stack[stackIndex].Index].Leaf.Value;
          if (!range.TryMerge(nrange, out var merged))
          {
            keep = true;
            break;
          }
          range = merged;
          end = Math.Min(range.Offset + range.Length, rend);
          rem = rem.Slice(end, rend - end);
        }
        range = range.Slice(start, end - start);
        other = initrem.Slice(start, end - start);
        return true;
      }

      private void NextLeaf()
      {
        var start = rem.Offset;
        var end = start + rem.Length;

        if (stackIndex == -2)
        {
          if (list.nodes.Length == 0)
          {
            stackIndex = -1;
            return;
          }
          stackIndex = 0;
          stack[0] = new() { Index = 0, Min = 0, Max = list.width };
        }

        var last = -1;
        while (stackIndex > -1)
        {
          ref var curs = ref stack[stackIndex];
          ref var curn = ref list.nodes[curs.Index];
          var mid = (curs.Min + curs.Max) >> 1;
          var curLast = last;
          last = curs.Index;
          if (start >= curs.Max || end <= curs.Min)
          {
            stackIndex--;
            continue;
          }
          if (curn.Leaf != null)
            return;

          if (start < mid && curn.Left != -1 && curn.Left != curLast)
          {
            stack[++stackIndex] = new()
            {
              Index = curn.Left,
              Min = curs.Min,
              Max = mid,
            };
            continue;
          }
          if (end >= curs.Max && curn.Right != -1 && curn.Right != curLast)
          {
            stack[++stackIndex] = new()
            {
              Index = curn.Right,
              Min = mid,
              Max = curs.Max,
            };
            continue;
          }
          stackIndex--;
        }
      }
    }
  }

  public class ListPool<T>
  {
    private readonly Stack<List<T>> lists = [];

    public List<T> Take(params Span<T> values)
    {
      if (!lists.TryPop(out var list))
        list = new();
      list.AddRange(values);
      return list;
    }

    public void Return(List<T> list)
    {
      if (list == null)
        return;
      list.Clear();
      lists.Push(list);
    }

    public BufferLease Lease() => new(this, Take());

    public record struct BufferLease(ListPool<T> Pool, List<T> List) : IDisposable
    {
      public void Dispose() => Pool.Return(List);
    }
  }

  public interface IAddr { public int Addr { get; } }

  public readonly struct FixedRange(int start, int length) : IAddr, IRange<FixedRange>
  {
    public static readonly FixedRange Invalid = new(-1, -1);

    public readonly int Start = start;
    public readonly int Length = length;

    public readonly int End => Start + Length;

    int IAddr.Addr => Start;

    public static FixedRange From(Range range, int length)
    {
      var (start, len) = range.GetOffsetAndLength(length);
      return new(start, len);
    }

    public FixedRange this[Range range]
    {
      get
      {
        var (off, len) = range.GetOffsetAndLength(Length);
        return new(Start + off, len);
      }
    }

    int IRange<FixedRange>.Offset => Start;
    int IRange<FixedRange>.Length => Length;
    FixedRange IRange<FixedRange>.Slice(int offset, int length) => new(offset, length);
    bool IRange<FixedRange>.TryMerge(FixedRange next, out FixedRange merged) =>
      (merged = End == next.Start ? new(Start, Length + next.Length) : default).Length > 0;

    public static FixedRange operator +(FixedRange range, int offset) => new(range.Start + offset, range.Length);
    public static FixedRange operator -(FixedRange range, int offset) => new(range.Start - offset, range.Length);

    public static implicit operator Range(FixedRange range) => range.Start..range.End;
  }

  public abstract class AppendBuffer
  {
    // consts separated out in non-generic base for easy access
    public const int CHUNK_SHIFT = 12;
    public const int CHUNK_SIZE = 1 << CHUNK_SHIFT;
    public const int CHUNK_MASK = CHUNK_SIZE - 1;
  }

  public class AppendBuffer<T> : AppendBuffer where T : struct
  {

    private readonly T[] copyChunk = new T[CHUNK_SIZE];
    private readonly List<T[]> chunks = [];
    private int length = 0;

    public int Length
    {
      get => length;
      set => length = value <= length ? value : throw new IndexOutOfRangeException();
    }

    public ref T this[int index]
    {
      get
      {
        var ci = index >> CHUNK_SHIFT;
        var co = index & CHUNK_MASK;
        return ref chunks[ci][co];
      }
    }

    public ReadOnlySpan<T> this[Range range] => this[FixedRange.From(range, length)];

    public ReadOnlySpan<T> this[FixedRange range]
    {
      get
      {
        if (range.Length == 0)
          return [];
        if (range.Length > CHUNK_SIZE)
          throw new InvalidOperationException($"range too large {range.Length}");

        var ci = range.Start >> CHUNK_SHIFT;
        var co = range.Start & CHUNK_MASK;
        if (co + range.Length <= CHUNK_SIZE)
          return chunks[ci].AsSpan()[co..(co + range.Length)];

        var l1 = CHUNK_SIZE - co;
        var l2 = range.Length - l1;

        chunks[ci].AsSpan()[co..].CopyTo(copyChunk.AsSpan());
        chunks[ci + 1].AsSpan()[..l2].CopyTo(copyChunk.AsSpan()[l1..]);

        return copyChunk.AsSpan()[..range.Length];
      }
    }

    public void Clear() => length = 0;

    public int Add(T val)
    {
      var ci = length >> CHUNK_SHIFT;
      var co = length & CHUNK_MASK;
      if (ci == chunks.Count)
        chunks.Add(new T[CHUNK_SIZE]);
      chunks[ci][co] = val;
      length++;
      return length - 1;
    }

    public int AddRange(params ReadOnlySpan<T> data)
    {
      var start = length;
      while (data.Length > 0)
      {
        var ci = length >> CHUNK_SHIFT;
        var co = length & CHUNK_MASK;
        if (ci == chunks.Count)
          chunks.Add(new T[CHUNK_SIZE]);

        var rem = CHUNK_SIZE - co;
        if (rem > data.Length)
          rem = data.Length;

        data[..rem].CopyTo(chunks[ci].AsSpan(co));
        data = data[rem..];

        length += rem;
      }
      return start;
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator(AppendBuffer<T> buf)
    {
      private readonly AppendBuffer<T> buf = buf;
      private int index = -1;
      public T Current => buf[index];
      public bool MoveNext() => ++index < buf.Length;
      public Enumerator GetEnumerator() => this;
    }

    public ChunkEnumerator Chunks() => new(this, new(0, length));
    public ChunkEnumerator Chunks(FixedRange range)
    {
      if (range.Start < 0) throw new IndexOutOfRangeException($"{range.Start}");
      if (range.End > length) throw new IndexOutOfRangeException($"{range.End}");
      return new(this, range);
    }

    public ref struct ChunkEnumerator(AppendBuffer<T> buf, FixedRange range)
    {
      private readonly AppendBuffer<T> buf = buf;
      private readonly FixedRange range = range;
      private int index = -1;
      public ReadOnlySpan<T> Current
      {
        get
        {
          var cstart = index << CHUNK_SHIFT;
          var start = cstart;
          var end = cstart + CHUNK_SIZE;
          if (start < range.Start)
            start = range.Start;
          if (end > range.End)
            end = range.End;
          return buf.chunks[index].AsSpan(start - cstart, end - start);
        }
      }
      public bool MoveNext()
      {
        if (index == -1)
          index = range.Start >> CHUNK_SHIFT;
        else
          index++;
        return index << CHUNK_SHIFT < buf.Length;
      }
      public ChunkEnumerator GetEnumerator() => this;
    }
  }

  public class RefStack<T> where T : struct
  {
    private const int INIT_SIZE = 10;

    private T[] values = new T[INIT_SIZE];
    private int length;

    public int Length => length;

    public ref T Top => ref values[length - 1];

    public void Push(T val)
    {
      if (length == values.Length)
      {
        var oldValues = values;
        values = new T[length * 2];
        oldValues.CopyTo(values);
      }
      values[length++] = val;
    }

    public T Pop() => values[--length];
  }

  public static partial class Extensions
  {
    public static void SortStable<T>(this Span<T> span, Comparer<T> comparer)
    {
      // n^2 insertion sort
      for (var i = 1; i < span.Length; i++)
      {
        var j = i;
        while (j > 0 && comparer.Compare(span[j - 1], span[j]) > 0)
        {
          (span[j - 1], span[j]) = (span[j], span[j - 1]);
          j--;
        }
      }
    }
  }
}