﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using myNet;

namespace MyNetUnity
{
    public enum NETWORK_EVENT : byte
    {
        connected,          // 접속 완료.
        disconnected,       // 연결 끊김.
        end
    }

    /// <summary>
    /// 네트워크 엔진에서 발생된 이벤트들을 큐잉시킨다.
    /// 워커 스레드와 메인 스레드 양쪽에서 호출될 수 있으므로 스레드 동기화 처리를 적용하였다.
    /// </summary>
    public class MyNetEventManager
    {
        object cs_event;                        // 동기화 객체.
        Queue<NETWORK_EVENT> network_events;    // 네트워크 엔진에서 발생된 이벤트들을 보관해놓는 큐.
        Queue<CPacket> network_message_events;  // 서버에서 받은 패킷들을 보관해놓는 큐.

        public MyNetEventManager()
        {
            this.network_events = new Queue<NETWORK_EVENT>();
            this.network_message_events = new Queue<CPacket>();
            this.cs_event = new object();
        }
        
        public bool has_event()
        {
            lock (this.cs_event)
            {
                return this.network_events.Count > 0;
            }
        }

        public void enqueue_network_event(NETWORK_EVENT event_type)
        {
            lock (this.cs_event)
            {
                this.network_events.Enqueue(event_type);
            }
        }

        public NETWORK_EVENT dequeue_network_event()
        {
            lock (this.cs_event)
            {
                return this.network_events.Dequeue();
            }
        }

        public bool has_message()
        {
            lock (this.cs_event)
            {
                return this.network_message_events.Count > 0;
            }
        }

        public void enqueue_network_message(CPacket buffer)
        {
            lock (this.cs_event)
            {
                this.network_message_events.Enqueue(buffer);
            }
        }

        public CPacket dequeue_network_message()
        {
            lock (this.cs_event)
            {
                return this.network_message_events.Dequeue();
            }
        }
    }
}
