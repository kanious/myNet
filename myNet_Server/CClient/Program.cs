using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using myNet;

namespace CClient
{
    using GameServer;
    using System.Net;

    class Program
    {
        static List<IPeer> game_servers = new List<IPeer>();    // 여러 가지 서버가 있을 수 있으므로 리스트로 관리.

        static void Main(string[] args)
        {
            CPacketBufferManager.initialize(2000);
            // CNetworkService 객체는 메시지의 비동기 송/수신 처리를 수행한다.
            // 메시지 송/수신은 서버, 클라이언트 모두 동일한 로직으로 처리될 수 있으므로
            // CNetworkService 객체를 생성하여 Connector 객체에 넘겨준다.
            CNetworkService service = new CNetworkService();

            // endpoint 정보를 갖고 있는 Connector를 생성, 만들어 둔 CNetworkService 객체를 넣어준다.
            CConnector connector = new CConnector(service);

            // 접속 성공 시 호출될 콜백 메서드 지정.
            connector.connected_callback += on_connected_gameserver;
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 7777);
            connector.connect(endpoint);

            while(true)
            {
                Console.Write("> ");
                string line = Console.ReadLine();
                if("q" == line)
                {
                    break;
                }

                CPacket msg = CPacket.create((short)PROTOCOL.CHAT_MSG_REQ);
                msg.push(line);
                game_servers[0].send(msg);
            }

            ((CRemoteServerPeer)game_servers[0]).token.disconnect();

            Console.ReadKey();
        }

        /// <summary>
        /// 접속 성공 시 호출될 콜백 메서드.
        /// </summary>
        static void on_connected_gameserver(CUserToken server_token)
        {
            lock (game_servers)
            {
                IPeer server = new CRemoteServerPeer(server_token);
                game_servers.Add(server);
                Console.WriteLine("Connected!");
            }
        }
    }
}
