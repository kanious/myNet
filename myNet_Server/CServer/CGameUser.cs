﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using myNet;

namespace CServer
{
    using GameServer;

    /// <summary>
    /// 하나의 세션 객체를 나타낸다.
    /// </summary>
    class CGameUser : IPeer
    {
        CUserToken token;

        public CGameUser(CUserToken token)
        {
            this.token = token;
            this.token.set_peer(this);
        }

        void IPeer.on_message(Const<byte[]> buffer)
        {
            CPacket msg = new CPacket(buffer.Value, this);
            PROTOCOL protocol = (PROTOCOL)msg.pop_protocol_id();
            Console.WriteLine("----------------------------------");
            Console.WriteLine("protocol id " + protocol);
            switch(protocol)
            {
                case PROTOCOL.CHAT_MSG_REQ:
                    {
                        string text = msg.pop_string();
                        Console.WriteLine(string.Format("text {0}", text));

                        CPacket response = CPacket.create((short)PROTOCOL.CHAT_MSG_ACK);
                        response.push(text);
                        send(response);
                    }
                    break;
            }
        }

        void IPeer.on_removed()
        {
            Console.WriteLine("The client disconnected.");
            //Console.WriteLine(string.Format("The client disconnected. IP Addr : {0}", this.token.socket.RemoteEndPoint));

            Program.remove_user(this);
        }

        public void send(CPacket msg)
        {
            this.token.send(msg);
        }

        void IPeer.disconnect()
        {
            this.token.socket.Disconnect(false);
        }

        void IPeer.process_user_operation(CPacket msg)
        {

        }
    }
}
