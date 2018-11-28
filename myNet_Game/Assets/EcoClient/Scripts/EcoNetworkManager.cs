using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyNetUnity;
using myNet;

/// <summary>
/// 로직 처리 부분에서 서버 접속, 이벤트 핸들링, 메시지 핸들링을 수행하는 클래스
/// </summary>
public class EcoNetworkManager : MonoBehaviour {

    MyNetUnityService gameserver;

    private void Awake()
    {
        // 네트워크 통신을 위해 MyNetUnityService 객체를 추가한다.
        this.gameserver = gameObject.AddComponent<MyNetUnityService>();

        // 상태 변화(접속, 끊김 등)을 통보 받을 델리게이트 설정.
        this.gameserver.appcallback_on_status_changed += on_status_changed;

        // 패킷 수신 델리게이트 설정.
        this.gameserver.appcallback_on_message += on_message;
    }

    public void connect()
    {
        this.gameserver.connect("127.0.0.1", 7777);
    }

    public bool is_connect()
    {
        return this.gameserver.is_connected();
    }

    /// <summary>
    /// 네트워크 상태 변경 시 호출될 메서드.
    /// </summary>
    void on_status_changed(NETWORK_EVENT status)
    {
        switch(status)
        {
            case NETWORK_EVENT.connected:
                {
                    Debug.Log("on connected");

                    CPacket msg = CPacket.create((short)PROTOCOL.CHAT_MSG_REQ);
                    msg.push("Welcome!!");
                    this.gameserver.send(msg);
                }
                break;

            case NETWORK_EVENT.disconnected:
                {
                    Debug.Log("disconnected");
                }
                break;
        }
    }

    void on_message(CPacket msg)
    {
        // 제일 먼저 프로토콜 아이디를 꺼내온다.
        PROTOCOL protocol_id = (PROTOCOL)msg.pop_protocol_id();

        // 프로토콜에 따른 분기 처리
        switch(protocol_id)
        {
            case PROTOCOL.CHAT_MSG_ACK:
                {
                    string text = msg.pop_string();
                    GameObject.Find("GameMain").GetComponent<EcoGameMain>().on_receive_chat_msg(text);
                }
                break;
        }
    }

    public void send(CPacket msg)
    {
        this.gameserver.send(msg);
    }
}
