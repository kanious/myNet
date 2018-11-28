using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using myNet;

namespace CServer
{
    class Program
    {
        static List<CGameUser> userlist;

        static void Main(string[] args)
        {
            CPacketBufferManager.initialize(2000);
            userlist = new List<CGameUser>();

            CNetworkService service = new CNetworkService();
            service.session_created_callback += on_session_created;     // 콜백 메서드 설정.
            service.initialize();

            // host ip는 서버의 IP 주소를 의미.
            // 0.0.0.0을 넣어주면 모든 IP를 통해 들어오는 데이터를 다 받아들인다는 뜻이다.
            // backlog값은 accept 처리 도중 대기시키 연결 개수를 의미한다.
            // accept 처리가 아직 끝나지 않은 상태에서 또 다른 연결 요청이 들어온다면 backlog로 설정된 값만큼
            // 대기 큐에 대기시켜 놓고 accept 처리 완료 후 진행시켜 주게 된다.
            service.listen("0.0.0.0", 7777, 100);

            Console.WriteLine("Start Server!");
            while (true)
            {
                // 서버가 중지되지 않도록 무한 루프를 돌려준다.
                // 메인 스레드가 블로킹되면 안 되기 때문에 sleep을 통해 적당히 쉬어준다.
                System.Threading.Thread.Sleep(1000);
            }

            //Console.ReadKey();
        }

        /// <summary>
        /// 클라이언트가 접속 완료 되었을 때 호출된다.
        /// n개의 워커 스레드에서 호출될 수 있으므로 공유 자원 접근 시 동기화 처리를 해주어야 한다.
        /// </summary>
        static void on_session_created(CUserToken token)
        {
            CGameUser user = new CGameUser(token);
            lock(userlist)
            {
                userlist.Add(user);
                //Console.WriteLine(string.Format("The client connected. IP Addr : {0}", token.socket.RemoteEndPoint));
            }
        }

        public static void remove_user(CGameUser user)
        {
            lock(userlist)
            {
                userlist.Remove(user);
            }
        }
    }
}
