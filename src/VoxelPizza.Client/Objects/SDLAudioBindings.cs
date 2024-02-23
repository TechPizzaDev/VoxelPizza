using System;
using System.Runtime.InteropServices;
using Veldrid.Sdl2;

namespace VoxelPizza.Client
{
    public delegate void SDL_PauseAudioDevice(uint device, int pause_on);

    public delegate void SDL_CloseAudioDevice(uint device);

    public delegate uint SDL_OpenAudioDevice(
        IntPtr device,
        int iscapture,
        ref SDL_AudioSpec desired, //SDL_AudioSpec
        ref SDL_AudioSpec obtained, //SDL_AudioSpec
        SDL_AllowedAudioChanges allowed_changes);

    // userdata refers to a void*, stream to a Uint8
    public delegate void SDL_AudioCallback(
        IntPtr userdata,
        IntPtr stream,
        int len);

    public static unsafe class SDLAudioBindings
    {
        public static void LoadFunctions()
        {
            OpenAudioDevice = Sdl2Native.LoadFunction<SDL_OpenAudioDevice>("SDL_OpenAudioDevice");
            PauseAudioDevice = Sdl2Native.LoadFunction<SDL_PauseAudioDevice>("SDL_PauseAudioDevice");
            CloseAudioDevice = Sdl2Native.LoadFunction<SDL_CloseAudioDevice>("SDL_CloseAudioDevice");
        }

        public static SDL_OpenAudioDevice OpenAudioDevice;
        public static SDL_PauseAudioDevice PauseAudioDevice;
        public static SDL_CloseAudioDevice CloseAudioDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_AudioSpec
    {
        public int freq;
        public SDL_AudioFormat format; // SDL_AudioFormat
        public byte channels;
        public byte silence;
        public ushort samples;
        public uint size;
        public SDL_AudioCallback callback;
        public IntPtr userdata; // void*
    }

    public enum SDL_AudioFormat : ushort
    {
        AUDIO_U8 = 0x0008,
        AUDIO_S8 = 0x8008,
        AUDIO_U16LSB = 0x0010,
        AUDIO_S16LSB = 0x8010,
        AUDIO_U16MSB = 0x1010,
        AUDIO_S16MSB = 0x9010,
        AUDIO_U16 = AUDIO_U16LSB,
        AUDIO_S16 = AUDIO_S16LSB,
        AUDIO_S32LSB = 0x8020,
        AUDIO_S32MSB = 0x9020,
        AUDIO_S32 = AUDIO_S32LSB,
        AUDIO_F32LSB = 0x8120,
        AUDIO_F32MSB = 0x9120,
        AUDIO_F32 = AUDIO_F32LSB,
    }

    [Flags]
    public enum SDL_AllowedAudioChanges
    {
        FREQUENCY = 0x00000001,
        FORMAT = 0x00000002,
        CHANNELS = 0x00000004,
        SAMPLES = 0x00000008,
        ANY = (FREQUENCY | FORMAT | CHANNELS | SAMPLES)
    }
}
