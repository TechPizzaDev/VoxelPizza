using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelPizza.Client
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexPosition<T> : IEquatable<VertexPosition<T>>
        where T : unmanaged, IEquatable<T>
    {
        public Vector3 Position;
        public T Data;

        public bool Equals(VertexPosition<T> other)
        {
            return Position.Equals(other.Position) && Data.Equals(other.Data);
        }
    }
}
