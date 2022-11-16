// 1. 모든 통신 패킷 정의
// 2. 패킷 직렬화, 역질렬화 기능 추가

using ProtoBuf;

namespace Shared;

public static class Pkt
{
    public const int HeaderLength = 4;

    public static class Type
    {
        // 서버와 클라간 주고받는 패킷들
        public const int ConnectionMessage         = 10000;         // 클라 -> 서버 / 클라이언트가 서버에 연결됨과 동시에 자신의 사설 IP 주소를 전달함
        public const int EchoMessage               = 10001;         // 클라 -> 서버 -> 클라 / 클라가 서버한테 에코 메시지보내면 서버는 고대로 돌려보내줌
        public const int SessionList               = 10002;         // 클라 -> 서버 / 현재 서버에 접속중인 클라이언트 목록 요청
        public const int SessionListAck            = 10003;         // 서버 -> 클라 / 클라이언트 목록 전달
        public const int RefreshInfo               = 10004;         // 클라 -> 서버 / 서버에게 내 정보 다시 달라고 요청
        public const int RefreshInfoAck            = 10005;         // 서버 -> 클라 / 클라이언트 정보 전달 (Public 주소)
        public const int P2PConnectRequest         = 10006;         // 클라 -> 서버 / 클라 A가 서버에게 B와 연결하고 싶다고 요청
        public const int P2PConnectRequestAck      = 10007;         // 서버 -> 클라 / 서버는 B에게 A와 연결하라고 명령
        public const int P2PConnectSuccess         = 10008;         // 클라 -> 서버 / 홀펀칭 성공 후 서버에 재접속하면서 보내주는 메시지
        public const int P2PDisconnected           = 10009;         // 클라 -> 서버 / P2P 연결이 끊어진 경우 서버에 알려줌
        public const int ServerMessage             = 10015;         // 서버 -> 클라 / 서버가 클라한테 보내는 메시지


        // P2P로 주고받는 패킷들
        public const int P2PEchoMessage                = 10105;
    }

    public static readonly Dictionary<int, string> NameMap = new ()
    {
        { Type.ConnectionMessage       , "ConnectionMessage"     },
        { Type.EchoMessage             , "EchoMessage"           },
        { Type.SessionList             , "SessionList"           },
        { Type.SessionListAck          , "SessionListAck"        },
        { Type.RefreshInfo             , "RefreshInfo"           },
        { Type.RefreshInfoAck          , "RefreshInfoAck"        },
        { Type.P2PConnectRequest       , "P2PConnectRequest"     },
        { Type.P2PConnectRequestAck    , "P2PConnectRequestAck"  },
        { Type.P2PConnectSuccess       , "P2PConnectSuccess"     },
        { Type.ServerMessage           , "ServerMessage"         },
        { Type.P2PEchoMessage              , "P2PMessage"            }
    };

    public static PktBase ToPktBase(byte[] bytes, int offset, int size)
    {
        using var stream = new MemoryStream();
        stream.Write(bytes, offset, size);
        stream.Seek(0, SeekOrigin.Begin);
        return Serializer.Deserialize<PktBase>(stream);
    }

    public static byte[] ToByteArray<T>(T obj)
    {
        /*
         *  [패킷 길이][실제 패킷 데이터]
         *    4 byte  
         */

        MemoryStream stream = new MemoryStream();

        stream.Write(BitConverter.GetBytes(0), 0, HeaderLength);                       // 길이 (미리 공간 확보)
        Serializer.Serialize(stream, obj);

        byte[] result = stream.ToArray();
        BitConverter.GetBytes((int)stream.Length - HeaderLength).CopyTo(result, 0);
        return result;
    }
}

#region PktBase
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
[ProtoInclude(Pkt.Type.ConnectionMessage, typeof(PktConnectionMessage))]
[ProtoInclude(Pkt.Type.EchoMessage, typeof(PktEchoMessage))]
[ProtoInclude(Pkt.Type.SessionList, typeof(PktSessionList))]
[ProtoInclude(Pkt.Type.SessionListAck, typeof(PktSessionListAck))]
[ProtoInclude(Pkt.Type.RefreshInfo, typeof(PktRefreshInfo))]
[ProtoInclude(Pkt.Type.RefreshInfoAck, typeof(PktRefreshInfoAck))]
[ProtoInclude(Pkt.Type.P2PConnectRequest, typeof(PktP2PConnectRequest))]
[ProtoInclude(Pkt.Type.P2PConnectRequestAck, typeof(PktP2PConnectRequestAck))]
[ProtoInclude(Pkt.Type.P2PConnectSuccess, typeof(PktP2PConnectSuccess))]
[ProtoInclude(Pkt.Type.P2PDisconnected, typeof(PktP2PDisconnected))]
[ProtoInclude(Pkt.Type.ServerMessage, typeof(PktServerMessage))]
[ProtoInclude(Pkt.Type.P2PEchoMessage, typeof(PktP2PEchoMessage))]
public class PktBase
{
    public int Type { get; }

    public PktBase() {}
    public PktBase(int type)
    {
        Type = type;
    }

    public override string ToString()
    {
        if (!Pkt.NameMap.ContainsKey(Type))
            return "Unknown";

        return Pkt.NameMap[Type];
    }
}
#endregion

#region PktConnectionMessage
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktConnectionMessage : PktBase
{
    public string PrivateEndPoint { get; }
    

    public PktConnectionMessage() : base(Pkt.Type.ConnectionMessage)
    {
        PrivateEndPoint = string.Empty;
    }

    public PktConnectionMessage(string privateEndPoint) : base(Pkt.Type.ConnectionMessage)
    {
        PrivateEndPoint = privateEndPoint;
    }
    
}
#endregion
    
#region PktEchoMessage

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktEchoMessage : PktBase
{
    public string Message { get; set; }
    public PktEchoMessage() : base(Pkt.Type.EchoMessage) { }
}

#endregion

#region PktSessionInfo
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktSessionInfo
{
    public long Id { get; set; }
    public string? PrivateEndPoint { get; set; }
    public string? PublicEndPoint { get; set; }
}
#endregion

#region PktSessionList
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktSessionList : PktBase
{
    public PktSessionList() : base(Pkt.Type.SessionList)
    {
    }
}
#endregion

#region PktSessionListAck
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktSessionListAck : PktBase
{
    public readonly List<PktSessionInfo> SessionInfos = new();

    public PktSessionListAck() : base(Pkt.Type.SessionListAck)
    {
    }
}
#endregion

#region PktRefreshInfo

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktRefreshInfo : PktBase
{
    public PktRefreshInfo() : base(Pkt.Type.RefreshInfo) { }
}

#endregion

#region PktRefreshInfoAck

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktRefreshInfoAck : PktBase
{
    public PktSessionInfo Info { get; set; }
    public PktRefreshInfoAck() : base(Pkt.Type.RefreshInfoAck) {}
    public PktRefreshInfoAck(PktSessionInfo info) : this()
    {
        Info = info;
    }
}

#endregion

#region PktServerMessage

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktServerMessage : PktBase
{
    public string Message { get; set; }
    public PktServerMessage() : base(Pkt.Type.ServerMessage) { }
}

#endregion

#region PktP2PConnectRequest
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktP2PConnectRequest : PktBase
{
    public long RequesterId { get; set; }       // A
    public long TargetId { get; set; }          // B
    public PktP2PConnectRequest() : base(Pkt.Type.P2PConnectRequest) { }
}
#endregion

#region PktP2PConnectRequestAck
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktP2PConnectRequestAck : PktBase
{
    public long TargetId { get; set; }
    public PktP2PConnectRequestAck() : base(Pkt.Type.P2PConnectRequestAck) { }
}
#endregion

#region PktP2PConnectSuccess
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktP2PConnectSuccess : PktBase
{
    public long TargetId { get; set; }
    public List<long> ConnectedPeers { get; set; } = new ();
    public PktP2PConnectSuccess() : base(Pkt.Type.P2PConnectSuccess) { }
}
#endregion

#region PktP2PDisconnected
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktP2PDisconnected : PktBase
{
    public List<long> ConnectedPeers { get; set; } = new();
    public PktP2PDisconnected() : base(Pkt.Type.P2PDisconnected) { }
}
#endregion


#region PktP2PMessage

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PktP2PEchoMessage : PktBase
{
    public long Sender { get; set; }
    public string Message { get; set; }
    public bool Echo { get; set; }
    public PktP2PEchoMessage() : base(Pkt.Type.P2PEchoMessage) { }
}

#endregion
