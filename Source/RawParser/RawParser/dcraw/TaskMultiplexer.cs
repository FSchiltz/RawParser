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
using System.Threading.Tasks;

namespace dcraw
{
    /// <summary>
    /// Simple multithreading library.  Didn't want dependency on an external library.
    /// </summary>
    public class TaskMultiplexer : IDisposable
    {
        private Queue<Task> workQueue = new Queue<Task>();

        public void QueueWorkItem(Task work)
        {
            lock (workQueue)
            {
                workQueue.Enqueue(work);
            }
        }

        public void Dispose()
        {
            // Start threads to do work
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                Task t = new Task(delegate ()
                {
                    while (true)
                    {
                        Task work;
                        lock (workQueue)
                        {
                            if (workQueue.Count == 0)
                            {
                                return;
                            }
                            work = workQueue.Dequeue();
                        }
                        work.RunSynchronously();
                    }
                });
                t.Start();
            }
            // Wait for queue to be emptied
            while (true)
            {
                lock (workQueue)
                {
                    if (workQueue.Count == 0)
                    {
                        return;
                    }
                }
                Task.Delay(10);
            }
        }
    }
}
