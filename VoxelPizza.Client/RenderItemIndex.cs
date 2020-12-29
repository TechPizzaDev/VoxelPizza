﻿using System;

namespace VoxelPizza.Client
{
    public struct RenderItemIndex : IComparable<RenderItemIndex>
    {
        public RenderOrderKey Key { get; }
        public int ItemIndex { get; }

        public RenderItemIndex(RenderOrderKey key, int itemIndex)
        {
            Key = key;
            ItemIndex = itemIndex;
        }

        public int CompareTo(RenderItemIndex other)
        {
            return Key.CompareTo(other.Key);
        }

        public override string ToString()
        {
            return string.Format("Index:{0}, Key:{1}", ItemIndex, Key);
        }
    }
}
