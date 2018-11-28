using System;

namespace VirusWarGameServer
{
    public enum PROTOCOL : short
    {
        BEGIN = 0,

        START_LOADING = 1,          // 로딩 시작
        LOADING_COMPLETED = 2,      // 로딩 완료
        GAME_START = 3,             // 게임 시작
        START_PLAYER_TURN = 4,      // 턴 시작
        MOVING_REQ = 5,             // 클라이언트의 이동 요청
        PLAYER_MOVED = 6,           // 플레이어가 이동했음을 알림
        TURN_FINISHED_REQ = 7,      // 클라이언트의 턴 연출이 끝났음을 알림
        ROOM_REMOVED = 8,           // 상대방 플레이어가 나가 방에 삭제됨
        ENTER_GAME_ROOM_REQ = 9,    // 게임방 입장 요청
        GAME_OVER = 10,             // 게임 종료

        END
    }
}
