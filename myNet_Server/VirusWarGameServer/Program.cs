using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using myNet;

namespace VirusWarGameServer
{
    class Program
    {
        static List<CGameUser> userlist;
        public static CGameServer game_main = new CGameServer();

        static void Main(string[] args)
        {
            CPacketBufferManager.initialize(2000);
            userlist = new List<CGameUser>();

            CNetworkService service = new CNetworkService();
            service.session_created_callback += on_session_created;
            service.initialize();
            service.listen("0.0.0.0", 7777, 100);

            Console.WriteLine("Started!");
            while(true)
            {
                string input = Console.ReadLine();
                System.Threading.Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// 클라이언트가 접속 완료 하였을 때 호출된다.
        /// n개의 워커스레드에서 호출될 수 있으므로 공유 자원 접근 시 동기화 처리를 해주어야 한다.
        /// </summary>
        static void on_session_created(CUserToken token)
        {
            CGameUser user = new CGameUser(token);
            lock (userlist)
            {
                userlist.Add(user);
            }
        }

        public static void remove_user(CGameUser user)
        {
            lock (userlist)
            {
                userlist.Remove(user);
                game_main.user_disconnected(user);

                CGameRoom room = user.battle_room;
                if(null != room)
                {
                    game_main.room_manager.remove_room(user.battle_room);
                }
            }
        }
    }
}
