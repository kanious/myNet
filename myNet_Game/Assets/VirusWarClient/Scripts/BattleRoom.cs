using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirusWarGameUnity;
using myNet;

public class BattleRoom : MonoBehaviour
{
    enum GAME_STATE
    {
        READY,
        STARTED
    }

    public static readonly int COL_COUNT = 7;

    List<Player> players;                   // 게임에 참여한 플레이어들.
    List<short> board;                      // 진행 중인 게임 보드판.
    List<short> table_board;                // 0 ~ 49 까지의 인덱스를 갖고 있는 보드판 데이터.
    List<short> available_attack_cells;     // 공격 가능한 범위를 나타낼 때 사용하는 리스트.
    byte current_player_index;              // 현재 턴을 진행중인 플레이어 인덱스.
    byte player_me_index;                   // 서버에서 지정해준 본인의 플레이어 인덱스.
    byte step;                              // 상황에 따른 터치 입력을 처리하기 위한 변수.

    MainTitle main_title;                   // 게임 종료 후 메인으로 돌아갈 때 사용하기 위한 MainTitle 객체의 참조.
    NetworkManager network_manager;         // 네트워크 데이터의 송/수신을 위한 네트워크 매니저 참조.
    GAME_STATE game_state;                  // 게임 상태에 따라 각각 다른 GUI 모습을 구현하기 위한 상태 변수.
    byte win_player_index;                  // 승리한 플레이어 인덱스. 무승부일 경우에는 byte.MaxValue가 들어간다.
    ImageNumber score_images;               // 점수를 표시하기 위한 이미지 숫자 객체.
    BattleInfoPanel battle_info;            // 현재 게임 상황 정보를 가지고 있는 객체.
    bool is_game_finished;                  // 게임이 종료되었는지를 나타내는 플래그.

    List<Texture> img_players;
    Texture background;
    //Texture blank_image;
    Texture game_board;
    //Texture graycell;
    Texture focus_cell;
    Texture win_img;
    Texture lose_img;
    Texture draw_img;
    Texture gray_transparent;

    // OnGUI 메서드에서 호출할 델리게이트.
    delegate void GUIFUNC();
    GUIFUNC draw;

    private void Awake()
    {
        this.board = new List<short>();
        this.table_board = new List<short>();
        this.available_attack_cells = new List<short>();
        this.main_title = GameObject.Find("MainTitle").GetComponent<MainTitle>();
        this.network_manager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        this.game_state = GAME_STATE.READY;
        this.win_player_index = byte.MaxValue;
        this.score_images = gameObject.AddComponent<ImageNumber>();
        this.battle_info = gameObject.AddComponent<BattleInfoPanel>();

        this.img_players = new List<Texture>();
        this.img_players.Add(Resources.Load("images/red") as Texture);
        this.img_players.Add(Resources.Load("images/blue") as Texture);
        this.background = Resources.Load("images/gameboard_bg") as Texture;
        //this.blank_image = Resources.Load("images/blank") as Texture;
        this.game_board = Resources.Load("images/gameboard") as Texture;
        //this.graycell = Resources.Load("images/graycell") as Texture;
        this.focus_cell = Resources.Load("images/border") as Texture;
        this.win_img = Resources.Load("images/win") as Texture;
        this.lose_img = Resources.Load("images/lose") as Texture;
        this.draw_img = Resources.Load("images/draw") as Texture;
        this.gray_transparent = Resources.Load("images/gray_transparent") as Texture;

        Screen.SetResolution(800, 480, false);
    }

    void reset()
    {
        // 보드판 데이터를 모두 초기화한다.
        this.board.Clear();
        this.table_board.Clear();
        for(int i = 0; i < COL_COUNT * COL_COUNT; ++i)
        {
            this.board.Add(short.MaxValue);
            this.table_board.Add((short)i);
        }

        // 보드판에 각 플레이어들의 위치를 입력한다.
        this.players.ForEach(obj =>
        {
            obj.cell_indexes.ForEach(cell =>
            {
                this.board[cell] = obj.player_index;
            });
        });
    }

    void clear()
    {
        this.current_player_index = 0;
        this.step = 0;
        this.draw = this.on_gui_playing;
        this.is_game_finished = false;
    }

    /// <summary>
    /// 게임방에 입장할 때 호출된다. 리소스 로딩을 시작한다.
    /// </summary>
    public void start_loading(byte player_me_index)
    {
        clear();

        this.network_manager.message_receiver = this;
        this.player_me_index = player_me_index;

        CPacket msg = CPacket.create((short)PROTOCOL.LOADING_COMPLETED);
        this.network_manager.send(msg);
    }
    
    /// <summary>
    /// 패킷을 수신했을 때 호출된다.
    /// </summary>
    void on_recv(CPacket msg)
    {
        PROTOCOL protocol_id = (PROTOCOL)msg.pop_protocol_id();

        switch(protocol_id)
        {
            case PROTOCOL.GAME_START:
                on_game_start(msg);
                break;

            case PROTOCOL.PLAYER_MOVED:
                on_player_moved(msg);
                break;

            case PROTOCOL.START_PLAYER_TURN:
                on_start_player_turn(msg);
                break;

            case PROTOCOL.ROOM_REMOVED:
                on_room_removed();
                break;

            case PROTOCOL.GAME_OVER:
                on_game_over(msg);
                break;
        }
    }

    void on_game_start(CPacket msg)
    {
        this.players = new List<Player>();

        byte count = msg.pop_byte();
        for(byte i = 0; i < count; ++i)
        {
            byte player_index = msg.pop_byte();

            GameObject obj = new GameObject(string.Format("player{0}", i));
            Player player = obj.AddComponent<Player>();
            player.initialize(player_index);
            player.clear();

            byte virus_count = msg.pop_byte();
            for(byte index = 0; index < virus_count; ++index)
            {
                short position = msg.pop_int16();
                player.add(position);
            }

            this.players.Add(player);
        }

        this.current_player_index = msg.pop_byte();
        reset();

        this.game_state = GAME_STATE.STARTED;
    }

    void on_player_moved(CPacket msg)
    {
        byte player_index = msg.pop_byte();
        short from = msg.pop_int16();
        short to = msg.pop_int16();

        StartCoroutine(on_selected_cell_to_attack(player_index, from, to));
    }

    void on_start_player_turn(CPacket msg)
    {
        phase_end();

        this.current_player_index = msg.pop_byte();
    }

    void on_room_removed()
    {
        if(!is_game_finished)
        {
            back_to_main();
        }
    }

    void on_game_over(CPacket msg)
    {
        this.is_game_finished = true;
        this.win_player_index = msg.pop_byte();
        this.draw = this.on_gui_game_result;
    }

    void back_to_main()
    {
        this.main_title.gameObject.SetActive(true);
        this.main_title.enter();

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if(this.is_game_finished)
        {
            if(Input.GetMouseButtonDown(0))
            {
                back_to_main();
            }
        }
    }

    float ratio = 1f;
    private void OnGUI()
    {
        this.draw();
    }

    /// <summary>
    /// 게임 진행 화면 그리기.
    /// </summary>
    void on_gui_playing()
    {
        if(GAME_STATE.STARTED != this.game_state)
        {
            return;
        }

        this.ratio = Screen.width / 800f;

        draw_board();
    }

    void on_gui_game_result()
    {
        on_gui_playing();

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), this.gray_transparent);
        GUI.BeginGroup(new Rect(Screen.width / 2 - 173, Screen.height / 2 - 84, this.win_img.width, this.win_img.height));

        if(byte.MaxValue == this.win_player_index)
        {
            GUI.DrawTexture(new Rect(0, 0, this.draw_img.width, this.draw_img.height), this.draw_img);
        }
        else
        {
            if(this.player_me_index == this.win_player_index)
            {
                GUI.DrawTexture(new Rect(0, 0, this.win_img.width, this.win_img.height), this.win_img);
            }
            else
            {
                GUI.DrawTexture(new Rect(0, 0, this.lose_img.width, this.lose_img.height), this.lose_img);
            }
        }

        // 자기 자신의 플레이어 이미지 출력.
        Texture character = this.img_players[this.player_me_index];
        GUI.DrawTexture(new Rect(28, 43, character.width, character.height), character);

        GUI.EndGroup();
    }

    void draw_board()
    {
        float scaled_height = 480f * ratio;
        //float gap_height = Screen.height - scaled_height;

        float outline_left = 0f;
        float outline_top = 0f;// gap_height * 0.5f;
        float outline_width = Screen.width;
        float outline_height = scaled_height;

        float hor_center = outline_width * 0.5f;
        float ver_center = outline_height * 0.5f;

        GUI.BeginGroup(new Rect(0, 0, outline_width, outline_height));

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), this.background);

        // 점수 표시.
        Rect redteam_rect = new Rect(outline_left + 20 * ratio, ver_center - 60 * ratio, 100 * ratio, 60 * ratio);
        Rect blueteam_rect = new Rect(outline_width - 120 * ratio, ver_center - 60 * ratio, 100 * ratio, 60 * ratio);
        this.score_images.print(this.players[0].get_virus_count(), redteam_rect.xMin, redteam_rect.yMin, ratio);
        this.score_images.print(this.players[1].get_virus_count(), blueteam_rect.xMin, blueteam_rect.yMin, ratio);

        // 보드 그리기.
        GUI.DrawTexture(new Rect(0, outline_top, outline_width, outline_height), this.game_board);

        // 현재 진행중인 턴 표시.
        this.battle_info.draw_turn_info(this.current_player_index, ratio);
        this.battle_info.draw_myinfo(this.player_me_index, ratio);

        int width = (int)(60 * ratio);
        int celloutline_width = width * BattleRoom.COL_COUNT;
        float half_celloutline_width = celloutline_width * 0.5f;

        GUI.BeginGroup(new Rect(hor_center - half_celloutline_width,
            ver_center - half_celloutline_width + outline_top, celloutline_width, celloutline_width));

        //List<int> current_turn = new List<int>();
        short index = 0;
        for (int row = 0; row < BattleRoom.COL_COUNT; ++row)
        {
            int gap_y = 0;//(row * 1);
            for (int col = 0; col < BattleRoom.COL_COUNT; ++col)
            {
                int gap_x = 0;//(col * 1);

                Rect cell_rect = new Rect(col * width + gap_x, row * width + gap_y, width, width);
                if (GUI.Button(cell_rect, ""))
                {
                    on_click(index);
                }

                if (this.board[index] != short.MaxValue)
                {
                    int player_index = this.board[index];
                    GUI.DrawTexture(cell_rect, this.img_players[player_index]);

                    if (this.current_player_index == player_index)
                    {
                        GUI.DrawTexture(cell_rect, this.focus_cell);
                    }
                }

                if (this.available_attack_cells.Contains(index))
                {
                    GUI.DrawTexture(cell_rect, this.focus_cell);
                }

                ++index;
            }
        }
        GUI.EndGroup();
        GUI.EndGroup();
    }

    short selected_cell = short.MaxValue;
    void on_click(short cell)
    {
        // 자신의 차례가 아니면 처리하지 않고 리턴한다.
        if(this.player_me_index != this.current_player_index)
        {
            return;
        }

        switch(this.step)
        {
            case 0:
                {
                    if(validate_begin_cell(cell))
                    {
                        this.selected_cell = cell;
                        this.step = 1;

                        refresh_available_cells(this.selected_cell);
                    }
                }
                break;

            case 1:
                {
                    if(this.players[this.current_player_index].cell_indexes.Exists(obj => obj == cell))
                    {
                        this.selected_cell = cell;
                        refresh_available_cells(this.selected_cell);
                        break;
                    }
                    
                    foreach(Player player in this.players)
                    {
                        if(player.cell_indexes.Exists(obj => obj == cell))
                        {
                            return;
                        }
                    }

                    if(2 < Helper.get_distance(this.selected_cell, cell))
                    {
                        return;
                    }

                    CPacket msg = CPacket.create((short)PROTOCOL.MOVING_REQ);
                    msg.push(this.selected_cell);
                    msg.push(cell);
                    this.network_manager.send(msg);

                    this.step = 2;
                }
                break;
        }
    }

    IEnumerator on_selected_cell_to_attack(byte player_index, short from, short to)
    {
        byte distance = Helper.howfar_from_clicked_cell(from, to);
        if(1 == distance)
        {
            yield return StartCoroutine(reproduce(to));
        }
        else if(2 == distance)
        {
            this.board[from] = short.MaxValue;
            this.players[player_index].remove(from);
            yield return StartCoroutine(reproduce(to));
        }

        CPacket msg = CPacket.create((short)PROTOCOL.TURN_FINISHED_REQ);
        this.network_manager.send(msg);

        yield return 0;
    }

    void phase_end()
    {
        this.step = 0;
        this.available_attack_cells.Clear();
    }

    void refresh_available_cells(short cell)
    {
        this.available_attack_cells = Helper.find_available_cells(cell, this.table_board, this.players);
    }

    void clear_available_attacking_cells()
    {
        this.available_attack_cells.Clear();
    }

    IEnumerator reproduce(short cell)
    {
        Player current_player = this.players[this.current_player_index];
        Player other_player = this.players.Find(obj => obj.player_index != this.current_player_index);

        clear_available_attacking_cells();

        this.board[cell] = current_player.player_index;
        current_player.add(cell);

        yield return new WaitForSeconds(0.5f);

        List<short> neighbors = Helper.find_neighbor_cells(cell, other_player.cell_indexes, 1);
        foreach(short obj in neighbors)
        {
            this.board[obj] = current_player.player_index;
            current_player.add(obj);

            other_player.remove(obj);

            yield return new WaitForSeconds(0.2f);
        }
    }

    bool validate_begin_cell(short cell)
    {
        return this.players[this.current_player_index].cell_indexes.Exists(obj => obj == cell);
    }
}
