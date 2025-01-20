using System.Collections.Generic;
using System.Collections;
using System;

#nullable enable
namespace Perfection
{
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
    public interface IMergable<U> where U : IMergable<U>
    {
        public U _Merge(U union);
    }
    public interface IMap<K, T> : IHasElements<ITiple<K, T>>, IEntryAddable<ITiple<K, T>>, IEntryRemovable<K>, IIndexReadable<K, IOption<T>>, IMergable<IMap<K, T>>
    { }
    public interface ISet<T> : IHasElements<T>, IEntryAddable<T>, IEntryRemovable<T>, IIndexReadable<T, bool>, IMergable<ISet<T>>
    { }
    public interface ISequence<T> : IHasElements<T>, IIndexReadable<int, T>, IEntryAddable<ITiple<int, T>>, IEntryAddable<T>, IMergable<ISequence<T>>, IEntryRemovable<Range>
    {
        public ISequence<T> _WithInsertionAt(int index, IEnumerable<T> items);
    }

    public static class SelfTypeAssumption
    {
        public static Self WithEntries<Self, T>(this Self s, IEnumerable<T> values) where Self : IEntryAddable<T>
        { return (Self)s._WithEntries(values); }
        public static Self WithoutEntries<Self, T>(this Self s, IEnumerable<T> values) where Self : IEntryRemovable<T>
        { return (Self)s._WithoutEntries(values); }
        public static Self Merge<Self, T>(this Self s, Self other) where Self : IMergable<Self>
        { return s._Merge(other); }
        public static Self WithInsertionAt<Self, T>(this Self s, int index, IEnumerable<T> values) where Self : ISequence<T>
        { return (Self)s._WithInsertionAt(index, values); }
    }

    // DEV/FIXME: temporary bare-functionality inneficient implementations.

    public class PMap<K, T>() : IMap<K, T> where K : notnull
    {
        private readonly Dictionary<K, T> _dict = new();
        public int Count => _dict.Count;

        public IEnumerable<ITiple<K, T>> Elements => _dict.Map(x => (x.Key, x.Value).Tiple());

        private PMap(Dictionary<K, T> dict) : this() { _dict = dict; }
        public IOption<T> At(K index)
        {
            return _dict.TryGetValue(index, out var v).ToOption(v)!;
        }

        IMap<K, T> IMergable<IMap<K, T>>._Merge(IMap<K, T> union)
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
    }
    public class PSet<T>() : ISet<T>
    {
        private readonly HashSet<T> _set = new();
        public int Count => _set.Count;

        public IEnumerable<T> Elements => _set;

        private PSet(HashSet<T> set) : this() { _set = set; }
        public bool At(T index)
        {
            return _set.Contains(index);
        }

        ISet<T> IMergable<ISet<T>>._Merge(ISet<T> union)
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
    }
    public class PSequence<T>() : ISequence<T>
    {
        private readonly CachingEnumerable<T> _list = new([]);
        public int Count => _list.CountAndCache();

        public IEnumerable<T> Elements => _list;

        public T At(int index)
        {
            return _list.At(index);
        }

        private PSequence(IEnumerable<T> values) : this() { _list = new(values); }
        ISequence<T> IMergable<ISequence<T>>._Merge(ISequence<T> union)
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

        ISequence<T> ISequence<T>._WithInsertionAt(int index, IEnumerable<T> items)
        {
            var nlist = new List<T>(_list);
            nlist.InsertRange(index, items);
            return new PSequence<T>(nlist);
            
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
}