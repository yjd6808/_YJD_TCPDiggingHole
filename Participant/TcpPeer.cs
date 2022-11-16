/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/13/2022 3:15:53 PM
 * * * * * * * * * * * * *
 * P2P 연결될 클라이언트
 * * * * * * * * * * * * * 
 */

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Shared;
namespace Participant;

public delegate void OnHolepunchSuccessHandler(TcpPeer target);

public delegate void OnPeerDisconnectedHandler(TcpPeer target, bool safeClosed);

public class TcpPeer 
{
    private const int ListenSock = 0;
    private const int PublicSock = 1;
    private const int PrivateSock = 2;
    private const int MaxSock = 3;

    public long Id { get; private set; }
    public IPEndPoint PrivateEndPoint { get; private set; }
    public IPEndPoint PublicEndPoint { get; private set; }

    public IPEndPoint? LocalEndPoint => _participant.LocalEndPoint;
    public IPEndPoint RemoteEndPoint => PublicEndPoint;

    private readonly IPktParser _parser;
    private readonly List<IHolepuncher> _sockets;
    private readonly TcpParticipant _participant;
    private int _connectionType;
    private bool _processingHolePunching;

    public event OnHolepunchSuccessHandler? OnHolepunchSuccess;
    public event OnPeerDisconnectedHandler? OnPeerDisconnected;


    public TcpPeer(TcpParticipant participant, PktSessionInfo sessionInfo, IPktParser parser)
    {
        _sockets = new List<IHolepuncher>(MaxSock);
        _connectionType = -1;
        _parser = parser;
        _participant = participant;

        PrivateEndPoint = IPEndPoint.Parse("0.0.0.0:0");
        PublicEndPoint = IPEndPoint.Parse("0.0.0.0:0");

        UpdatePeerInfo(sessionInfo);
    }

    public void UpdatePeerInfo(PktSessionInfo sessionInfo)
    {
        using var _ = DisposeLock.AutoLock(this);

        // 이미 P2P 연결된 대상이면 무시함
        if (IsConnected())
            return;

        // 정보사항이 동일한 경우 업데이트 안함
        if (PrivateEndPoint.Equals(IPEndPoint.Parse(sessionInfo.PrivateEndPoint)) &&
            PublicEndPoint.Equals(IPEndPoint.Parse(sessionInfo.PublicEndPoint)) &&
            Id == sessionInfo.Id)
            return;

        // 홀펀칭 진행중이면 강제 중지
        StopHolePunchProcess();

        // 연결된 Private, Public, Listening 소켓 중에 연결된게 있으면 끊어줌
        Disconnect();

        ConsoleEx.DebugLogLine($"클라이언트 {sessionInfo.Id}의 정보 업데이트 진행");

        Id = sessionInfo.Id;
        PublicEndPoint = IPEndPoint.Parse(sessionInfo.PublicEndPoint);
        PrivateEndPoint = IPEndPoint.Parse(sessionInfo.PrivateEndPoint);

        IPEndPoint? localEndPoint = _participant.LocalEndPoint;
        Debug.Assert(localEndPoint != null);

        // Public, Private 정보가 업데이트 되었으므로 세로운 홀펀쳐들을 생성해줘야한다.
        var listeningPuncher = new ListeningHolepuncher(this, localEndPoint.Port, _parser);
        var publicPuncher = new ConnectingHolepuncher(this, localEndPoint.Port, PublicEndPoint, _parser, ConnectingHolepuncher.Type.PublicConnection);
        var privatePuncher = new ConnectingHolepuncher(this, localEndPoint.Port, PrivateEndPoint, _parser, ConnectingHolepuncher.Type.PrivateConnection);

        if (_sockets.Count == 0)
        {
            _sockets.Add(listeningPuncher);
            _sockets.Add(publicPuncher);
            _sockets.Add(privatePuncher);
        }
        else
        {
            _sockets[ListenSock] = listeningPuncher;
            _sockets[PublicSock] = publicPuncher;
            _sockets[PrivateSock] = privatePuncher;
        }
    }

    public void StartHolePunchProcess()
    {
        using var _ = DisposeLock.AutoLock(this);
        _processingHolePunching = true;

        if (IsConnected())
            Disconnect();

        _sockets.ForEach(x => x.StartHandshake());
    }

    public bool IsHolepunching()
    {
        using var _ = DisposeLock.AutoLock(this);
        return _processingHolePunching;
    }

    public void Disconnect()
    {
        using var _ = DisposeLock.AutoLock(this);

        // 홀펀칭이 진행중이면 중단하도록 한다.
        StopHolePunchProcess();

        _processingHolePunching = false;

        if (!IsConnected())
            return;

        // 연결중인 대상이 있으면 연결중지해준다.
        _sockets[_connectionType].Disconnect();
    }


    // 이미 연결중인건 건들지 않고 홀펀칭 진행만 더이상 하지 않도록 한다.
    public void StopHolePunchProcess()
    {
        using var _ = DisposeLock.AutoLock(this);
        _sockets.ForEach(x => x.StopHandshake());
        _processingHolePunching = false;
    }

    public bool IsConnected()
    {
        using var _ = DisposeLock.AutoLock(this);

        if (_connectionType == -1)
            return false;

        return _sockets[_connectionType].IsConnected();
    }

   
    private void OnHolePunchProcessSuccess(IHolepuncher holepunchSocket)
    {
        using var _ = DisposeLock.AutoLock(this);

        StopHolePunchProcess();

        _connectionType = holepunchSocket.GetHolepunchType();
        _processingHolePunching = false;
        
        OnHolepunchSuccess?.Invoke(this);
    }

    public string ConnectionTypeToString()
    {
        using var _ = DisposeLock.AutoLock(this);
        if (_connectionType == -1)
            return "Not Connected";

        var typeString = _sockets[_connectionType].ToString();
        return typeString ?? "Not Connected";
    }

    public bool TrySendAsync(PktBase pkt)
    {
        using var _ = DisposeLock.AutoLock(this);

        if (_connectionType == -1)
            return false;

        Debug.Assert(_sockets != null);
        return _sockets[_connectionType].TrySendAsync(pkt);
    }

    protected interface IHolepuncher
    {
        void StartHandshake();
        void StopHandshake();
        bool IsConnected();
        void Disconnect();
        bool TrySendAsync(PktBase pkt);
        int GetHolepunchType();
    }

    protected class ListeningHolepuncher : IHolepuncher
    {
        private readonly TcpPeer _peer;
        private readonly IPktParser _pktParser;
        private readonly Socket _listeningSocket;

        private TcpClientEx? _acceptedClient;
        
        public ListeningHolepuncher(TcpPeer peer, int localPort, IPktParser pktParser)
        {
            _peer = peer;
            _pktParser = pktParser;

            _listeningSocket = SocketEx.CreateTcpSocket();
            _listeningSocket.SetReuseAddress(true);            
            _listeningSocket.BindV4(localPort);
        }

        public void StartHandshake()
        {
            ConsoleEx.DebugLogLine($"[홀펀처: {ToString()}] {_listeningSocket.LocalEndPoint} 리스닝 시작");

            _listeningSocket.Listen();
            _listeningSocket.BeginAccept(AcceptAsyncResult, null);
        }

        public void StopHandshake()
        {
            if (IsConnected())
                return;

            ConsoleEx.DebugLogLine($"[홀펀처: {ToString()}] {_listeningSocket.LocalEndPoint} 리스닝 중단");

            try
            {
                // 리소스 해제해줘야 Listening을 중지할 수 있음 ㄷㄷ;
                _listeningSocket.Dispose();
            }
            catch (SocketException e)
            {
                ConsoleEx.PrintExecption(e);
            }
        }

        public bool IsConnected()
        {

            if (_acceptedClient == null)
                return false;

            return _acceptedClient.IsConnected();
        }


        public void Disconnect()
        {
            if (_acceptedClient == null)
                return;

            _acceptedClient.Disconnect();
        }

        public bool TrySendAsync(PktBase pkt)
        {
            if (_acceptedClient == null)
                return false;

            if (!_acceptedClient.IsConnected())
                return false;

            _acceptedClient.SendAsync(pkt);
            return true;
        }

        public int GetHolepunchType() => ListenSock;

        private void AcceptAsyncResult(IAsyncResult result)
        {
            var success = false;
            try
            {
                Socket acceptedSocket = _listeningSocket.EndAccept(result);
                _acceptedClient = new TcpClientEx(acceptedSocket, _pktParser);
                _acceptedClient.OnDisconnected += (client, safeClosed) => _peer.OnPeerDisconnected?.Invoke(_peer, safeClosed);
                _acceptedClient.OnReceived += (client, pkt, receivedBytes) => ConsoleEx.LogLine($"{ToString()} {client.RemoteEndPoint}로부터 {pkt}[{receivedBytes}] 수신완료", ConsoleColor.Green);
                _acceptedClient.OnSent += (client, pkt, sentBytes) => ConsoleEx.LogLine($"{ToString()} {client.RemoteEndPoint}로 {pkt}[{sentBytes}바이트] 전송완료", ConsoleColor.Yellow);
                _acceptedClient.ReceiveAsync();
                success = true;
            }
            catch (Exception e)
            {
                ConsoleEx.PrintExecption(e, $"{ToString()}");
            }
            finally
            {
                if (success)
                {
                    Debug.Assert(_acceptedClient != null);
                    _peer.OnHolePunchProcessSuccess(this);
                }
                else
                {
                    ConsoleEx.DebugLogLine($"[홀펀처: {ToString()}] 클라이언트 {_peer.Id}의 접속 수락 실패");
                }
            }
        }

        public override string ToString() => $"{"Listening Connection",20}";
    }

    protected class ConnectingHolepuncher : TcpClientEx, IHolepuncher
    {
        public enum Type
        {
            PublicConnection,
            PrivateConnection
        }

        private const int MaxRetryCount = 5; // 재시도 횟수
        private const int RetryDelay = 1500; // Private 연결 소켓이나 Public 연결 소켓이 접속에 실패할 경우 다음 재접속 시도까지 대기하는 시간

        private readonly TcpPeer _peer;
        private readonly IPEndPoint _connectionEndPoint;
        private readonly Type _connectionType;

        private int _retryCount;

        public ConnectingHolepuncher(TcpPeer peer, int localPort, IPEndPoint connectionEndPoint, IPktParser pktParser, Type connectionType) :
            base(localPort, pktParser)
        {
            _peer = peer;
            _connectionType = connectionType;
            _connectionEndPoint = connectionEndPoint;
            _retryCount = MaxRetryCount;

            base.OnConnectedSuccess += OnConnectedSuccessCallback;
            base.OnConnectedFailed += OnConnectedFailedCallback;
            base.OnDisconnected += OnDisconnectedCallback;
            base.OnReceived += OnReceivedCallback;
            base.OnSent += OnSentCallback;
        }



        private void OnSentCallback(TcpClientEx client, PktBase pkt, int sentBytes)
        {
            ConsoleEx.LogLine($"클라이언트 {_peer.Id}({client.RemoteEndPoint})로 {pkt}[{sentBytes}바이트] 전송완료", ConsoleColor.Yellow);
        }

        private void OnReceivedCallback(TcpClientEx client, PktBase pkt, int receivedBytes)
        {
            ConsoleEx.LogLine($"클라이언트 {_peer.Id}({client.RemoteEndPoint})로부터 {pkt}[{receivedBytes}바이트] 수신완료", ConsoleColor.Green);
        }

        private void OnDisconnectedCallback(TcpClientEx client, bool safeclosed)
        {
            _peer.OnPeerDisconnected?.Invoke(_peer, safeclosed);
        }


        public void StartHandshake()
        {
            Interlocked.Decrement(ref _retryCount);
            ConsoleEx.DebugLogLine($"[홀펀처: {ToString()}] 클라이언트 {_peer.Id}({_connectionEndPoint})로 비동기 연결 시작");
            ConnectAsync(_connectionEndPoint);
        }

        public void StopHandshake()
        {
            if (IsConnected())
                return;

            ConsoleEx.DebugLogLine($"[홀펀처: {ToString()}] 클라이언트 {_peer.Id}({_connectionEndPoint})로 비동기 연결시도 중단");

            Interlocked.Exchange(ref _retryCount, 0);
            Disconnect();
        }

        private void OnConnectedSuccessCallback(TcpClientEx client)
        {
            ConsoleEx.DebugLogLine($"[홀펀처: {ToString()}] 클라이언트 {_peer.Id}({_connectionEndPoint})에게 접속하였습니다.");
            _peer.OnHolePunchProcessSuccess(this);
        }

        private async void OnConnectedFailedCallback(TcpClientEx client)
        {
            var retryCount = InterlockedInt.Read(ref _retryCount);
            ConsoleEx.DebugLogLine($"[홀펀처: {ToString()}] 클라이언트 {_peer.Id}({_connectionEndPoint})로 비동기 연결 실패 (남은 재시도 횟수: {retryCount})");

            if (retryCount > 0)
            {
                await Task.Delay(RetryDelay).ContinueWith(continuationAction =>
                {
                    if (InterlockedInt.Read(ref _retryCount) > 0)
                        StartHandshake();
                });
            }
        }

        public override string ToString()
        {
            if (_connectionType == Type.PrivateConnection)
                return $"{"Private Connection",20}";

            return $"{"Public Connection",20}";
        }

        public bool TrySendAsync(PktBase pkt)
        {
            try
            {
                SendAsync(pkt);
                return true;
            }
            catch (Exception e)
            {
                ConsoleEx.PrintExecption(e, $"{ToString()}", true, ConsoleColor.Red);
                return false;
            }
        }

        public int GetHolepunchType()
        {
            if (_connectionType == Type.PrivateConnection)
                return PrivateSock;

            return PublicSock;
        }
    }
}
