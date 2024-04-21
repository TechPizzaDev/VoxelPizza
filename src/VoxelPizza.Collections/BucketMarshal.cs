
namespace VoxelPizza.Collections;

public static class BucketMarshal
{
    /// <inheritdoc cref="BucketDict{TKey, TValue}.CollectionsMarshalHelper.GetValueRefOrAddDefault"/>
    public static ref TValue? GetValueRefOrAddDefault<TKey, TValue>(BucketDict<TKey, TValue> dictionary, TKey key, out bool exists)
        where TKey : notnull
    {
        return ref BucketDict<TKey, TValue>.CollectionsMarshalHelper.GetValueRefOrAddDefault(dictionary, key, out exists);
    }
}
