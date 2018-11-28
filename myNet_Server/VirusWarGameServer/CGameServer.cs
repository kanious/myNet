using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using myNet;

namespace VirusWarGameServer
{
    class CGameServer
    {
        object operation_lock;
        Queue<CPacket> user_operations;
        Thread logic_Thread;            // 로직은 하나의 스레드로만 처리.
        AutoResetEvent loop_event;

        // 게임 로직 처리 관련 변수
        public CGameRoomManager room_manager { get; private set; }  // 게임방을 관리하는 매니저.
        List<CGameUser> matching_waiting_users;                     // 매칭 대기 리스트.

        public CGameServer()
        {
            this.operation_lock = new object();
            this.loop_event = new AutoResetEvent(false);
            this.user_operations = new Queue<CPacket>();
            this.room_manager = new CGameRoomManager();
            this.matching_waiting_users = new List<CGameUser>();

            this.logic_Thread = new Thread(gameloop);
            this.logic_Thread.Start();
        }

        /// <summary>
        /// 게임 로직을 수행하는 루프.
        /// 유저 패킷 처리를 담당한다.
        /// </summary>
        void gameloop()
        {
            while(true)
            {
                CPacket packet = null;
                lock (this.operation_lock)
                {
                    if (this.user_operations.Count > 0)
                    {
                        packet = this.user_operations.Dequeue();
                    }
                }

                if(null != packet)
                {
                    process_receive(packet);
                }

                if(this.user_operations.Count <= 0)
                {
                    this.loop_event.WaitOne();
                }
            }
        }

        /// <summary>
        /// 유저로부터 매칭 요청이 왔을 때 호출됨.
        /// </summary>
        public void matching_req(CGameUser user)
        {
            // 대기 리스트에 중복 추가 되지 않도록 체크.
            if(this.matching_waiting_users.Contains(user))
            {
                return;
            }

            // 매칭 대기 리스트에 추가.
            this.matching_waiting_users.Add(user);

            // 2명이 모이면 매칭 성공.
            if (this.matching_waiting_users.Count == 2)
            {
                // 게임 방 생성.
                this.room_manager.create_room(this.matching_waiting_users[0], this.matching_waiting_users[1]);
                // 매칭 대기 리스트 삭제.
                this.matching_waiting_users.Clear();
            }
        }

        public void enqueue_packet(CPacket packet, CGameUser user)
        {
            lock (this.operation_lock)
            {
                this.user_operations.Enqueue(packet);
                this.loop_event.Set();
            }
        }

        void process_receive(CPacket msg)
        {
            msg.owner.process_user_operation(msg);
        }

        public void user_disconnected(CGameUser user)
        {
            if(this.matching_waiting_users.Contains(user))
            {
                this.matching_waiting_users.Remove(user);
            }
        }
    }
}
