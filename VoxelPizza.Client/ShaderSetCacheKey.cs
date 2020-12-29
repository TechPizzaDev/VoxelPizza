using System;
using Veldrid;

namespace VoxelPizza.Client
{
    public struct ShaderSetCacheKey : IEquatable<ShaderSetCacheKey>
    {
        public string Name { get; }
        public SpecializationConstant[] Specializations { get; }

        public ShaderSetCacheKey(string name, SpecializationConstant[] specializations)
        {
            Name = name;
            Specializations = specializations;
        }

        public bool Equals(ShaderSetCacheKey other)
        {
            return Name.Equals(other.Name) 
                && ArraysEqual(Specializations, other.Specializations);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            foreach (var specConst in Specializations)
                hash.Add(specConst);
            return hash.ToHashCode();
        }

        private static bool ArraysEqual<T>(T[] a, T[] b)
            where T : IEquatable<T>
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }

            return true;
        }
    }
}
