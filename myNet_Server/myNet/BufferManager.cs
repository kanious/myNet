using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace myNet
{
    // 버퍼라는 것은 바이트 배열로 이루어진 메모리 덩어리.
    class BufferManager
    {
        int m_numBytes;                  // 버퍼 풀에 의해 제어되는 총 바이트 수
        byte[] m_buffer;                 // 버퍼 매니저에 의해 유지되는 기본 바이트 배열
        Stack<int> m_freeIndexPool;
        int m_currentIndex;
        int m_bufferSize;

        public BufferManager(int totalBytes, int bufferSize)
        {
            m_numBytes = totalBytes;
            m_currentIndex = 0;
            m_bufferSize = bufferSize;
            m_freeIndexPool = new Stack<int>();
        }

        public void InitBuffer()
        {
            // 하나의 큰 버퍼를 생성하고 각각의 SocketAsyncEventArg 오브젝트가 분할하여 사용
            m_buffer = new byte[m_numBytes];
        }

        /// <summary>
        /// SocketAsyncEventArgs 객체에 버퍼를 설정해 준다.
        /// 사용하지 않아 반환된 버퍼가 있는 경우 해당 위치의 버퍼를 할당, 없으면 현재 인덱스 위치의 버퍼를 할당한다.
        /// 남은 공간이 버퍼 사이즈보다 작으면 실패.
        /// </summary>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if(0 < m_freeIndexPool.Count)
            {
                args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
            }
            else
            {
                if((m_numBytes - m_bufferSize) < m_currentIndex)
                {
                    return false;
                }
                args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                m_currentIndex += m_bufferSize;
            }
            return true;
        }

        /// <summary>
        /// 사용하지 않는 버퍼를 반환한다.
        /// </summary>
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            // 이 네트워크 모듈에서는 사용되지 않는다.
            // 왜냐하면 프로그램을 시작할 때 최대 동시 접속 수치만큼 버퍼를 할당한 뒤
            // 중간에 해제하지 않고 계속 물고 있을 것이기 때문이다.
            // SocketAsyncEventArgs만 풀링하여 재사용한다면 이 객체에 할당된 버퍼도 그대로 따라가게 된다.
            m_freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
