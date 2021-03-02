using System;
using Veldrid;

namespace VoxelPizza.Client
{
    public struct ShaderSetCacheKey : IEquatable<ShaderSetCacheKey>
    {
        public string VertexName { get; }
        public string FragmentName { get; }
        public ReadOnlyMemory<SpecializationConstant> Specializations { get; }

        public ShaderSetCacheKey(string vertexName, string fragmentName, ReadOnlySpan<SpecializationConstant> specializations)
        {
            VertexName = vertexName;
            FragmentName = fragmentName;
            Specializations = specializations.ToArray();
        }

        public bool Equals(ShaderSetCacheKey other)
        {
            return VertexName.Equals(other.VertexName)
                && FragmentName.Equals(other.FragmentName)
                && Specializations.Span.SequenceEqual(other.Specializations.Span);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(VertexName);
            hash.Add(FragmentName);
            foreach (var specConst in Specializations.Span)
                hash.Add(specConst);
            return hash.ToHashCode();
        }
    }
}
