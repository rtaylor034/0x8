using System.Collections;

#nullable enable
namespace Perfection
{
    // DEV: so what if we made the default IPStructures read-only (out T),
    // then made a "handle" to mutate it, with that handle being 'in T'.
    public delegate T Updater<T>(T original);
    // MUST: methods that start with '_' must return the instatiating type.
    // is this dumb as shit? yes. is it kinda cool? in my head and in my head only.
    public interface IHasElements<out T>
    {
        public int Count { get; }
        public IEnumerable<T> Elements { get; }
    }
    public interface IEntryAddable<in T>
    {
        public IEntryAddable<T> _WithEntries(IEnumerable<T> entries);
    }
    public interface IEntryRemovable<in T>
    {
        public IEntryRemovable<T> _WithoutEntries(IEnumerable<T> entries);
    }
    public interface IIndexReadable<in I, out T>
    {
        public T At(I index);
    }
    public interface IMergable<in U> where U : IMergable<U>
    {
        public IMergable<U> _MergedWith(U union);
    }
    public interface IIntersectable<in U> where U : IIntersectable<U>
    {
        public IIntersectable<U> _IntersectedWith(U intersection);
        public IIntersectable<U> _InversectedWith(U intersection);
    }
    public interface IPMap<K, T> : IHasElements<ITiple<K, T>>, IEntryAddable<ITiple<K, T>>, IEntryRemovable<K>, IIndexReadable<K, IOption<T>>, IMergable<IPMap<K, T>>
    { }
    public interface IPSet<T> : IHasElements<T>, IEntryAddable<T>, IEntryRemovable<T>, IIndexReadable<T, bool>, IMergable<IPSet<T>>, IIntersectable<IPSet<T>>
    { }
    public interface IPSequence<T> : IHasElements<T>, IIndexReadable<int, T>, IEntryAddable<ITiple<int, T>>, IEntryAddable<T>, IMergable<IPSequence<T>>, IEntryRemovable<Range>
    {
        public IPSequence<T> _WithInsertionAt(int index, IEnumerable<T> items);
    }
    public interface IPStack<T> : IEntryAddable<T>, IIndexReadable<int, IOption<IPStack<T>>>, IHasElements<T>
    {
        public IOption<T> TopValue { get; }
    }
    public static class SelfTypeAssumption
    {
        public static Self WithEntries<Self, T>(this Self s, IEnumerable<T> values) where Self : IEntryAddable<T>
        { return (Self)s._WithEntries(values); }
        public static Self WithEntries<Self, T>(this Self s, params T[] values) where Self : IEntryAddable<T>
            => WithEntries(s, values.IEnumerable());
        public static Self WithoutEntries<Self, T>(this Self s, IEnumerable<T> values) where Self : IEntryRemovable<T>
        { return (Self)s._WithoutEntries(values); }
        public static Self WithoutEntries<Self, T>(this Self s, params T[] values) where Self : IEntryRemovable<T>
            => WithoutEntries(s, values.IEnumerable());

        //CHECK: type restrictions might be silly here
        public static Self MergedWith<Self, T>(this Self s, T other) where Self : IMergable<T>, T where T : IMergable<T>
        { return (Self)s._MergedWith(other); }
        public static Self IntersectedWith<Self, T>(this Self s, T other) where Self : IIntersectable<T>, T where T : IIntersectable<T>
        { return (Self)s._IntersectedWith(other); }
        public static Self InversectedWith<Self, T>(this Self s, T other) where Self : IIntersectable<T>, T where T : IIntersectable<T>
        { return (Self)s._InversectedWith(other); }
        public static Self WithInsertionAt<Self, T>(this Self s, int index, IEnumerable<T> values) where Self : IPSequence<T>
        { return (Self)s._WithInsertionAt(index, values); }
        public static Self WithInsertionAt<Self, T>(this Self s, int index, params T[] values) where Self : IPSequence<T>
            => WithInsertionAt(s, index, values.IEnumerable());
    }
    public static class StructureExtensions
    {
        public static IEnumerable<IPStack<T>> SubStacks<T>(this IPStack<T> stack)
        {
            for (var link = stack.AsSome(); link.Check(out var substack) && substack.Count > 0; link = substack.At(1))
                yield return substack;
        }
        public static IPStack<T> MapTopValue<T>(this IPStack<T> stack, Func<T, T> function)
        {
            return stack.TopValue.Check(out var top)
                ? stack.At(1).Expect("TopValue implies At(1)").WithEntries(function(top))
                : stack;
        }
        public static RecursiveEvalTree<O, T> RecursiveEvalTree<O, T>(this O root, Func<O, IResult<T, IEnumerable<O>>> resolveFunc, Func<IEnumerable<T>, T> combineFunc)
        {
            return new(root, resolveFunc, combineFunc);
        }
        public static IOption<V> At<K, V>(this IDictionary<K, V> dict, K key)
        {
            return (dict.TryGetValue(key, out var v))
                ? v.AsSome()
                : new None<V>();
        }
    }
    public static class StructureICast
    {
        public static IPStack<T> I<T>(this IPStack<T> s) => s;
        public static IPSequence<T> I<T>(this IPSequence<T> s) => s;
        public static IPSet<T> I<T>(this IPSet<T> s) => s;
        public static IPMap<K, V> I<K, V>(this IPMap<K, V> s) => s;
    }

    // DEV/FIXME: temporary bare-functionality inneficient implementations.

    public class PMap<K, T>() : IPMap<K, T> where K : notnull
    {
        private readonly Dictionary<K, T> _dict = new(0);
        public int Count => _dict.Count;

        public IEnumerable<ITiple<K, T>> Elements => _dict.Map(x => (x.Key, x.Value).Tiple());

        private PMap(Dictionary<K, T> dict) : this() { _dict = dict; }
        public IOption<T> At(K index)
        {
            return _dict.TryGetValue(index, out var v) ? v.AsSome() : new None<T>();
        }

        IMergable<IPMap<K, T>> IMergable<IPMap<K, T>>._MergedWith(IPMap<K, T> union)
        {
            var ndict = new Dictionary<K, T>(_dict);
            foreach (var e in union.Elements) ndict[e.A] = e.B;
            return new PMap<K, T>(ndict);
        }

        IEntryAddable<ITiple<K, T>> IEntryAddable<ITiple<K, T>>._WithEntries(IEnumerable<ITiple<K, T>> entries)
        {
            var ndict = new Dictionary<K, T>(_dict);
            foreach (var e in entries) ndict[e.A] = e.B;
            return new PMap<K, T>(ndict);
        }

        IEntryRemovable<K> IEntryRemovable<K>._WithoutEntries(IEnumerable<K> entries)
        {
            var ndict = new Dictionary<K, T>(_dict);
            foreach (var r in entries) _ = ndict.Remove(r);
            return new PMap<K, T>(ndict);
        }
        public override string ToString()
        {
            return $"PMap[{string.Join(", ", Elements)}]";
        }
    }
    public class PSet<T>() : IPSet<T>
    {
        private readonly HashSet<T> _set = new(0);
        public int Count => _set.Count;
        public IEnumerable<T> Elements => _set;

        private PSet(HashSet<T> set) : this() { _set = set; }
        public bool At(T index)
        {
            return _set.Contains(index);
        }

        IMergable<IPSet<T>> IMergable<IPSet<T>>._MergedWith(IPSet<T> union)
        {
            var nset = new HashSet<T>(_set);
            nset.UnionWith(union.Elements);
            return new PSet<T>(nset);
        }

        IEntryAddable<T> IEntryAddable<T>._WithEntries(IEnumerable<T> entries)
        {
            var nset = new HashSet<T>(_set);
            nset.UnionWith(entries);
            return new PSet<T>(nset);
        }

        IEntryRemovable<T> IEntryRemovable<T>._WithoutEntries(IEnumerable<T> entries)
        {
            var nset = new HashSet<T>(_set);
            nset.ExceptWith(entries);
            return new PSet<T>(nset);
        }

        IIntersectable<IPSet<T>> IIntersectable<IPSet<T>>._IntersectedWith(IPSet<T> intersection)
        {
            var nset = new HashSet<T>(_set);
            nset.IntersectWith(intersection.Elements);
            return new PSet<T>(nset);
        }

        IIntersectable<IPSet<T>> IIntersectable<IPSet<T>>._InversectedWith(IPSet<T> intersection)
        {
            var nset = new HashSet<T>(_set);
            nset.SymmetricExceptWith(intersection.Elements);
            return new PSet<T>(nset);
        }
    }
    public class PSequence<T>() : IPSequence<T>
    {
        private readonly CachingEnumerable<T> _list = new([], 0);
        public int Count => _list.CountAndCache();
        public IEnumerable<T> Elements => _list;

        public T At(int index)
        {
            return _list.At(index);
        }

        private PSequence(IEnumerable<T> values) : this() { _list = new(values); }
        IMergable<IPSequence<T>> IMergable<IPSequence<T>>._MergedWith(IPSequence<T> union)
        {
            return this.WithEntries(union.Elements);
        }

        IEntryAddable<ITiple<int, T>> IEntryAddable<ITiple<int, T>>._WithEntries(IEnumerable<ITiple<int, T>> entries)
        {
            var nlist = new List<T>(_list);
            foreach (var t in entries) nlist[t.A] = t.B;
            return new PSequence<T>(nlist);
        }

        IEntryAddable<T> IEntryAddable<T>._WithEntries(IEnumerable<T> entries)
        {
            return new PSequence<T>(_list.Also(entries));
        }

        IEntryRemovable<Range> IEntryRemovable<Range>._WithoutEntries(IEnumerable<Range> entries)
        {
            var optList = new List<IOption<T>>(_list.Map(x => x.AsSome()));
            foreach (var range in entries)
            {
                for (int i = Math.Max(0, range.Start.Value); i < optList.Count && i <= range.End.Value; i++)
                {
                    optList[i] = new None<T>();
                }
            }
            return new PSequence<T>(optList.FilterMap(x => x));
        }

        IPSequence<T> IPSequence<T>._WithInsertionAt(int index, IEnumerable<T> items)
        {
            var nlist = new List<T>(_list);
            nlist.InsertRange(index, items);
            return new PSequence<T>(nlist);
            
        }
        public override string ToString()
        {
            return $"PSeq[{string.Join(", ", Elements)}]";
        }
    }
    public class PStack<T>() : IPStack<T>
    {
        public IEnumerable<T> Elements => this.SubStacks().Map(x => x.TopValue.Unwrap());
        public int Count { get; private init; } = 0;
        public IOption<T> TopValue { get; private init; } = new None<T>();
        private readonly IOption<IPStack<T>> _link = new None<IPStack<T>>();

        private PStack(T value, IPStack<T> link, int height) : this()
        {
            TopValue = value.AsSome();
            _link = link.AsSome();
            Count = height;
        }

        private IOption<IPStack<T>> RecursiveAt(int index)
        {
            return (index < 0)
                ? this.AsNone()
                : (index == 0)
                    ? this.AsSome()
                    : _link.RemapAs(x => x.At(index - 1)).Press(); 
        }
        public IOption<IPStack<T>> At(int index)
        {
            if (index == 1) return _link;
            IOption<IPStack<T>> o = this.AsSome();
            while (index > 0 && o.Check(out var stack))
            {
                o = stack.At(1);
                index--;
            }
            return (index >= 0) ? o : new None<PStack<T>>();
        }

        IEntryAddable<T> IEntryAddable<T>._WithEntries(IEnumerable<T> entries)
        {
            var o = this;
            foreach(var (i, v) in entries.Enumerate()) o = new(v, o, Count + i+1);
            return o;
        }
        public override string ToString()
        {
            return $"PStack[{string.Join(", ", Elements)}]";
        }
    }

    public class CachingEnumerable<T> : IEnumerable<T>, IIndexReadable<int, T>
    {
        public bool IsFullyCached => _iter is null;
        public int CachedIndex => _cachedIndex;

        private IEnumerator<T>? _iter;
        private readonly List<T> _list;
        private int _cachedIndex = -1;

        public CachingEnumerable(IEnumerable<T> enumerable, int initialCapacity)
        {
            _iter = enumerable.GetEnumerator();
            _list = new(initialCapacity);
        }
        public CachingEnumerable(IEnumerable<T> enumerable)
        {
            _iter = enumerable.GetEnumerator();
            _list = new();
        }
        public int CountAndCache()
        {
            if (_iter is not null)
            {
                while (_iter.MoveNext()) _list.Add(_iter.Current);
                _list.TrimExcess();
                _iter = null;
                _cachedIndex = _list.Count;
            }
            return _list.Count;
        }
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; true; i++)
            {
                if (i <= _cachedIndex)
                {
                    yield return _list[i];
                }
                else if (_iter is not null && _iter.MoveNext())
                {
                    _cachedIndex++;
                    _list.Add(_iter.Current);
                    yield return _iter.Current;
                }
                else
                {
                    if (!IsFullyCached)
                    {
                        _iter = null;
                        _list.TrimExcess();
                    }
                    yield break;
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T At(int index)
        {
            while (_cachedIndex < index && _iter is not null)
            {
                if (!_iter.MoveNext()) throw new IndexOutOfRangeException();
                _cachedIndex++;
                _list.Add(_iter.Current);
            }
            return _list[index];
        }
    }

    public class RecursiveEvalTree<O, T>
    {
        public readonly T Evaluation;
        public readonly O Object;
        public readonly IOption<RecursiveEvalTree<O, T>[]> Branches;

        public RecursiveEvalTree(O root, Func<O, IResult<T, IEnumerable<O>>> resolveFunc, Func<IEnumerable<T>, T> combineFunc)
        {
            Object = root;
            if (resolveFunc(root).Split(out Evaluation, out var branches))
            {
                Branches = Branches.None();
            } 
            else
            {
                var branchArr =
                    branches.Map(obj => new RecursiveEvalTree<O, T>(obj, resolveFunc, combineFunc))
                    .ToArray();
                Branches = branchArr.AsSome();
                Evaluation = combineFunc(branchArr.Map(x => x.Evaluation));
            }
        }
    }
}