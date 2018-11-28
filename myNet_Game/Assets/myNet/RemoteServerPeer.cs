using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using myNet;
using System;

namespace MyNetUnity
{
    /// <summary>
    /// 클라이언트에서 통신을 수행할 대상이 되는 서버 객체
    /// </summary>
    public class RemoteServerPeer : IPeer
    {
        public CUserToken token { get; private set; }
        WeakReference mynet_eventmanager;

        public RemoteServerPeer(CUserToken token)
        {
            this.token = token;
            this.token.set_peer(this);
        }

        public void set_eventmanager(MyNetEventManager event_manager)
        {
            this.mynet_eventmanager = new WeakReference(event_manager);
        }

        /// <summary>
        /// 메시지를 수신했을 때 호출된다.
        /// 파라미터로 넘어온 버퍼는 워커스레드에서 재사용 되므로 복사한 뒤 어플리케이션으로 넘겨준다.
        /// </summary>
        void IPeer.on_message(Const<byte[]> buffer)
        {
            // 버퍼를 복사한 뒤 CPacket 클래스로 감싸 넘겨준다.
            // CPacket 클래스 내부에서는 참조로만 들고 있는다.
            byte[] app_buffer = new byte[buffer.Value.Length];
            Array.Copy(buffer.Value, app_buffer, buffer.Value.Length);
            CPacket msg = new CPacket(app_buffer, this);
            (this.mynet_eventmanager.Target as MyNetEventManager).enqueue_network_message(msg);
        }

        void IPeer.on_removed()
        {
            (this.mynet_eventmanager.Target as MyNetEventManager).enqueue_network_event(NETWORK_EVENT.disconnected);
        }

        void IPeer.send(CPacket msg)
        {
            this.token.send(msg);
        }

        void IPeer.disconnect()
        {

        }

        void IPeer.process_user_operation(CPacket msg)
        {

        }
    }
}
