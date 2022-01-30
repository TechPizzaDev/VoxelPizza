using Veldrid.Sdl2;

namespace VoxelPizza.Client
{
    public class Program
    {
        private static unsafe void Main(string[] args)
        {
            SDL_version version;
            Sdl2Native.SDL_GetVersion(&version);

            using VoxelPizza app = new();
            app.Run();
        }
    }
}
