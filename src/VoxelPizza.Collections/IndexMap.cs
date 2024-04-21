namespace VoxelPizza.Collections;

public class IndexMap<T>
{
    private SortedList<T> _map;

    public IndexMap()
    {
        _map = new SortedList<T>();
    }

    public int IndexForValue(T value)
    {
        int index = _map.BinarySearch(value);
        if (index < 0)
        {
            return -1;
        }
        return index;
    }

    public T ValueForIndex(int index)
    {
        return _map[index];
    }

    public bool Add(T value)
    {
        return _map.TryAdd(value);
    }

    public bool Remove(T value)
    {
        return _map.Remove(value);
    }

    public bool Contains(T value)
    {
        return _map.Contains(value);
    }
}
