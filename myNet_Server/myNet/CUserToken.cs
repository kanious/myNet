using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace myNet
{
    public class CUserToken
    {
        public Socket socket { get; set; }
        CMessageResolver message_resolver;      // 명령어 해석 전용 클래스, 바이트를 패킷 형식으로 해석해준다.
        private object cs_sending_queue;        // sending queue lock처리에 사용되는 객체.
        Queue<CPacket> sending_queue;           // 전송할 패킷을 보관해놓는 큐. 1-Send로 처리하기 위한 큐이다.
        IPeer peer;                             // session객체. 어플리케이션 상에서 구현하여 사용.

        public SocketAsyncEventArgs receive_event_args { get; private set; }
        public SocketAsyncEventArgs send_event_args { get; private set; }

        public CUserToken()
        {
            this.cs_sending_queue = new object();
            this.message_resolver = new CMessageResolver();
            this.sending_queue = new Queue<CPacket>();
            this.peer = null;
        }

        public void set_peer(IPeer peer)
        {
            this.peer = peer;
        }

        public void set_event_args(SocketAsyncEventArgs receive_event_args, SocketAsyncEventArgs send_event_args)
        {
            this.receive_event_args = receive_event_args;
            this.send_event_args = send_event_args;
        }

        public void on_receive(byte[] buffer, int offset, int transfered)
        {
            this.message_resolver.on_receive(buffer, offset, transfered, on_message);
        }

        void on_message(Const<byte[]> buffer)
        {
            if(null != this.peer)
            {
                this.peer.on_message(buffer);
            }
        }

        public void on_removed()
        {
            if(null != this.peer)
            {
                this.peer.on_removed();
            }
        }

        /// <summary>
        /// 패킷을 전송한다.
        /// 큐가 비어 있을 경우에는 큐에 추가한 뒤 바로 SendAsync 메서드를 호출하고,
        /// 데이터가 들어있을 경우에는 새로 추가만 한다.
        /// 큐잉된 패킷의 전송 시점 : 현재 진행 중인 SendAsync가 완료되었을 때 큐를 검사하여 나머지 패킷을 전송한다.
        /// </summary>
        public void send(CPacket msg)
        {
            CPacket clone = new CPacket();
            msg.copy_to(clone);

            lock(this.cs_sending_queue)
            {
                // 큐가 비어 있다면 큐에 추가하고 바로 비동기 전송 메서드를 호출한다.
                if(this.sending_queue.Count <= 0)
                {
                    this.sending_queue.Enqueue(clone);
                    start_send();
                    return;
                }

                // 큐가 비어 있지 않다면 아직 이전 전송이 완료되지 않은 상태이므로 큐에 추가만 하고 리턴한다.
                // 현재 수행중인 SendAsync가 완료된 이후에 큐를 검사하여 데이터가 있으면 다시 SendAsync를 호출하여 처리할 것이다.
                Console.WriteLine("Queue is not empty. Copy and Enqueue a msg. protocol id : " + msg.protocol_id);
                this.sending_queue.Enqueue(clone);
            }
        }

        /// <summary>
        /// 비동기 전송을 시작한다.
        /// </summary>
        void start_send()
        {
            lock (this.cs_sending_queue)
            {
                // 전송이 아직 완료된 상태가 아니므로 데이터만 가져오고 큐에서 제거하지 않는다.
                // Dequeue()로 패킷을 꺼내오면 큐에서 제거가 된다.
                // 해당 패킷이 큐의 마지막 하나의 패킷이었다면, 패킷을 꺼내는 즉시 큐가 empty 상태가 된다.
                // 따라서 다른 데이터 전송 요청이 들어올 경우 큐가 비었다는 것을 인식하여 SendAsync를 호출한다.
                // 마지막으로 꺼내온 패킷의 SendAsync가 완료되지 않은 상태에서 또 다른 SendAsync가 실행되게 된다.
                //
                // SendAsync 자체는 중복 호출되어도 상관 없지만 비동기 전송 시 this.send_event_args 변수를
                // 공유하여 사용하고 있기 때문에 문제가 생긴다.
                CPacket msg = this.sending_queue.Peek();

                // 헤더에 패킷 사이즈를 기록한다.
                msg.record_size();

                // 이번에 보낼 패킷 사이즈만큼 버퍼 크기를 설정한다.
                this.send_event_args.SetBuffer(this.send_event_args.Offset, msg.position);

                // 패킷 내용을 SocketAsyncEventArgs 버퍼에 복사한다.
                Array.Copy(msg.buffer, 0, this.send_event_args.Buffer, this.send_event_args.Offset, msg.position);

                // 비동기 전송 시작
                bool pending = this.socket.SendAsync(this.send_event_args);
                if(!pending)
                {
                    process_send(this.send_event_args);
                }
            }
        }

        static int sent_count = 0;
        static object cs_count = new object();
        /// <summary>
        /// 비동기 전송 완료 시 호출되는 콜백 메소드.
        /// </summary>
        public void process_send(SocketAsyncEventArgs e)
        {
            if(e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                Console.WriteLine(string.Format("Failed to send. error {0}, transferred {1}", e.SocketError, e.BytesTransferred));
                return;
            }

            lock (this.cs_sending_queue)
            {
                if(this.sending_queue.Count <= 0)
                {
                    throw new Exception("Sending queue count is less than zero!");
                }

                // TODO : 재전송 로직 검토 필요, 패킷 하나를 다 못보낸 경우 예외 처리 필요
                int size = this.sending_queue.Peek().position;
                if(e.BytesTransferred != size)
                {
                    string error = string.Format("Need to send more! transferred {0}, packet size {1}", e.BytesTransferred, size);
                    Console.WriteLine(error);
                    return;
                }

                // 콘솔 확인용
                // TODO : lock 구문 빼고 그냥 콘솔 찍어보기
                lock (cs_count)
                {
                    ++sent_count;
                    Console.WriteLine(string.Format("process send : {0}, transferred {1}, sent count {2}"
                        , e.SocketError, e.BytesTransferred, sent_count));
                }

                // 전송 완료된 패킷을 큐에서 제거한다.
                this.sending_queue.Dequeue();

                // 아직 전송하지 않은 대기중인 패킷이 있다면 다시 한 번 전송을 요청한다.
                if(this.sending_queue.Count > 0)
                {
                    start_send();
                }
            }
        }

        public void disconnect()
        {
            try
            {
                this.socket.Shutdown(SocketShutdown.Send);
            }
            catch(Exception e)
            {
                // 이미 클라이언트가 닫힌 경우 예외 처리
                Console.WriteLine(e.Message);
            }

            this.socket.Close();
        }

        public void start_keepalive()
        {
            System.Threading.Timer keepalive = new System.Threading.Timer((object e) =>
            {
                CPacket msg = CPacket.create(0);
                msg.push(0);
                send(msg);
            }, null, 0, 3000);
        }
    }
}
