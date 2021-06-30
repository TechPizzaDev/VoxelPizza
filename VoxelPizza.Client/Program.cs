using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Veldrid.Sdl2;

namespace VoxelPizza.Client
{
    struct wrap : IEquatable<wrap>
    {
        public object field;

        public bool Equals(wrap other)
        {
            return object.ReferenceEquals(field, other.field);
        }

        public override int GetHashCode()
        {
            return field.GetHashCode();
        }
    }

    struct wrapbro : IEquatable<wrapbro>
    {
        public bro field;

        public bool Equals(wrapbro other)
        {
            return field == other.field;
        }

        public override int GetHashCode()
        {
            return field.GetHashCode();
        }
    }

    public sealed class bro : IEquatable<bro>
    {
        public int code;
        private int _refCount;

        public bro(int code)
        {
            this.code = base.GetHashCode();
        }

        public int Increment()
        {
            int ret = Interlocked.Increment(ref _refCount);
            return ret;
        }

        public bool Equals(bro? other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return code;
        }
    }

    public class Program
    {
        static unsafe void Main(string[] args)
        {
            /*
            int iters = 3000;
            int count = 12000;
            HashSet<int> ints = new(count);
            HashSet<long> longs = new(count);
            HashSet<object> objs = new(count);
            HashSet<wrap> wraps = new(count);
            HashSet<wrapbro> wrapbros = new(count);
            Dictionary<int, bro> wrapbrodict = new(count);

            int[] intar = new int[count];
            long[] longar = new long[count];
            object[] objar = new object[count];
            wrap[] wrapar = new wrap[count];
            wrapbro[] wrapbroar = new wrapbro[count];

            for (int i = 0; i < count; i++)
            {
                intar[i] = i;
                longar[i] = i;
                objar[i] = new object();
                wrapar[i] = new wrap { field = new object() };
                wrapbroar[i] = new wrapbro { field = new bro(HashCode.Combine(i)) };
            }

            for (int i = 0; i < count; i++)
            {
                ints.Add(intar[i]);
                longs.Add(longar[i]);
                objs.Add(objar[i]);
                wraps.Add(wrapar[i]);
                wrapbros.Add(wrapbroar[i]);
            }

            Stopwatch intwat = new Stopwatch();
            Stopwatch longwat = new Stopwatch();
            Stopwatch objwat = new Stopwatch();
            Stopwatch wrapwat = new Stopwatch();
            Stopwatch wrapbrowat = new Stopwatch();

            bool b = false;
            int h = 0;

            for (int j = 0; j < iters; j++)
            {
                bool intb = false;
                int inth = 0;
                intwat.Start();
                for (int i = 0; i < intar.Length - 1; i++)
                {
                    var x = intar[i];
                    var y = intar[i + 1];
                    intb = EqualityComparer<int>.Default.Equals(x, y);
                    inth += EqualityComparer<int>.Default.GetHashCode(x);
                    //inth += EqualityComparer<int>.Default.GetHashCode(y);
                }
                intwat.Stop();

                bool longb = false;
                int longh = 0;
                longwat.Start();
                for (int i = 0; i < longar.Length - 1; i++)
                {
                    var x = longar[i];
                    var y = longar[i + 1];
                    longb = EqualityComparer<long>.Default.Equals(x, y);
                    longh += EqualityComparer<long>.Default.GetHashCode(x);
                    //longh += EqualityComparer<long>.Default.GetHashCode(y);
                }
                longwat.Stop();

                bool objb = false;
                int objh = 0;
                objwat.Start();
                for (int i = 0; i < objar.Length - 1; i++)
                {
                    var x = objar[i];
                    var y = objar[i + 1];
                    objb = EqualityComparer<object>.Default.Equals(x, y);
                    objh += EqualityComparer<object>.Default.GetHashCode(x);
                    //objh += EqualityComparer<object>.Default.GetHashCode(y);
                }
                objwat.Stop();

                bool wrapb = false;
                int wraph = 0;
                wrapwat.Start();
                for (int i = 0; i < wrapar.Length - 1; i++)
                {
                    var x = wrapar[i];
                    var y = wrapar[i + 1];
                    wrapb = EqualityComparer<wrap>.Default.Equals(x, y);
                    wraph += EqualityComparer<wrap>.Default.GetHashCode(x);
                    //wraph += EqualityComparer<wrap>.Default.GetHashCode(y);
                }
                wrapwat.Stop();

                bool wrapbrob = false;
                int wrapbroh = 0;
                wrapbrowat.Start();
                for (int i = 0; i < wrapbroar.Length - 1; i++)
                {
                    var x = wrapbroar[i];
                    var y = wrapbroar[i + 1];
                    wrapbrob = EqualityComparer<wrapbro>.Default.Equals(x, y);
                    wrapbroh += EqualityComparer<wrapbro>.Default.GetHashCode(x);
                    //wrapbroh += EqualityComparer<wrap>.Default.GetHashCode(y);
                }
                wrapbrowat.Stop();

                b = intb | longb | objb | wrapb | wrapbrob;
                h += inth + longh + objh + wraph + wrapbroh;
            }

            Console.WriteLine(b + " - " + h);
            Console.WriteLine(intwat.ElapsedMilliseconds);
            Console.WriteLine(longwat.ElapsedMilliseconds);
            Console.WriteLine(objwat.ElapsedMilliseconds);
            Console.WriteLine(wrapwat.ElapsedMilliseconds);
            Console.WriteLine(wrapbrowat.ElapsedMilliseconds);

            intwat.Reset();
            longwat.Reset();
            objwat.Reset();
            wrapwat.Reset();
            wrapbrowat.Reset();
            Console.WriteLine();

            for (int j = 0; j < iters; j++)
            {
                intwat.Start();
                for (int i = 0; i < intar.Length; i++)
                {
                    ints.Add(intar[i]);
                }
                intwat.Stop();

                longwat.Start();
                for (int i = 0; i < longar.Length; i++)
                {
                    longs.Add(longar[i]);
                }
                longwat.Stop();

                objwat.Start();
                for (int i = 0; i < objar.Length; i++)
                {
                    objs.Add(objar[i]);
                }
                objwat.Stop();

                wrapwat.Start();
                for (int i = 0; i < wrapar.Length; i++)
                {
                    wraps.Add(wrapar[i]);
                }
                wrapwat.Stop();

                wrapbrowat.Start();
                for (int i = 0; i < wrapbroar.Length; i++)
                {
                    wrapbros.Add(wrapbroar[i]);
                }
                wrapbrowat.Stop();
            }

            Console.WriteLine(intwat.ElapsedMilliseconds);
            Console.WriteLine(longwat.ElapsedMilliseconds);
            Console.WriteLine(objwat.ElapsedMilliseconds);
            Console.WriteLine(wrapwat.ElapsedMilliseconds);
            Console.WriteLine(wrapbrowat.ElapsedMilliseconds);

            intwat.Reset();
            longwat.Reset();
            objwat.Reset();
            wrapwat.Reset();
            wrapbrowat.Reset();
            Console.WriteLine();

            ints.Clear();
            longs.Clear();
            objs.Clear();
            wraps.Clear();
            wrapbros.Clear();

            for (int j = 0; j < iters; j++)
            {
                intwat.Start();
                for (int i = 0; i < intar.Length; i++)
                {
                    ints.Add(intar[i]);
                }
                ints.Clear();
                intwat.Stop();

                longwat.Start();
                for (int i = 0; i < longar.Length; i++)
                {
                    longs.Add(longar[i]);
                }
                longs.Clear();
                longwat.Stop();

                objwat.Start();
                for (int i = 0; i < objar.Length; i++)
                {
                    objs.Add(objar[i]);
                }
                objs.Clear();
                objwat.Stop();

                wrapwat.Start();
                for (int i = 0; i < wrapar.Length; i++)
                {
                    wraps.Add(wrapar[i]);
                }
                wraps.Clear();
                wrapwat.Stop();

                wrapbrowat.Start();
                for (int i = 0; i < wrapbroar.Length; i++)
                {
                    wrapbros.Add(wrapbroar[i]);
                }
                wrapbros.Clear();
                wrapbrowat.Stop();
            }

            Console.WriteLine(intwat.ElapsedMilliseconds);
            Console.WriteLine(longwat.ElapsedMilliseconds);
            Console.WriteLine(objwat.ElapsedMilliseconds);
            Console.WriteLine(wrapwat.ElapsedMilliseconds);
            Console.WriteLine(wrapbrowat.ElapsedMilliseconds);
            return;
            */

            SDL_version version;
            Sdl2Native.SDL_GetVersion(&version);

            var app = new VoxelPizza();
            app.Run();
        }
    }
}
