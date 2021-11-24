using System;
using System.IO;
using System.Runtime.InteropServices;
using LoudPizza;

namespace VoxelPizza.Client
{
    public class AudioBackend
    {

    }

    public unsafe class Sdl2Backend : AudioBackend
    {
        public SDL_AudioSpec gActiveAudioSpec;
        public uint gAudioDeviceID;

        private SDL_AudioCallback audioCallback;

        public SoLoud SoLoud { get; }

        public Sdl2Backend(SoLoud soloud)
        {
            SoLoud = soloud ?? throw new ArgumentNullException(nameof(soloud));
        }

        public SOLOUD_ERRORS init(uint aSamplerate = 48000, uint aBufferSize = 2048, uint aChannels = 0)
        {
            //if (!SDL_WasInit(SDL_INIT_AUDIO))
            //{
            //    if (SDL_InitSubSystem(SDL_INIT_AUDIO) < 0)
            //    {
            //        return SOLOUD_ERRORS.UNKNOWN_ERROR;
            //    }
            //}

            SDL_AudioSpec spec;
            spec.silence = default;
            spec.userdata = default;
            spec.size = default;

            spec.freq = (int)aSamplerate;
            spec.format = SDL_AudioFormat.AUDIO_F32;
            spec.channels = (byte)aChannels;
            spec.samples = (ushort)aBufferSize;

            audioCallback = soloud_sdl2static_audiomixer;
            spec.callback = audioCallback;

            var flags = SDL_AllowedAudioChanges.ANY & ~(SDL_AllowedAudioChanges.FORMAT | SDL_AllowedAudioChanges.CHANNELS);
            gAudioDeviceID = SDLAudioBindings.OpenAudioDevice(IntPtr.Zero, 0, ref spec, ref gActiveAudioSpec, flags);
            if (gAudioDeviceID == 0)
            {
                spec.format = SDL_AudioFormat.AUDIO_S16;
                gAudioDeviceID = SDLAudioBindings.OpenAudioDevice(IntPtr.Zero, 0, ref spec, ref gActiveAudioSpec, flags);
            }

            if (gAudioDeviceID == 0)
            {
                return SOLOUD_ERRORS.UNKNOWN_ERROR;
            }

            SoLoud.postinit_internal((uint)gActiveAudioSpec.freq, gActiveAudioSpec.samples, SoLoud.mFlags, gActiveAudioSpec.channels);

            SoLoud.mBackendCleanupFunc = soloud_sdl2_deinit;
            SoLoud.mBackendString = "SDL2";

            SDLAudioBindings.PauseAudioDevice(gAudioDeviceID, 0); // start playback

            return SOLOUD_ERRORS.SO_NO_ERROR;
        }

        private void soloud_sdl2static_audiomixer(IntPtr userdata, IntPtr stream, int len)
        {
            short* buf = (short*)stream;
            if (gActiveAudioSpec.format == SDL_AudioFormat.AUDIO_F32)
            {
                int samples = len / (gActiveAudioSpec.channels * sizeof(float));
                SoLoud.mix((float*)buf, (uint)samples);
            }
            else // assume s16 if not float
            {
                int samples = len / (gActiveAudioSpec.channels * sizeof(short));
                SoLoud.mixSigned16(buf, (uint)samples);
            }
        }

        private void soloud_sdl2_deinit(SoLoud aSoloud)
        {
            SDLAudioBindings.CloseAudioDevice(gAudioDeviceID);
        }
    }

    public unsafe class AudioTest
    {
        public SoLoud soloud;
        public Sdl2Backend backend;

        public Handle voicehandle;

        public void Run()
        {
            soloud = new SoLoud();
            backend = new Sdl2Backend(soloud);
            backend.init();

            //soloud.postinit_internal(48000, 2048, default, 2);
            //float* buffer = stackalloc float[4096];

            //int length = 10000;
            //float* noise = stackalloc float[length];
            //for (int i = 0; i < length; i++)
            //{
            //    noise[i] = MathF.Sin(i / (float)length * MathF.PI * 2);
            //}
            ////Span<short> noiseSpan = new Span<short>(noise, 44100);
            //wav.loadRawWave(noise, (uint)length, 10000, 1);

            AudioBuffer wav = new AudioBuffer();
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes("black apple.raw");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                bytes = Array.Empty<byte>();
            }
            float[] next = new float[bytes.Length / 4];
            fixed (byte* srcPtr = bytes)
            fixed (float* dst = next)
            {
                float* src = (float*)srcPtr;
                float* dst1 = (float*)dst;
                float* dst2 = (float*)dst + (next.Length / 2);

                for (int i = 0; i < next.Length / 2; i++)
                {
                    dst1[i] = src[i * 2 + 0];
                    dst2[i] = src[i * 2 + 1];
                }

                wav.loadRawWave((float*)dst, (uint)next.Length, 44100, 2, false);
            }

            AudioSource asrc = wav;

            try
            {
                asrc = new Mp3Stream(File.OpenRead("streamtest.mp3"), false);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }

            asrc.set3dAttenuator(LinearDistanceAudioAttenuator.Instance);

            //uint voiceHandle = soloud.play(asrc, 1, 0, false);
            Handle voiceHandle = soloud.play3d(asrc, 0, 0, 0);
            voicehandle = voiceHandle;

            Handle groupHandle = soloud.createVoiceGroup();
            soloud.addVoiceToGroup(groupHandle, voiceHandle);

            soloud.setVolume(groupHandle, 0.05f);
            soloud.setLooping(groupHandle, true);
            soloud.setRelativePlaySpeed(groupHandle, 4f);

            //soloud.set3dSourceMinMaxDistance(groupHandle, 1, 100);

            //soloud.setPan(voiceHandle, -1f);
            //soloud.oscillateRelativePlaySpeed(groupHandle, 0.8f, 1.2f, 0.5f);
            //soloud.oscillatePan(voiceHandle, -1, 1, 1);

            //while (true)
            //{
            //    soloud.mix(buffer, 2048);
            //}

            //Span<float> bufferSpan = new Span<float>(buffer, 4096);

        }
    }
}
