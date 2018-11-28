using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace myNet
{
    class CListener
    {
        SocketAsyncEventArgs accept_args;       // 비동기 Accept를 위한 EventARgs
        Socket listen_socket;                   // 클라이언트의 접속을 처리할 소켓

        AutoResetEvent flow_control_event;      // Accept 처리 순서를 제어하기 위한 이벤트 변수
        //ManualResetEvent flow_control_event;  // AutoResetEvent와 ManualResetEvent 두 가지가 있다.
                                                // 오토는 한 번 Set이 된 이후 자동으로 Reset 상태로 만들어주고,
                                                // 매뉴얼은 직접 Reset 메서드를 호출하기 전까지 set 상태로 남아있는다.
                                                // 원하는 것으로 골라서 사용 가능.

        // 새로운 클라이언트가 접속했을 때 호출되는 델리게이트
        public delegate void NewclientHandler(Socket client_socket, object token);
        public NewclientHandler callback_on_newclient;

        public CListener()
        {
            this.callback_on_newclient = null;
        }

        public void start(string host, int port, int backlog)
        {
            // 소켓을 생성한다.
            this.listen_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress address;

            if("0.0.0.0" == host)
            {
                address = IPAddress.Any;
            }
            else
            {
                address = IPAddress.Parse(host);
            }
            IPEndPoint endpoint = new IPEndPoint(address, port);

            try
            {
                // 소켓에 host 정보를 바인딩시킨 뒤 Listen 메서드를 호출하여 대기한다.
                this.listen_socket.Bind(endpoint);
                this.listen_socket.Listen(backlog);

                this.accept_args = new SocketAsyncEventArgs();
                this.accept_args.Completed += new EventHandler<SocketAsyncEventArgs>(on_accept_completed);

                // 클라이언트가 돌아오기를 기다린다.
                // 비동기 메서드이므로 블로킹되지 않고 바로 리턴되며 콜백 메서드를 통해서 접속 통보를 받는다.
                //this.listen_socket.AcceptAsync(this.accept_args);
                // AcceptAsync부분을 스레드로 처리하도록 바꾼다.
                // 꼭 스레드를 통하지 않아도 처리 가능하지만, 특정 OS버전에서는 콘솔 입력이 대기중일 때
                // accept 처리가 되지 않는 버그가 있다. : http://goo.gl/MONp9F
                Thread listen_thread = new Thread(do_listen);
                listen_thread.Start();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// 루프를 돌며 클라이언트를 받아들인다.
        /// 하나의 접속 처리가 완료된 후 다음 accept를 수행하기 위해서 event 객체를 통해 흐름을 제어한다.
        /// </summary>
        void do_listen()
        {
            // accept 처리 제어를 위해 이벤트 객체를 생성한다.
            this.flow_control_event = new AutoResetEvent(false);

            while(true)
            {
                // SocketAsyncEventArgs를 재사용하기 위해서 null로 만들어 준다.
                this.accept_args.AcceptSocket = null;

                bool pending = true;
                try
                {
                    // 비동기 accept를 호출하여 클라이언트의 접속을 받아들인다.
                    // 비동기 메서드이지만 동기적으로 수행이 완료될 경우도 있으니
                    // 리턴 값을 확인하여 분기 처리를 해줘야 한다.
                    pending = listen_socket.AcceptAsync(this.accept_args);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                // 즉시 완료(리턴이 false)가 되면 이벤트가 발생하지 않으므로 콜백 메서드를 직접 호출해줘야 한다.
                // pending 상태라면 비동기 요청이 들어간 상태라는 뜻이며 콜백 메서드를 기다린다.
                // 참고 : https://goo.gl/4b99VW
                if(!pending)
                {
                    on_accept_completed(null, this.accept_args);
                }

                // 클라이언트 접속 처리가 완료되면 이벤트 객체의 신호를 전달받아 다시 루프를 수행한다.
                this.flow_control_event.WaitOne();      // 신호가 올 때까지 스레드 차단

                // 반드시 WaitOne -> Set 순서로 호출 되야 하는 것은 아니다.
                // Accept 작업이 굉장히 빨리 끝나서 Set -> WaitOne 순서로 호출된다고 하더라도
                // 다음 Accept 호출 까지 문제 없이 이루어 진다.
                // WaitOne 메서드가 호출될 때 이벤트 객체가 이미 signalled 상태라면 스레드를 대기 하지 않고 계속 진행하기 때문이다.
            }
        }

        /// <summary>
        /// AcceptAsync의 콜백 메서드.
        /// </summary>
        void on_accept_completed(object sender, SocketAsyncEventArgs e)
        {
            if(SocketError.Success == e.SocketError)
            {
                // 새로 생긴 소켓을 보관한 후
                Socket client_socket = e.AcceptSocket;

                // 다음 연결을 받아들인다.
                this.flow_control_event.Set();

                // 이 클래스에서는 accept까지의 역할만 수행하고 클라이언트의 접속 이후의 처리는
                // 외부로 넘기기 위해서 콜백 메서드를 호출해 주도록 한다.
                // 그 이유는 소켓 처리부와 콘텐츠 구현부를 분리하기 위해서다.
                // 콘텐츠 구현 부분은 자주 바뀔 가능성이 있지만, 소켓 accept 부분은
                // 상대적으로 변경이 적은 부분이기 때문에 양쪽을 분리시켜 주는 것이 좋다.
                // 또한, 클래스 설계 방침에 따라 Listen에 관련된 코드만 존재하도록 하기 위한 이유도 있다.

                if(null != this.callback_on_newclient)
                {
                    this.callback_on_newclient(client_socket, e.UserToken);
                }

                return;
            }
            else
            {
                // accept 실패 처리
                Console.WriteLine("Failed to accept client");
            }

            // 다음 연결을 받아들인다.
            this.flow_control_event.Set();
        }
    }
}
