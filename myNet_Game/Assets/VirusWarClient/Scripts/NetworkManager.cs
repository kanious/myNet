using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirusWarGameUnity;
using MyNetUnity;
using myNet;

public class NetworkManager : MonoBehaviour {

    MyNetUnityService gameserver;
    string received_msg;

    public MonoBehaviour message_receiver;

    private void Awake()
    {
        this.received_msg = "";

        this.gameserver = gameObject.AddComponent<MyNetUnityService>();
        this.gameserver.appcallback_on_status_changed += on_status_changed;
        this.gameserver.appcallback_on_message += on_message;
    }

    public void connect()
    {
        this.gameserver.connect("127.0.0.1", 7777);
    }

    public bool is_connected()
    {
        return this.gameserver.is_connected();
    }

    /// <summary>
    /// 네트워크 상태 변결 시 호출될 콜백 메서드.
    /// </summary>
    void on_status_changed(NETWORK_EVENT status)
    {
        switch(status)
        {
            case NETWORK_EVENT.connected:
                {
                    LogManager.log("on connected");
                    this.received_msg += "on connected\n";

                    GameObject.Find("MainTitle").GetComponent<MainTitle>().on_connected();
                }
                break;

            case NETWORK_EVENT.disconnected:
                {
                    LogManager.log("disconnected");
                    this.received_msg += "disconnected\n";
                }
                break;
        }
    }

    /// <summary>
    /// 메시지 수신 시 호출될 콜백 메서드.
    /// </summary>
    void on_message(CPacket msg)
    {
        this.message_receiver.SendMessage("on_recv", msg);
    }

    public void send(CPacket msg)
    {
        this.gameserver.send(msg);
    }
}
