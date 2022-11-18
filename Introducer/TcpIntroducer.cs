/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/12/2022 9:11:56 PM
 * * * * * * * * * * * * *
 * 1. 중개 서버로 공인망에 위치함
 * 2. 접속한 클라이언트들간에 홀펀칭 진행을 도와줌
 * 
 *
 * * * * * * * * * * * * * 
 */

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using CTree;

using MoreLinq;

using Shared;

namespace Introducer;

public class TcpIntroducer
{
    private readonly TcpListener _tcpListener;
    private readonly Dictionary<long, TcpSession> _sessions;
    private readonly SessionPktParser _pktParser;
    
    private readonly Thread _acceptThread;
    private readonly Thread _clearThread;

    public IPEndPoint? LocalEndPoint => _tcpListener.LocalEndpoint as IPEndPoint;

    public TcpIntroducer(int port)
    {
        _tcpListener = new TcpListener(IPEndPoint.Parse($"0.0.0.0:{port}"));
        _sessions = new Dictionary<long, TcpSession>();
        _acceptThread = new Thread(AcceptThreadMain) { IsBackground = true };
        _clearThread = new Thread(ClearThreadMain) { IsBackground = true };
        _pktParser = new SessionPktParser(this);
        _pktParser.Initialize();
    }

    public void Start()
    {
        _tcpListener.Start();
        _acceptThread.Start();
        _clearThread.Start();
    }

    public void Stop()
    {
        lock (this)
        {
            _sessions.Values.ForEach(x => x.Disconnect());
            _sessions.Clear();
        }

        _tcpListener.Stop();
        _acceptThread.Join();
        _clearThread.Join();
    }

    private void AcceptThreadMain()
    {
        try
        {
            while (true)
            {
                var session = new TcpSession(_tcpListener.AcceptTcpClient(), _pktParser);
                session.OnDisconnected += Session_OnDisconnected;
                session.OnReceived += Session_OnReceived;
                session.OnSent += Session_OnSent;
                session.ReceiveAsync();
            }
        }
        catch (Exception e)
        {
            ConsoleEx.PrintExecption(e);
        }
    }

    private void ClearThreadMain()
    {
        List<TcpSession> disconnected = new ();

        while (true)
        {
            lock (this)
            {
                _sessions.Values.ForEach(x =>
                {
                    if (!x.IsConnected())
                    {
                        x.DisconnectionTime += 100;

                        // 약 5초동안 재연결되지 않는 경우 연결 끊어줌
                        if (x.DisconnectionTime >= 5000)
                            disconnected.Add(x);
                    }
                });

                disconnected.ForEach(x =>
                {
                    _sessions.Remove(x.Id);
                    ConsoleEx.LogLine($"클라이언트 타움아웃 종료 {x.Id} [남은 인원: {_sessions.Count}]", ConsoleColor.Red);
                });

                if (disconnected.Count > 0)
                    BroadcastSessionInfos();

                disconnected.Clear();
            } // lock end

            Thread.Sleep(100);
        }
    }

    private void Session_OnSent(TcpClientEx client, PktBase pkt, int sentBytes)
    {
        TcpSession? session = client as TcpSession;
        Debug.Assert(session != null);

        ConsoleEx.LogLine($"클라이언트 {session.Id}({client.RemoteEndPoint})로 {pkt}[{sentBytes}바이트] 전송완료", ConsoleColor.Yellow);
    }

    private void Session_OnReceived(TcpClientEx client, PktBase pkt, int receivedBytes)
    {
        TcpSession? session = client as TcpSession;
        Debug.Assert(session != null);

        ConsoleEx.LogLine($"클라이언트 {session.Id}({client.RemoteEndPoint})로부터 {pkt}[{receivedBytes}바이트] 수신완료", ConsoleColor.Green);
    }

    // 연결 성공 기준을 상대의 Private 주소를 획득한 시점으로 하자.
    public void Session_OnConnected(TcpSession session)
    {
        lock (this)
        {
            // 이미 존재하는 ID인경우 연결해준다.
            if (_sessions.ContainsKey(session.Id))
            {
                _sessions[session.Id].Disconnect();
                _sessions[session.Id] = session;
                ConsoleEx.WriteLine($"클라이언트 {session.Id}({session.RemoteEndPoint})가 재접속하였습니다.", ConsoleColor.Green);
            }
            else
            {
                _sessions.Add(session.Id, session);
                ConsoleEx.WriteLine($"클라이언트 {session.Id}({session.RemoteEndPoint})가 신규 접속하였습니다.", ConsoleColor.Green);
            }
        }

        BroadcastSessionInfos();
    }

    public TcpSession? GetSession(long id)
    {
        using var _ = DisposeLock.AutoLock(this);
        if (!_sessions.ContainsKey(id))
            return null;

        return _sessions[id];
    }

    private void Session_OnDisconnected(TcpClientEx client, bool safe)
    {
        TcpSession? session = client as TcpSession;
        Debug.Assert(session != null);

        // 홀펀칭 진행중이고 안전하게 종료된 경우에는 세션목록에서 제거하지 않는다.
        if (safe && session.IsHolePunching)
            return;

        lock (this)
        {
            if (_sessions.ContainsKey(session.Id))
                _sessions.Remove(session.Id);
        }

        if (safe)
            ConsoleEx.WriteLine($"클라이언트 안전하게 종료 {session.Id} [남은 인원: {_sessions.Count}]", ConsoleColor.Green);
        else
            ConsoleEx.WriteLine($"클라이언트 안전하지 않게 종료 {session.Id} [남은 인원: {_sessions.Count}]", ConsoleColor.Red);

        BroadcastSessionInfos();
    }

    public void Broadcast(PktBase pkt) => GetSessionList().ForEach(x => x.SendAsync(pkt));

    public void BroadcastSessionInfos()
    {
        var sessions = GetSessionList();
        var ptk = new PktSessionListAck();
        sessions.ForEach(x => ptk.SessionInfos.Add(x.GetSessionInfo()));
        Broadcast(ptk);
    }

    public void SendAsync(long id, PktBase pkt)
    {
        using var _ = DisposeLock.AutoLock(this);
        if (_sessions.ContainsKey(id))
            _sessions[id].SendAsync(pkt);
    }

    public List<TcpSession> GetSessionList()
    {
        var sessions = new List<TcpSession>();
        using var _ = DisposeLock.AutoLock(this);
        sessions.AddRange(_sessions.Values);
        return sessions;
    }

    public void PrintSessions()
    {
        ConsoleTree commandTree = new("[접속중인 유저 목록]")
        {
            BridgeForegroundColor = ConsoleColor.Red,
            ItemForegroundColor = ConsoleColor.Cyan,
        };

        commandTree.Add("[●]: 연결됨");
        commandTree.Add("[  ]: 연결 안됨");
        commandTree.AddDummy();

        using var _ = DisposeLock.AutoLock(this);
        var sessions = _sessions.Values.ToList();
        foreach (TcpSession session in sessions)
        {
            string info = $"[{session.Id}]";
            if (session.IsHolePunching)
                info += "[홀펀칭 진행중]";

            ConsoleTreeItem userItem = new (info)
            {
                ForegroundColor = session.IsConnected() ? 
                    ConsoleColor.Green : 
                    ConsoleColor.Red
            };

            userItem.Add($"PrivateEndPoint: {session.PrivateEndPoint}");
            userItem.Add($"PublicEndPoint: {session.PublicEndPoint}");
            var child = userItem.AddReturnChild($"LocalEndPoint: {session.LocalEndPoint}");

            List<long> connectedSessions = session.ConnectedSessions;
            int connectedSessionCount = connectedSessions.Count;

            if (connectedSessionCount > 0)
            {
                child.Add(new ConsoleTreeItem("[P2P 연결된 대상]") { ForegroundColor = ConsoleColor.DarkYellow });
                for (int j = 0; j < connectedSessionCount; j++)
                    child.Add(new ConsoleTreeItem($"{connectedSessions[j]}") { ForegroundColor = ConsoleColor.DarkYellow });
            }

            commandTree.Add(userItem);
        }

        ConsoleEx.Lock();
        commandTree.Print();
        ConsoleEx.Unlock();
    }

    public void BroadcastRandomMessage(string message) => Broadcast(new PktServerMessage { Message = message });

    public void SendMessage(long id, string message)
    {
        using var _ = DisposeLock.AutoLock(this);
        if (!_sessions.ContainsKey(id))
        {
            Console.WriteLine("대상을 찾지 못했습니다.", ConsoleColor.Red);
            return;
        }

        _sessions[id].SendAsync(new PktServerMessage { Message = message });
    }
}

