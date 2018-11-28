using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace myNet
{
    /// <summary>
    /// Endpoint 정보를 받아서 서버에 접속한다.
    /// 접속하려는 서버 하나 당 인스턴스 한개씩 생성하여 사용하면 된다.
    /// </summary>
    public class CConnector
    {
        Socket client;                      // 원격지 서버와의 연결을 위한 소켓.
        CNetworkService network_service;

        public delegate void ConnectedHandler(CUserToken token);
        public ConnectedHandler connected_callback { get; set; }

        public CConnector(CNetworkService network_service)
        {
            this.network_service = network_service;
            this.connected_callback = null;
        }

        public void connect(IPEndPoint remote_endpoint)
        {
            this.client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 비동기 접속을 위한 event args.
            SocketAsyncEventArgs event_arg = new SocketAsyncEventArgs();
            event_arg.Completed += on_connect_completed;
            event_arg.RemoteEndPoint = remote_endpoint;
            bool pending = this.client.ConnectAsync(event_arg);
            if(!pending)
            {
                on_connect_completed(null, event_arg);
            }
        }

        void on_connect_completed(object sender, SocketAsyncEventArgs e)
        {
            if(e.SocketError == SocketError.Success)
            {
                CUserToken token = new CUserToken();

                // 데이터 수신 준비.
                this.network_service.on_connect_completed(this.client, token);

                if(null != this.connected_callback)
                {
                    this.connected_callback(token);
                }
            }
            else
            {
                Console.WriteLine(string.Format("Failed to connect. {0}", e.SocketError));
            }
        }
    }
}
