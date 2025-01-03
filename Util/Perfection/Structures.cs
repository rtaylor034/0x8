using System.Collections.Generic;
using System.Collections;
using System;

#nullable disable
namespace Perfection
{
    /// <summary>
    /// <paramref name="original"/> is to be named 'Q' by convention.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="original"></param>
    /// <returns></returns>
    public delegate T Updater<T>(T original);
    public struct Empty<T> : IEnumerable<T>
    {
        public readonly IEnumerator<T> GetEnumerator()
        {
            yield break;
        }

        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            yield break;
        }
        public static IEnumerable<T> Yield()
        {
            yield break;
        }
    }
    // Shitty ass HashSet
    public record PSet<T> : PIndexedSet<T, T>
    {
        public PSet(float storageRatio = 1.3f) : base(x => x, storageRatio) { }
        public override string ToString() => _storage
            .AccumulateInto($"PSet:\n", (msg1, x) => msg1 +
        $"{x.AccumulateInto(">", (msg2, y) => msg2 + $" [{y}]\n  ")}\n");
    }
    /// <summary>
    /// TODO: make HAMT.
    /// </summary>
    /// <typeparam name="I"></typeparam>
    /// <typeparam name="T"></typeparam>
    public record PIndexedSet<I, T>
    {
        protected readonly List<List<T>> _storage;
        public required IEnumerable<T> Elements
        {
            get => _storage.Flatten(); init
            {
                var elementList = new List<T>(value);
                Count = elementList.Count;
                Modulo = Math.Max((int)(Count * _storageRatio), 1);
                _storage = new List<List<T>>(Modulo);
                _storage.AddRange(new List<T>(2).Sequence((_) => new(2)).Take(Modulo));
                foreach (var v in elementList)
                {
                    var bindex = IndexGenerator(v).GetHashCode().Abs() % Modulo;
                    var foundAt = _storage[bindex].FindIndex(x => IndexGenerator(v).Equals(IndexGenerator(x)));
                    if (foundAt == -1) _storage[bindex].Add(v);
                    else _storage[bindex][foundAt] = v;
                }
            }
        }
        public Updater<IEnumerable<T>> dElements { init => Elements = value(Elements); }
        public readonly int Modulo;
        public readonly int Count;
        public readonly Func<T, I> IndexGenerator;
        public PIndexedSet(Func<T, I> indexGenerator, float storageRatio = 1.3f)
        {
            _storageRatio = storageRatio;
            Modulo = 1;
            IndexGenerator = indexGenerator;
            Count = 0;
            _storage = new(0);
        }
        public bool Contains(I index) => GetBucket(index).HasMatch(x => IndexGenerator(x).Equals(index));
        public IOption<T> this[I index] => Count > 0 ? GetBucket(index).Find(x => IndexGenerator(x).Equals(index)).NullToNone() : new None<T>();
        public IOption<T> this[T obj] => this[IndexGenerator(obj)];
        private List<T> GetBucket(I index) => _storage[index.GetHashCode().Abs() % Modulo];
        public override string ToString() => _storage.AccumulateInto("PIndexedSet:\n", (msg1, x) => msg1 +
        $"{x.AccumulateInto(">", (msg2, y) => msg2 + $" [{IndexGenerator(y)} : {y}]\n  ")}\n");

        private readonly float _storageRatio;
    }
    // Shitty ass Dictionary
    /// <summary>
    /// TODO: make HAMT.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="T"></typeparam>
    public record PMap<K, T>
    {
        private readonly List<List<(K key, T val)>> _storage;
        public required IEnumerable<(K key, T val)> Elements
        {
            get => _storage.Flatten(); init
            {
                var elementList = new List<(K key, T val)>(value);
                Count = elementList.Count;
                Modulo = (int)(Count * _storageRatio);
                _storage = new List<List<(K key, T val)>>(Modulo);
                _storage.AddRange(new List<(K key, T val)>(2).Sequence((_) => new(2)).Take(Modulo));
                foreach (var v in elementList)
                {
                    var bindex = v.key.GetHashCode().Abs() % Modulo;
                    var foundAt = _storage[bindex].FindIndex(x => v.key.Equals(x.key));
                    if (foundAt == -1) _storage[bindex].Add(v);
                    else _storage[bindex][foundAt] = v;
                }
            }
        }
        public Updater<IEnumerable<(K key, T val)>> dElements { init => Elements = value(Elements); }
        public readonly int Modulo;
        public readonly int Count;
        public PMap(float storageRatio = 1.3f)
        {
            _storageRatio = storageRatio;
            Modulo = 0;
            Count = 0;
            _storage = new(0);
        }
        public IOption<T> this[K indexer] => GetBucket(indexer).FirstMatch(x => indexer.Equals(x.key)).RemapAs(x => x.val);
        private List<(K key, T val)> GetBucket(K element) => _storage[element.GetHashCode().Abs() % Modulo];
        public override string ToString() => Elements.AccumulateInto("PMap:\n", (msg, x) => msg + $"- [{x.key} : {x.val}]\n");

        private readonly float _storageRatio;
    }
    /// <summary>
    /// TODO: make efficient.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public record PList<T>
    {
        public required IEnumerable<T> Elements { get => _list; init => _list = new(value); }
        public Updater<IEnumerable<T>> dElements { init => Elements = value(Elements); }
        public int Count => _list.Count;
        public PList()
        {
            _list = new(0);
        }
        public IEnumerable<T> this[Range range] => _list[range];
        public T this[int index] => _list[index];
        public T[] ToArray() => _list.ToArray();
        public override string ToString() => Elements.AccumulateInto("PList:\n", (msg, x) => msg + $"- {x}\n");
        private readonly List<T> _list;
    }

    // DEV - bro, we need HAMTs bro. just trust me bro... bro... kiss me...
}