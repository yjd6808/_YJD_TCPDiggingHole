/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/12/2022 10:22:18 PM
 * * * * * * * * * * * * *
 * 서버가 수신한 메시지에 대한 처리
 * 
 *
 * * * * * * * * * * * * * 
 */

using System.Diagnostics;

using Shared;

namespace Introducer;

public class SessionPktParser : PktParser<TcpSession>
{
    private TcpIntroducer _introducer;

    public SessionPktParser(TcpIntroducer introducer) : base()
    {
        _introducer = introducer;
    }

    public override PktParser<TcpSession> Initialize()
    {
        _ptkHandlers.Add(Pkt.Type.ConnectionMessage, ConnectionMessage);
        _ptkHandlers.Add(Pkt.Type.P2PConnectRequest, P2PConnectRequest);
        _ptkHandlers.Add(Pkt.Type.P2PConnectSuccess, P2PConnectSuccess);
        _ptkHandlers.Add(Pkt.Type.EchoMessage, EchoMessage);
        return this;
    }

    private void ConnectionMessage(PktBase ptkBase, TcpSession receiver)
    {
        var pkt = ptkBase as PktConnectionMessage;
        Debug.Assert(pkt != null);

        // 유저 정보 갱신
        receiver.UpdatePrivateEndPoint(pkt.PrivateEndPoint);
        receiver.SendAsync(new PktRefreshInfoAck(receiver.GetSessionInfo()));

        _introducer.Session_OnConnected(receiver);
    }

    private void EchoMessage(PktBase pktBase, TcpSession receiver)
    {
        var pkt = pktBase as PktEchoMessage;
        Debug.Assert(pkt != null);

        ConsoleEx.WriteLine($"클라이언트 {receiver.Id}({receiver.RemoteEndPoint})로부터 에코 메시지 수신: {pkt.Message}", ConsoleColor.Magenta);
        receiver.SendAsync(new PktEchoMessage { Message = pkt.Message });
    }

    private void P2PConnectRequest(PktBase ptkBase, TcpSession receiver)
    {
        var pkt = ptkBase as PktP2PConnectRequest;
        Debug.Assert(pkt != null);

        TcpSession? target = _introducer.GetSession(pkt.TargetId);

        if (target == null)
        {
            _introducer.SendMessage(receiver.Id, "대상이 연결되어있지 않습니다.");
            return;
        }

        // A가 서버한테 B와 연결하고 싶다고 하였다.
        // 서버는 A에게 B와 홀펀칭 프로세스를 시작하라고 하고
        receiver.SendAsync(new PktP2PConnectRequestAck() { TargetId = pkt.TargetId });
        receiver.IsHolePunching = true;

        // 서버는 B에게 A와 홀펀칭 프로세스를 시작하라고 한다.
        target.SendAsync(new PktP2PConnectRequestAck() { TargetId = pkt.RequesterId });
        target.IsHolePunching = true;
    }

    private void P2PConnectSuccess(PktBase ptkBase, TcpSession receiver)
    {
        var pkt = ptkBase as PktP2PConnectSuccess;
        Debug.Assert(pkt != null);

        receiver.UpdateConnectedPeers(pkt.ConnectedPeers);
        receiver.IsHolePunching = false;
    }


}
