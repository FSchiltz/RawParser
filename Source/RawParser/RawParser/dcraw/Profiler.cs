// dcraw.net - camera raw file decoder
// Copyright (C) 1997-2008  Dave Coffin, dcoffin a cybercom o net
// Copyright (C) 2008-2009  Sam Webster, Dave Brown
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace dcraw
{
    internal class Profiler
    {
        private static readonly Dictionary<string, long> times = new Dictionary<string, long>();
        
        internal static IDisposable BlockProfile(string blockName)
        {
            return new ProfileBlock(blockName);
        }

        private class ProfileBlock : IDisposable
        {
            private readonly Stopwatch sw;
            private readonly string blockName;

            internal ProfileBlock(string blockName)
            {
                sw = Stopwatch.StartNew();
                this.blockName = blockName;
            }

            public void Dispose()
            {
                sw.Stop();
                AddTime(blockName, sw.ElapsedTicks);
            }
        }

        public static void DumpStats()
        {
            Console.WriteLine();
            Console.WriteLine("Timing information:");
            lock (times)
            {
                foreach (string s in times.Keys)
                {
                    long val = times[s];

                    Console.WriteLine("{0} : {1}ms", s, (val*1000)/Stopwatch.Frequency);
                }
            }
        }

        public static void AddTime(string name, long milliseconds)
        {
            lock(times)
            {
                long oldTime;
                times.TryGetValue(name, out oldTime);

                oldTime += milliseconds;

                times[name] = oldTime;
            }
        }
    }
}
