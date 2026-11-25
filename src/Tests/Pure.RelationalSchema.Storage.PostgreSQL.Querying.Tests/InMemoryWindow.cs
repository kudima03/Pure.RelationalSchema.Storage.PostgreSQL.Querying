namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The window of a row computed in memory as PostgreSQL defines it, which a query of
/// <c>PostgreSqlWindows.Over</c> over stored tables is compared with: the rows of
/// the row's partition in the order of the window, the row's position among them
/// and its peers, the rows equal to it on every key of the order.
/// </summary>
internal sealed record InMemoryWindow<T>
{
    private readonly IReadOnlyList<T> _partition;

    private readonly int _index;

    private readonly int _firstPeer;

    private readonly int _lastPeer;

    private readonly int _peerGroup;

    private readonly bool _ordered;

    private InMemoryWindow(
        IReadOnlyList<T> partition,
        int index,
        Func<T, object?> peerKey,
        bool ordered
    )
    {
        _partition = partition;
        _index = index;
        _ordered = ordered;
        object? key = peerKey(partition[index]);
        _firstPeer = index;
        while (_firstPeer > 0 && Equals(peerKey(partition[_firstPeer - 1]), key))
        {
            _firstPeer--;
        }
        _lastPeer = index;
        while (
            _lastPeer < partition.Count - 1 && Equals(peerKey(partition[_lastPeer + 1]), key)
        )
        {
            _lastPeer++;
        }
        _peerGroup = 1;
        for (int position = 1; position <= _firstPeer; position++)
        {
            if (!Equals(peerKey(partition[position]), peerKey(partition[position - 1])))
            {
                _peerGroup++;
            }
        }
    }

    public long RowNumber => _index + 1;

    public long Rank => _ordered ? _firstPeer + 1 : 1;

    public long DenseRank => _ordered ? _peerGroup : 1;

    public double PercentRank =>
        _partition.Count > 1 ? (Rank - 1) / (double)(_partition.Count - 1) : 0;

    public double CumeDist =>
        (_ordered ? _lastPeer + 1 : _partition.Count) / (double)_partition.Count;

    /// <summary>
    /// The rows of the default frame: with an order, from the first row of the
    /// partition to the last peer of the row; without, the partition.
    /// </summary>
    public IEnumerable<T> Peers =>
        _ordered ? _partition.Take(_lastPeer + 1) : _partition;

    /// <summary>
    /// The rows of each row of the source: its partition, by the partition key, in
    /// the order of the window, and the row's window there. Without an order the
    /// partition keeps the order of the source and every row is a peer.
    /// </summary>
    public static IEnumerable<(T Row, InMemoryWindow<T> Window)> Over(
        IEnumerable<T> source,
        Func<T, object?> partitionBy,
        Func<IEnumerable<T>, IOrderedEnumerable<T>>? orderBy,
        Func<T, object?> peerKey
    )
    {
        foreach (IGrouping<object?, T> group in source.GroupBy(partitionBy))
        {
            T[] partition = orderBy is null ? [.. group] : [.. orderBy(group)];
            for (int index = 0; index < partition.Length; index++)
            {
                yield return (
                    partition[index],
                    new InMemoryWindow<T>(
                        partition,
                        index,
                        orderBy is null ? _ => null : peerKey,
                        orderBy is not null
                    )
                );
            }
        }
    }

    /// <summary>
    /// NTILE: the partition divided into the buckets as equally as possible, the
    /// first buckets one row larger.
    /// </summary>
    public int Ntile(int buckets)
    {
        int count = _partition.Count;
        if (buckets >= count)
        {
            return _index + 1;
        }
        int size = count / buckets;
        int larger = count % buckets;
        int inLarger = larger * (size + 1);
        return _index < inLarger
            ? (_index / (size + 1)) + 1
            : larger + ((_index - inLarger) / size) + 1;
    }

    public TValue Lag<TValue>(Func<T, TValue> value, int offset, TValue defaultValue)
    {
        int position = _index - offset;
        return position >= 0 && position < _partition.Count
            ? value(_partition[position])
            : defaultValue;
    }

    public TValue Lead<TValue>(Func<T, TValue> value, int offset, TValue defaultValue)
    {
        return Lag(value, -offset, defaultValue);
    }

    /// <summary>
    /// The rows of a <c>ROWS</c> frame: from <paramref name="start"/> rows to
    /// <paramref name="end"/> rows from the row (negative before it), unbounded on a
    /// side that is null.
    /// </summary>
    public IEnumerable<T> Rows(long? start, long? end)
    {
        long first = start is long before ? Math.Max(0, _index + before) : 0;
        long last = end is long after
            ? Math.Min(_partition.Count - 1, _index + after)
            : _partition.Count - 1;
        for (long position = first; position <= last; position++)
        {
            yield return _partition[(int)position];
        }
    }

    /// <summary>
    /// The rows of a <c>RANGE</c> frame over the one sort key: those whose key,
    /// given with the key of the row, is within the frame of the row.
    /// </summary>
    public IEnumerable<T> Range<TKey>(Func<T, TKey> key, Func<TKey, TKey, bool> within)
    {
        TKey current = key(_partition[_index]);
        return _partition.Where(row => within(key(row), current));
    }
}
