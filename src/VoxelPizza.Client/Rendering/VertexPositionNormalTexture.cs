using System.Numerics;

namespace VoxelPizza.Client
{
    public readonly struct VertexPositionNormalTexture
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Vector2 Texture;

        public VertexPositionNormalTexture(Vector3 position, Vector3 normal, Vector2 texture)
        {
            Position = position;
            Normal = normal;
            Texture = texture;
        }
    }
}