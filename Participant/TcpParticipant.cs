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
    

    public TcpParticipant(IPktParser parser) : base(parser)
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
                var pktParser = new PeerPktParser().Initialize();
                var tcpPeer = new TcpPeer(this, info, pktParser);

                tcpPeer.OnHolepunchSuccess += TcpPeerOnHolepunchSuccess;
                tcpPeer.OnPeerDisconnected += TcpPeerOnDisconnected;

                OtherParticipants.Add(info.Id, tcpPeer);
                ConsoleEx.LogLine($"클라이언트 {info.Id}({info.PublicEndPoint})이 중개서버에 접속하였습니다.", ConsoleColor.Green);
            }
            else
            {
                OtherParticipants[info.Id].UpdatePeerInfo(info);
            }
        }
        ConsoleEx.WriteLine();
    }

    private void TcpPeerOnDisconnected(TcpPeer target, bool safeClosed)
    {
        if (safeClosed)
            ConsoleEx.LogLine($"클라이언트 {target.Id}({target.RemoteEndPoint})와의 P2P 연결이 안전하게 끊어졌습니다.", ConsoleColor.Green);
        else
            ConsoleEx.LogLine($"클라이언트 {target.Id}({target.RemoteEndPoint})와의 P2P 연결이 강제로 끊어졌습니다.", ConsoleColor.Red);

        PrintPeers();
        SendAsync(new PktP2PDisconnected() { ConnectedPeers = GetConnectedPeers() });
    }

    private void TcpPeerOnHolepunchSuccess(TcpPeer target)
    {
        ConsoleEx.LogLine($"[홀펀처: {target.ConnectionTypeToString()}] 클라이언트 {target.Id}({target.RemoteEndPoint})와 성공적으로 P2P 연결되었습니다.", ConsoleColor.Green);
        PrintPeers();
        SendAsync(new PktP2PConnectSuccess() { TargetId = target.Id, ConnectedPeers = GetConnectedPeers() });
    }

    public void PrintPeers()
    {
        using var _ = DisposeLock.AutoLock(this);

        if (OtherParticipants.Count == 0)
        {
            ConsoleEx.WriteLine("접속중인 유저가 없습니다.", ConsoleColor.Red);
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
                ConsoleEx.WriteLine("대상을 찾지 못했습니다.", ConsoleColor.Red);
                return;
            }

            if (OtherParticipants[targetId].IsConnected())
            {
                ConsoleEx.WriteLine("이미 연결되어있습니다. ㅠㅠ", ConsoleColor.Red);
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
            ConsoleEx.WriteLine("대상을 찾지 못했습니다.", ConsoleColor.Red);
            return;
        }

        if (!OtherParticipants[id].TrySendAsync(new PktP2PEchoMessage { Sender = Id, Message = message }))
            ConsoleEx.WriteLine("대상과 아직 P2P 연결이 되지 않았습니다.", ConsoleColor.Red);
    }

    public void DisconnectPeer(long id)
    {
        using var _ = DisposeLock.AutoLock(this);
        if (!OtherParticipants.ContainsKey(id))
        {
            ConsoleEx.WriteLine("대상을 찾지 못했습니다.", ConsoleColor.Red);
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
