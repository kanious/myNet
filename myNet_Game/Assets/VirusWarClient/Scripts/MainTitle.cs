using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirusWarGameUnity;
using myNet;

public class MainTitle : MonoBehaviour {

    enum USER_STATE
    {
        NOT_CONNECTED,
        CONNECTED,
        WAITING_MATCHING
    }

    Texture bg;
    BattleRoom battle_room;

    NetworkManager network_manager;
    USER_STATE user_state;

    Texture waiting_img;

    private void Start()
    {
        this.user_state = USER_STATE.NOT_CONNECTED;
        this.bg = Resources.Load("images/title_blue") as Texture;
        this.battle_room = GameObject.Find("BattleRoom").GetComponent<BattleRoom>();
        this.battle_room.gameObject.SetActive(false);

        this.network_manager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        this.waiting_img = Resources.Load("images/waiting") as Texture;
        enter();
    }

    public void enter()
    {
        StopCoroutine("after_connected");

        this.network_manager.message_receiver = this;

        if(!this.network_manager.is_connected())
        {
            this.user_state = USER_STATE.CONNECTED;
            this.network_manager.connect();
        }
        else
        {
            on_connected();
        }
    }

    IEnumerator after_connected()
    {
        // BattleRoom의 게임오버 상태에서 마우스 입력을 통해 메인 화면으로 넘어오도록 되어 있는데,
        // 한 프레임 내에서 이 코루틴이 실행될 경우 아직 마우스 입력이 남아있는 것으로 판단되어
        // 메인 화면으로 돌아오자마자 ENTER_GAME_ROOM_REQ 패킷을 보내는 일이 발생한다.
        // 따라서 강제로 한 프레임을 건너뛰어 다음 프레임부터 코루틴의 내용이 수행될 수 있도록 한다.
        yield return new WaitForEndOfFrame();

        while(true)
        {
            if(USER_STATE.CONNECTED == this.user_state)
            {
                if(Input.GetMouseButtonDown(0))
                {
                    this.user_state = USER_STATE.WAITING_MATCHING;

                    CPacket msg = CPacket.create((short)PROTOCOL.ENTER_GAME_ROOM_REQ);
                    this.network_manager.send(msg);

                    StopCoroutine("after_connected");
                }
            }

            yield return 0;
        }
    }

    private void OnGUI()
    {
        switch(this.user_state)
        {
            case USER_STATE.NOT_CONNECTED:
                break;

            case USER_STATE.CONNECTED:
                {
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), this.bg);
                }
                break;

            case USER_STATE.WAITING_MATCHING:
                {
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), this.bg);
                    GUI.DrawTexture(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 50, 200, 82), this.waiting_img);
                }
                break;
        }
    }

    /// <summary>
    /// 서버에 접속이 완료되면 호출된다.
    /// </summary>
    public void on_connected()
    {
        this.user_state = USER_STATE.CONNECTED;

        StartCoroutine("after_connected");
    }

    /// <summary>
    /// 패킷을 수신했을 때 호출된다.
    /// </summary>
    public void on_recv(CPacket msg)
    {
        PROTOCOL protocol_id = (PROTOCOL)msg.pop_protocol_id();

        switch(protocol_id)
        {
            case PROTOCOL.START_LOADING:
                {
                    byte player_index = msg.pop_byte();

                    this.battle_room.gameObject.SetActive(true);
                    this.battle_room.start_loading(player_index);
                    gameObject.SetActive(false);
                }
                break;
        }
    }
}
