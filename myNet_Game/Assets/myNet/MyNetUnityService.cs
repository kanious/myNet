using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using myNet;
using System.Net;
using System;

namespace MyNetUnity
{
    /// <summary>
    /// MyNet 엔진과 유니티 어플리케이션을 이어주는 클래스이다.
    /// MyNet 엔진에서 받은 접속 이벤트, 메시지 수신 이벤트등을 어플리케이션으로 전달하는 역할을 하는데
    /// MonoBehaviour를 상속받아 유니티 어플리케이션과 동일한 스레드에서 작동되도록 구현하였다.
    /// 따라서 이 클래스의 콜백 메서드에서 유니티 오브젝트에 접근할 때 별도의 동기화 처리는 하지 않아도 된다.
    /// </summary>
    public class MyNetUnityService : MonoBehaviour
    {
        MyNetEventManager event_manager;

        IPeer gameserver;               // 연결된 게임 서버 객체.
        CNetworkService service;        // TCP 통신을 위한 서비스 객체.

        // 접속 완료 시 호출되는 델리게이트. 어플리케이션에서 콜백 메서드를 설정하여 사용한다.
        public delegate void StatusChangedHandler(NETWORK_EVENT status);
        public StatusChangedHandler appcallback_on_status_changed;

        // 네트워크 메시지 수신 시 호출되는 델리게이트. 어플리케이션에서 콜백 메서드를 설정하여 사용한다.
        public delegate void MessageHandler(CPacket msg);
        public MessageHandler appcallback_on_message;

        private void Awake()
        {
            CPacketBufferManager.initialize(10);
            this.event_manager = new MyNetEventManager();
        }

        public void connect(string host, int port)
        {
            if (null != this.service)
            {
                Debug.LogError("Already connected.");
                return;
            }

            // CNetworkService 객체는 메시지의 비동기 송/수신 처리를 수행한다.
            this.service = new CNetworkService();

            // endpoint 정보를 갖고 있는 Connector 생성. 만들어 둔 NetworkService 객체를 넣어준다.
            CConnector connector = new CConnector(service);

            // 접속 성공 시 호출될 콜백 메서드 지정.
            connector.connected_callback += on_connected_gameserver;
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(host), port);
            connector.connect(endpoint);
        }

        public bool is_connected()
        {
            return null != this.gameserver;
        }

        /// <summary>
        /// 접속 성공 시 호출될 콜백 메서드.
        /// </summary>
        void on_connected_gameserver(CUserToken server_token)
        {
            this.gameserver = new RemoteServerPeer(server_token);
            ((RemoteServerPeer)this.gameserver).set_eventmanager(this.event_manager);

            // 유니티 어플리케이션으로 이벤트를 넘겨주기 위해 매니저에 큐잉 시켜 준다.
            this.event_manager.enqueue_network_event(NETWORK_EVENT.connected);
        }

        /// <summary>
        /// 네트워크에서 발생하는 모든 이벤트를 클라이언트에게 알려주는 역할을 update에서 진행한다.
        /// MyNet엔진의 메시지 송수신 처리는 워커스레드에서 수행되지만, 유니티의 로직 처리는 메인스레드에서 수행되므로
        /// 큐잉처리를 통하여 메인스레드에서 모든 로직 처리가 이루어지도록 구성한다.
        /// 
        /// 큐잉처리를 하는 이유 :
        /// 네트워크 발생 이벤트의 경우 비동기 소켓 메서드를 시용하였기 때문에 메인스레드가 아닌 워커스레드에서 실행된다.
        /// 따라서 콜백도 워커스레드에서 호출되게 된다. 이 콜백 함수에서 유니티 메서드를 직접 호출하거나
        /// 게임 로직 처리를 담당하는 클래스의 메서드를 호출하게 되면 메인스레드가 아니라서 사용할 수 없다는 에러가 뜨거나
        /// 로직 데이터가 깨지게 된다. 그래서 컨테이너에 큐잉하여 간접적으로 처리하는 것이다.
        /// </summary>
        private void Update()
        {
            // 수신된 메시지에 대한 콜백.
            if (this.event_manager.has_message())
            {
                CPacket msg = this.event_manager.dequeue_network_message();
                if (null != this.appcallback_on_message)
                {
                    this.appcallback_on_message(msg);
                }
            }

            // 네트워크 발생 이벤트에 대한 콜백.
            if (this.event_manager.has_event())
            {
                NETWORK_EVENT status = this.event_manager.dequeue_network_event();
                if (null != this.appcallback_on_status_changed)
                {
                    this.appcallback_on_status_changed(status);
                }
            }
        }

        public void send(CPacket msg)
        {
            try
            {
                this.gameserver.send(msg);
                CPacket.destroy(msg);
            }
            catch(Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        /// <summary>
        /// 정상적인 종료 시에는 OnApplicationQuit 메서드에서 disconnect를 호출해줘야 유니티가 hang되지 않는다.
        /// </summary>
        private void OnApplicationQuit()
        {
            if (null != this.gameserver)
            {
                ((RemoteServerPeer)this.gameserver).token.disconnect();
            }
        }
    }
}
