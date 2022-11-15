/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/12/2022 9:38:23 PM
 * * * * * * * * * * * * *
 * 중개서버와 연결될 클라이언트
 *
 */

using System.Net;

using Shared;

namespace Participant;

public class TcpParticipant : TcpClientEx
{
    public long Id { get; set; }

    public IPEndPoint PublicEndPoint
    {
        get
        {
            using var _ = DisposeLock.AutoLock(this);
            return _publicEndPoint;
        }
    }

    public Dictionary<long, TcpPeer> OtherParticipants { get; }


    private IPEndPoint _publicEndPoint;
    

    public TcpParticipant(ParticipantPktParser parser) : base(parser)
    {
        Id = -1;
        OtherParticipants = new Dictionary<long, TcpPeer>();
        _publicEndPoint= IPEndPoint.Parse("0.0.0.0:0");
        
    }

    public void UpdateMe(long id, IPEndPoint publicEp)
    {
        using var _ = DisposeLock.AutoLock(this);
        Id = id;
        _publicEndPoint = publicEp;
    }

    public void UpdatePeers(List<PktSessionInfo> infos)
    {
        using var _ = DisposeLock.AutoLock(this);
        List<TcpPeer> disconnected = new ();

        // 새로 갱신된 리스트에 없는 대상이 삭제된 녀석임
        foreach (var participant in OtherParticipants.Values)
        {
            if (infos.FindIndex(x => x.Id == participant.Id) == -1)
            {
                disconnected.Add(participant);
            }
        }

        disconnected.ForEach(x =>
        {
            OtherParticipants[x.Id].Disconnect();
            OtherParticipants.Remove(x.Id);
        });

        // 없는 대상은 새로 추가해줌
        for (int i = 0; i < infos.Count; i++)
        {
            var info = infos[i];

            if (info.Id == Id)
                continue;

            if (!OtherParticipants.ContainsKey(info.Id))
            {
                var pktParser = new PeerPktParser();
                pktParser.Initialize();

                var tcpPeer = new TcpPeer(this, info, pktParser);
                tcpPeer.OnHolepunchSuccess += OnTcpPeerOnOnHolepunchSuccess;
                OtherParticipants.Add(info.Id, tcpPeer);
                ConsoleEx.Write($"\n새로운 클라이언트가 들어왔습니다. [{info.Id}]", ConsoleColor.DarkYellow);
            }
            else
            {
                OtherParticipants[info.Id].UpdatePeerInfo(info);
            }
        }
        ConsoleEx.WriteLine();
    }

    public void PrintPeers()
    {
        using var _ = DisposeLock.AutoLock(this);

        if (OtherParticipants.Count == 0)
        {
            ConsoleEx.WriteLine("접속중인 유저가 없습니다.");
            return;
        }

        ConsoleEx.WriteLine("[접속중인 유저 목록]");
        ConsoleEx.WriteLine(" ├ [●]: P2P 연결됨");
        ConsoleEx.WriteLine(" ├ [  ]: P2P 연결 안됨");
        ConsoleEx.WriteLine(" │");

        List<TcpPeer> list = OtherParticipants.Values.ToList();
        bool prevP2PConn = false;

        for (int i = 0; i < list.Count; i++)
        {
            var peer = list[i];
            bool connected = peer.IsConnected();
            string connectedStr = connected ? "●" : "  ";

            if (i == list.Count - 1)
                ConsoleEx.WriteLine($" └ [{peer.Id}][{connectedStr}] {peer.PrivateEndPoint} {peer.PublicEndPoint}",
                    connected ? ConsoleColor.Green : ConsoleColor.DarkGray);
            else
                ConsoleEx.WriteLine($" ├ [{peer.Id}][{connectedStr}] {peer.PrivateEndPoint} {peer.PublicEndPoint}",
                    connected ? ConsoleColor.Green : ConsoleColor.DarkGray);

            if (connected)
            {
                string bridgeStr = prevP2PConn ? "    " : " │  ";

                if (i == list.Count - 1)
                    bridgeStr = "    ";

                ConsoleEx.WriteLine($"{bridgeStr}├ 연결타입: {peer.ConnectionTypeToString()}", ConsoleColor.DarkYellow);
                ConsoleEx.WriteLine($"{bridgeStr}├ LocalEndPoint: {peer.LocalEndPoint}", ConsoleColor.Cyan);
                ConsoleEx.WriteLine($"{bridgeStr}└ RemoteEndPoint: {peer.RemoteEndPoint}", ConsoleColor.Cyan);
                prevP2PConn = true;
                continue;
            }

            prevP2PConn = false;
        }
    }

    private async void OnTcpPeerOnOnHolepunchSuccess(TcpPeer target)
    {
        ConsoleEx.DebugLogLine("홀펀칭 성공!", ConsoleColor.Green);

        PrintPeers();

        SendAsync(new PktP2PConnectSuccess() { TargetId = target.Id });


        ///*
        // * 서버가 이전 세션을 정리할 수 있도록 시간을 준다.
        // * 이걸 안주면 서버에서 Session_OnConnected보다 Session_OnDisconnected보다 먼저 호출되버려서
        // * 홀펀칭 성공 후 바로 목록에서 지워져버리는 문제가 있을 수 있다.
        // * 이걸 깔끔하게 해결할려면 서버와 패킷을 주고받아서 동기화를 해줘야하는데 그렇게까지 하기에는 테스트용 프로그램이므로 귀찮음
        // */
        //await Task.Delay(500);

        //ConsoleEx.DebugLogLine("서버 재접속 시도 (로컬 포트 변경)");

        //// 이전 소켓은 Disposed 되었기 때문에 새로운 포트에 새롭게 바인딩 해줘야함.
        //// 기존 서버와 연결되었던 Local 포트는 다른 피어와 연결되었으므로 사용할 수 없음
        //BindNew();

        //if (TryConnectSynchronously(Config.IntroducerEndPoint, 1000))
        //{
        //    SendAsync(new PktP2PConnectSuccess() { TargetId = target.Id });
        //    ConsoleEx.DebugLogLine("서버에 재접속하였습니다.");
        //}
        //else
        //{
        //    ConsoleEx.DebugLogLine("긴급! 긴급! 서버 재접속 실패", ConsoleColor.Red);
        //}
    }

    // startHolePunch = false인 경우는 연결하고 싶다고 요청
    // A -> 서버에게 B와 연결하고 싶다고 요청을 보내는 경우임
    // 서버가 A와 B에게 PktP2PConnectRequestAck를 보내면
    // A는 startHolePunch = true로하여 B를 대상으로 홀펀치 프로세스를 진행하고
    // B도 startHolePunch = true로하여 A를 대상으로 홀펀치 프로세스를 진행한다.
    public void ConnectToPeer(long targetId, bool startHolePunch = false)
    {
        lock (this)
        {
            if (!OtherParticipants.ContainsKey(targetId))
            {
                ConsoleEx.WriteLine("\n대상을 찾지 못했습니다.");
                return;
            }

            if (OtherParticipants[targetId].IsConnected())
            {
                ConsoleEx.WriteLine("\n이미 연결되어있습니다. ㅠㅠ");
                return;
            }

            if (startHolePunch)
            {
                OtherParticipants[targetId].StartHolePunchProcess();
                return;
            }
        }

        SendAsync(new PktP2PConnectRequest()
        {
            RequesterId = Id,       // 나
            TargetId = targetId     // 연결할 대상
        });
    }

    public void SendMessageToPeer(long id, string message)
    {
        using var _ = DisposeLock.AutoLock(this);

        if (!OtherParticipants.ContainsKey(id))
        {
            ConsoleEx.WriteLine("대상을 찾지 못했습니다.");
            return;
        }

        if (!OtherParticipants[id].TrySendAsync(new PktP2PMessage { Sender = Id, Message = message }))
            ConsoleEx.WriteLine("대상과 아직 P2P 연결이 되지 않았습니다.");
    }

    public void DisconnectPeer(long id)
    {
        using var _ = DisposeLock.AutoLock(this);
        if (!OtherParticipants.ContainsKey(id))
        {
            ConsoleEx.WriteLine("대상을 찾지 못했습니다.");
            return;
        }

        OtherParticipants[id].Disconnect();
    }

    public void DisconnectAllPeers()
    {
        using var _ = DisposeLock.AutoLock(this);
        OtherParticipants.Values.ToList().ForEach(x => x.Disconnect());
    }


    public List<long> GetConnectedPeers()
    {
        using var _ = DisposeLock.AutoLock(this);
        return OtherParticipants.Values
            .ToList()
            .FindAll(x => x.IsConnected())
            .Select(x => x.Id)
            .ToList();
    }
}
