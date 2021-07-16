using System;
using VoxelPizza.Collections;

namespace VoxelPizza.Client
{
    public struct NonEmptyStoredChunkEnumerator : IRefEnumerator<StoredChunkMesh>
    {
        private StoredChunkMesh[] _storedChunks;
        private int _index;
        private int _offset;

        public ref StoredChunkMesh Current => ref _storedChunks[_index];

        public NonEmptyStoredChunkEnumerator(StoredChunkMesh[] storedChunks) 
        {
            _storedChunks = storedChunks ?? throw new ArgumentNullException(nameof(storedChunks));
            _index = -1;
            _offset = 0;
        }

        public bool MoveNext()
        {
            StoredChunkMesh[] storedChunks = _storedChunks;
            int i = _offset;
            for (; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh chunk = ref storedChunks[i];
                if (chunk.IsUploadRequired && !chunk.StoredMesh.IsEmpty)
                {
                    _index = i;
                    _offset = i + 1;
                    return true;
                }
            }
            _index = i;
            _offset = i;
            return false;
        }
    }
}
