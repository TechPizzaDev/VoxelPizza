using System;
using System.Numerics;

namespace VoxelPizza.Client
{
    public class Transform
    {
        private Vector3 _position;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;

        public Vector3 Position { get => _position; set { _position = value; TransformChanged?.Invoke(this); } }
        public Quaternion Rotation { get => _rotation; set { _rotation = value; TransformChanged?.Invoke(this); } }
        public Vector3 Scale { get => _scale; set { _scale = value; TransformChanged?.Invoke(this); } }

        public event Action<Transform>? TransformChanged;

        public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, _rotation);

        public void Set(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            _position = position;
            _rotation = rotation;
            _scale = scale;
            TransformChanged?.Invoke(this);
        }

        public Matrix4x4 GetTransformMatrix()
        {
            return Matrix4x4.CreateScale(_scale)
                * Matrix4x4.CreateFromQuaternion(_rotation)
                * Matrix4x4.CreateTranslation(Position);
        }
    }
}
