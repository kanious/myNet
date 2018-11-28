using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/*
 * < 닷넷 네트워크 API >
 * AcceptAsync : 클라이언트의 연결을 수락
 * ReceiveAsync : 메시지를 수신
 * SendAsync : 메시지를 전송
 * ConnectAsync : 서버에 접속을 수행
 * 
 * < SocketAsyncEventArgs >
 * 이 클래스는 비동기 소켓 작업에 사용되는 클래스이다. MSDN에는 "Represents an asynchronous socket operation"이라고
 * 나온다. 비동기 네트워크 메서드를 이용하여 I/O 작업을 수행한 뒤 해당 작업이 완료될 때, 이 클래스 객체의
 * Completed 이벤트를 통해서 작업 완료 통보가 들어온다. Completed 이벤트에서 호출되는 메서드는 다음과 같은 형태를 갖는다.
 * void on_connect_completed(object sender, SocketAsyncEventArgs e)
 * 이 메서드의 파라미터로 들어오는 SocketAsyncEventArgs 객체는 비동기 메서드를 호출할 때 파라미터로 넣어줬던 객체이다.
 * 따라서 메서드를 호출할 때마다 따로 보관해놓을 필요 없이 파라미터로 넘어오는 객체를 그대로 사용하면 된다.
 */

namespace myNet
{
    public class CNetworkService
    {
        CListener client_listener;                          // 클라이언트 접속을 받아들이기 위한 객체
        SocketAsyncEventArgsPool receive_event_args_pool;   // 메시지 수신 시 필요한 객체
        SocketAsyncEventArgsPool send_event_args_pool;      // 메시지 전송 시 필요한 객체
        BufferManager buffer_manager;                       // 메시지 수신, 전송 시 닷넷 비동기 소켓에서 사용할 버퍼를 관리하는 객체
        int connected_count;
        int max_connections;
        int buffer_size;
        readonly int pre_alloc_count = 2;                   // 전송용(write) 1개, 수신용(read) 2개

        // 클라이언트의 접속이 이루어졌을 때 호출되는 델리게이트
        public delegate void SessionHandler(CUserToken token);
        public SessionHandler session_created_callback { get; set; }

        public CNetworkService()
        {
            this.connected_count = 0;
            this.session_created_callback = null;
        }

        public void initialize()
        {
            this.max_connections = 10;      // 최대로 접속할 수 있는 클라이언트의 수
            this.buffer_size = 1024;        // 한 번에 할당되는 버퍼 크기

            this.receive_event_args_pool = new SocketAsyncEventArgsPool(this.max_connections);
            this.send_event_args_pool = new SocketAsyncEventArgsPool(this.max_connections);
            
            // 버퍼의 크기 = 최대 동시 접속 수치 * 버퍼 하나의 크기 * (전송용 1개 + 수신용 1개)
            this.buffer_manager = new BufferManager(this.max_connections * this.buffer_size * this.pre_alloc_count, this.buffer_size);
            this.buffer_manager.InitBuffer();

            SocketAsyncEventArgs arg;
            for(int i = 0; i < this.max_connections; ++i)
            {
                // 동일한 소켓에 대고 send, receive를 하므로 user token은 세션별로 하나씩만 만들어 놓고
                // receive, send EventArgs에서 동일한 token을 참조하도록 구성한다.
                // userToken 1개 = client 1대
                CUserToken token = new CUserToken();

                // receive pool
                {
                    // Pre-allocate a set of reusable SocketAsyncEventArgs
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(receive_completed);
                    arg.UserToken = token;

                    // SocketAsyncEventArg에 버퍼 할당
                    this.buffer_manager.SetBuffer(arg);

                    // add SocketAsyncEventArg to the pool
                    this.receive_event_args_pool.Push(arg);
                }

                // send pool
                {
                    // Pre-allocate a set of resuable SocketAsyncEventArgs
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(send_completed);
                    arg.UserToken = token;

                    // SocketAsyncEventArg에 버퍼 할당
                    this.buffer_manager.SetBuffer(arg);

                    // add SocketAsyncEventArg to the pool
                    this.send_event_args_pool.Push(arg);
                }
            }
        }

        public void listen(string host, int port, int backlog)
        {
            CListener listener = new CListener();
            listener.callback_on_newclient += on_new_client;
            listener.start(host, port, backlog);
        }

        /// <summary>
        /// 원격 서버에 접속 성공 했을 때 호출된다.
        /// </summary>
        public void on_connect_completed(Socket socket, CUserToken token)
        {
            // SocketAsyncEventArgsPool에서 빼오지 않고 그때 그때 할당해서 사용한다.
            // pool은 서버에서 클라이언트와의 통신용으로만 쓰려고 만든 것이기 때문이다.
            // 클라이언트 입장에서 서버와 통신을 할 때는 접속한 서버 당 두 개의 EventArgs만 있으면 되기 때문에 그냥 새로 할당해서 쓴다.
            // 서버 간 연결에서도 마찬가지이다.
            // 풀링처리를 하려면 c->s로 가는 별도의 풀을 만들어서 써야 한다.
            SocketAsyncEventArgs receive_event_arg = new SocketAsyncEventArgs();
            receive_event_arg.Completed += new EventHandler<SocketAsyncEventArgs>(receive_completed);
            receive_event_arg.UserToken = token;
            receive_event_arg.SetBuffer(new byte[1024], 0, 1024);

            SocketAsyncEventArgs send_event_arg = new SocketAsyncEventArgs();
            send_event_arg.Completed += new EventHandler<SocketAsyncEventArgs>(send_completed);
            send_event_arg.UserToken = token;
            send_event_arg.SetBuffer(new byte[1024], 0, 1024);

            begin_receive(socket, receive_event_arg, send_event_arg);
        }

        /// <summary>
        /// 새로운 클라이언트가 접속 성공 했을 때 호출된다.
        /// AcceptAsync의 콜백에서 호출되며 여러 스레드에서 동시에 호출될 수 있기 때문에 공유자원 접근 시 주의해야 한다.
        /// </summary>
        void on_new_client(Socket client_socket, object token)
        {
            Interlocked.Increment(ref this.connected_count);

            Console.WriteLine(string.Format("[{0}] A client connected. handle {1}, count {2}"
                , Thread.CurrentThread.ManagedThreadId, client_socket.Handle, this.connected_count));

            // 이미 만들어둔 SocketAsyncEventArgs 풀에서 하나 꺼내어 사용
            SocketAsyncEventArgs receive_args = this.receive_event_args_pool.Pop();
            SocketAsyncEventArgs send_args = this.send_event_args_pool.Pop();

            // SocketAsyncEventArgs 풀을 생성할 때 만들어 두었던 CUserToken을 꺼내와서
            // 콜백 메서드의 파라미터로 넘겨준다.
            CUserToken user_token = null;
            if (null != this.session_created_callback)
            {
                user_token = receive_args.UserToken as CUserToken;
                this.session_created_callback(user_token);
            }

            // 클라이언트로부터 데이터를 수신할 준비를 한다.
            begin_receive(client_socket, receive_args, send_args);
            //user_token.start_keepalive();
        }

        void begin_receive(Socket socket, SocketAsyncEventArgs receive_args, SocketAsyncEventArgs send_args)
        {
            // receive_args, send_args 아무곳에서나 꺼내와도 된다. 둘 다 동일한 CUserToken을 물고 있기 때문이다.
            CUserToken token = receive_args.UserToken as CUserToken;
            token.set_event_args(receive_args, send_args);

            // 생성된 클라이언트 소켓을 보관해 놓고 통신할 때 사용한다.
            token.socket = socket;

            // 데이터를 받을 수 있도록 수신 메서드를 호출해준다.
            // 비동기로 수신될 경우 워커 스레드에서 대기 중으로 있다가 Completed에 설정해놓은 메서드가 호출된다.
            // 동기로 완료될 경우에는 직접 완료 메서드를 호출해줘야 한다.
            bool pending = socket.ReceiveAsync(receive_args);
            if(!pending)
            {
                process_receive(receive_args);
            }
        }
        
        void receive_completed(object sender, SocketAsyncEventArgs e)
        {
            // receive_arg 선언 당시 Completed 이벤트에 이 함수를 추가해 주었기 때문에
            // 클라이언트로부터 메시지가 들어오면 이 함수로 도착한다.
            // 이 때 process_receive를 호출해준다.
            // 만약 메시지 수신이 동기적으로 이루어졌을 때는 이 함수를 거치지 않기 때문에
            // bool 변수로 확인하여 process_receive 함수를 직접 호출해주는 것이다.
            if (SocketAsyncOperation.Receive == e.LastOperation)
            {
                process_receive(e);
                return;
            }

            // 송, 수신을 분리해서 호출하도록 구성하였기 때문에 예외처리는 발생하지 않을 것이다.
            throw new ArgumentException("the last operation completed on the socket was not a receive.");
        }

        void send_completed(object sender, SocketAsyncEventArgs e)
        {
            CUserToken token = e.UserToken as CUserToken;
            token.process_send(e);
        }

        /// <summary>
        /// 이 메서드는 비동기 수신 작업이 완료 될 때 호출된다.
        /// 원격 호스트가 연결을 닫으면 소켓이 닫힌다.
        /// </summary>
        void process_receive(SocketAsyncEventArgs e)
        {
            // 원격 호스트가 닫혀 있는지 체크한다.
            CUserToken token = e.UserToken as CUserToken;
            if(e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                // 이후의 작업은 CUserToken에 맡긴다.
                token.on_receive(e.Buffer, e.Offset, e.BytesTransferred);

                // 다음 메시지 수신을 위해서 다시 ReceiveAsync 메서드를 호출한다.
                bool pending = token.socket.ReceiveAsync(e);
                if(!pending)
                {
                    process_receive(e);
                }
            }
            else
            {
                Console.WriteLine(string.Format("error {0}, transferred {1}", e.SocketError, e.BytesTransferred));
                close_clientsocket(token);
            }
        }

        public void close_clientsocket(CUserToken token)
        {
            token.on_removed();

            // 버퍼는 반환할 필요가 없다. SocketAsyncEventArg가 버퍼를 물고 있기 때문에
            // 이것을 재사용할 때 물고 있는 버퍼를 그대로 사용하면 되기 때문이다.
            if(null != this.receive_event_args_pool)
            {
                this.receive_event_args_pool.Push(token.receive_event_args);
            }

            if(null != this.send_event_args_pool)
            {
                this.send_event_args_pool.Push(token.send_event_args); 
            }
        }
    }
}
