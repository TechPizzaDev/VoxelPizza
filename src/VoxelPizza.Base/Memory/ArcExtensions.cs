namespace VoxelPizza.Memory;

public static class ArcExtensions
{
    public static ValueArc<T> Wrap<T>(this Arc<T> arc)
        where T : IDestroyable
    {
        return new ValueArc<T>(arc);
    }

    public static ValueArc<T> Wrap<T>(this ValueArc<T> arc)
        where T : IDestroyable
    {
        return arc;
    }

    /// <inheritdoc cref="ValueArc{T}.Increment"/>
    public static ValueArc<T> Track<T>(this Arc<T> arc)
        where T : IDestroyable
    {
        arc.Increment();
        return new ValueArc<T>(arc);
    }

    /// <inheritdoc cref="ValueArc{T}.Increment"/>
    public static ValueArc<T> Track<T>(this ValueArc<T> arc)
        where T : IDestroyable
    {
        arc.Increment();
        return arc;
    }

    public static ValueArc<T> TryTrack<T>(this Arc<T>? arc)
        where T : IDestroyable
    {
        if (arc != null && arc.TryIncrement())
        {
            return new ValueArc<T>(arc);
        }
        return ValueArc<T>.Empty;
    }

    public static ValueArc<T> TryTrack<T>(this ValueArc<T> arc)
        where T : IDestroyable
    {
        if (arc.TryIncrement())
        {
            return arc;
        }
        return ValueArc<T>.Empty;
    }
}
