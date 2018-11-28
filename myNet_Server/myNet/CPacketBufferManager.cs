using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace myNet
{
    public class CPacketBufferManager
    {
        static object cs_buffer = new object();     // lock용 object
        static Stack<CPacket> pool;
        static int pool_capacity;

        public static void initialize(int capacity)
        {
            pool = new Stack<CPacket>();
            pool_capacity = capacity;
            allocate();
        }

        static void allocate()
        {
            for(int i = 0; i < pool_capacity; ++i)
            {
                pool.Push(new CPacket());
            }
        }

        public static CPacket pop()
        {
            lock (cs_buffer)
            {
                if(pool.Count <= 0)
                {
                    Console.WriteLine("Packet pool reallocate.");
                    allocate();
                }

                return pool.Pop();
            }
        }

        public static void push(CPacket packet)
        {
            lock (cs_buffer)
            {
                pool.Push(packet);
            }
        }
    }
}
