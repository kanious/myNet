using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MyNetUnity
{
    public enum PROTOCOL : short
    {
        BEGIN = 0,

        CHAT_MSG_REQ = 1,       // REQ는 뭔가에 대한 요청을 의미
        CHAT_MSG_ACK = 2,       // ACK는 REQ에 대한 응답을 의미

        END
    }
}
