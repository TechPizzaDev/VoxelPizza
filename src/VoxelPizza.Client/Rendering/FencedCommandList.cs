using System;
using Veldrid;

namespace VoxelPizza.Client
{
    public readonly struct FencedCommandList : IEquatable<FencedCommandList>, IDisposable
    {
        public CommandList CommandList { get; }
        public Fence Fence { get; }

        public FencedCommandList(CommandList commandList, Fence fence)
        {
            CommandList = commandList ?? throw new ArgumentNullException(nameof(commandList));
            Fence = fence ?? throw new ArgumentNullException(nameof(fence));
        }

        public bool Equals(FencedCommandList other)
        {
            return CommandList == other.CommandList
                && Fence == other.Fence;
        }

        public override bool Equals(object? obj)
        {
            return obj is FencedCommandList other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CommandList, Fence);
        }

        public void Dispose()
        {
            CommandList?.Dispose();
            Fence?.Dispose();
        }

        public static bool operator ==(FencedCommandList left, FencedCommandList right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FencedCommandList left, FencedCommandList right)
        {
            return !(left == right);
        }
    }
}
