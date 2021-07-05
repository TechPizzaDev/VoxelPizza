using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoxelPizza.Diagnostics
{
    public class Profiler
    {
        public class FrameSet
        {
            public List<Item> Items = new();
            public int Offset;
        }

        public struct Item
        {
            public int ParentOffset;
            public TimeSpan TimeOfPush;
            public TimeSpan TimeOfPop;

            public StackFrame? Frame;
            public string MemberName;
            public string FilePath;
            public int LineNumber;

            public readonly TimeSpan Duration => TimeOfPop - TimeOfPush;
        }

        public List<FrameSet> _sets = new();
        private int _index;
        private Stopwatch _watch = new();

        public bool IsRecording => _watch.IsRunning;

        public void Push(
            string memberName,
            string filePath,
            int lineNumber)
        {
            TimeSpan timeOfPush = _watch.Elapsed;

            if (_sets.Count <= _index)
            {
                _sets.Add(new FrameSet());
            }
            else
            {
                _sets[_index].Offset++;
            }

            int parentOffset = 0;
            if (_index > 0)
            {
                parentOffset = _sets[_index - 1].Offset;
            }

            Item item;
            item.ParentOffset = parentOffset;
            item.TimeOfPush = timeOfPush;
            item.TimeOfPop = default;

            item.Frame = default;
            item.MemberName = memberName;
            item.FilePath = filePath;
            item.LineNumber = lineNumber;

            _sets[_index].Items.Add(item);
            _index++;
        }

        public void Pop()
        {
            TimeSpan timeOfPop = _watch.Elapsed;

            _index--;
            CollectionsMarshal.AsSpan(_sets[_index].Items)[^1].TimeOfPop = timeOfPop;
        }

        public void Start()
        {
            _watch.Start();
        }

        public void Stop()
        {
            _watch.Stop();
        }

        public void Clear()
        {
            foreach (FrameSet set in _sets)
            {
                set.Items.Clear();
                set.Offset = -1;
            }
            _watch.Reset();
        }
    }
}
