using System.Runtime.CompilerServices;

namespace VoxelPizza.Diagnostics
{
    public static class ProfilerExtensions
    {
        public static ProfilerPopToken Push(
            this Profiler? profiler,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (profiler != null && profiler.IsRecording)
            {
                profiler.Push(memberName, filePath, lineNumber);
            }
            return new ProfilerPopToken(profiler);
        }
    }
}
