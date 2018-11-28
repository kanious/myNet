using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace myNet
{
    // MSDN의 샘플 코드를 그대로 사용
    class SocketAsyncEventArgsPool
    {
        Stack<SocketAsyncEventArgs> m_pool;

        public SocketAsyncEventArgsPool(int capacity)
        {
            m_pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        public void Push(SocketAsyncEventArgs item)
        {
            if (null == item)
            {
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null.");
            }

            // lock문
            // 한 번에 하나의 스레드만 lock 블럭 실행
            // 매개변수에 임의의 객체를 사용할 수 있다.
            // lock 내부의 코드 블럭 = Critical Section
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }

        public SocketAsyncEventArgs Pop()
        {
            lock (m_pool)
            {
                return m_pool.Pop();
            }
        }

        public int Count
        {
            get { return m_pool.Count; }
        }
    }
}
