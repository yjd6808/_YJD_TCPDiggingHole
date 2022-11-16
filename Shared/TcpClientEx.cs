/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/13/2022 9:51:17 AM
 * * * * * * * * * * * * *
 * [상속목록]
 * TcpPeer              - 클라이언트와 P2P 연결된 클라이언트
 * TcpParticipant       - 클라이언트
 * TcpSession           - 서버에 연결된 클라이언트
 *
 * 비동기 소켓 통신 구현
 */

using System.Diagnostics;

namespace Shared;

using System;
using System.Net.Sockets;
using System.Net;

public delegate void OnDisconnectedHandler(TcpClientEx client, bool safeClosed);
public delegate void OnConnectedSuccessHandler(TcpClientEx client);
public delegate void OnConnectedFailedHandler(TcpClientEx client);

public delegate void OnSentHandler(TcpClientEx client, PktBase pkt, int sentBytes);
public delegate void OnReceivedHandler(TcpClientEx client, PktBase pkt, int receivedBytes);

public class TcpClientEx
{
    protected Socket _tcpClientSocket;              // 클라이언트 소켓
    protected readonly IPktParser _pktParser;       // 패킷 들어오면 맞는 콜백함수 실행해줌

    private readonly byte[] _recvBuffer;            // 수신버퍼
    private const int BufferSize = 8192;            // 수신버퍼 크기

    private int _unProcessedByteCount;              // 아직 처리안된 바이트 수

    public IPEndPoint? LocalEndPoint => _tcpClientSocket.LocalEndPoint as IPEndPoint; 

    public IPEndPoint? RemoteEndPoint
    {
        get
        {
            if (!_tcpClientSocket.Connected)
                return null;

            return _tcpClientSocket.RemoteEndPoint as IPEndPoint;
        }
    }

    // 로컬 포트 중복 바인딩을 할 수 있도록 하기 위함
    public bool ReuseAddress
    {
        get => _tcpClientSocket.GetReuseAddress();
        set => _tcpClientSocket.SetReuseAddress(value);
    }

    // 구멍 유지를 위함
    public bool KeepAlive
    {
        get => _tcpClientSocket.GetkeepAlive();
        set => _tcpClientSocket.SetkeepAlive(value);
    }
        

    public event OnDisconnectedHandler? OnDisconnected;
    public event OnConnectedSuccessHandler? OnConnectedSuccess;
    public event OnConnectedFailedHandler? OnConnectedFailed;
    public event OnSentHandler? OnSent;
    public event OnReceivedHandler? OnReceived;
    

    // 클라에서 TcpParticipant 생성할 때 호출
    public TcpClientEx(IPktParser pktParser)
    {
        _pktParser = pktParser;
        _recvBuffer = new byte[BufferSize];
        _tcpClientSocket = SocketEx.CreateTcpSocket();

        KeepAlive = true;
        ReuseAddress = true;
    }

    // 서버가 TcpSession 생성할 때 호출
    // 클라가 TcpPeer에서 ListeningHolepuncher 생성할 때 호출
    public TcpClientEx(Socket clientSocket, IPktParser pktParser)
    {
        _pktParser = pktParser;
        _recvBuffer = new byte[BufferSize];
        _tcpClientSocket = clientSocket;

        KeepAlive = true;
        ReuseAddress = true;
    }

    // 클라가 TcpPeer에서 ConnectionHolepuncher 생성할 때 호출
    public TcpClientEx(int localPort, IPktParser pktParser)
    {
        _pktParser = pktParser;
        _recvBuffer = new byte[BufferSize];
        _tcpClientSocket = SocketEx.CreateTcpSocket();

        KeepAlive = true;
        ReuseAddress = true;

        _tcpClientSocket.Bind(new IPEndPoint(IPAddress.Any, localPort));
    }

    
    public bool TryConnectSynchronously(IPEndPoint endPoint, int timeout = -1)
    {
        try
        {
            _tcpClientSocket.BeginConnect(endPoint, (s) => _tcpClientSocket?.EndConnect(s), this).AsyncWaitHandle.WaitOne(timeout);
            OnConnectedSuccess?.Invoke(this);
            ReceiveAsync();
            return true;
        }
        catch (Exception e)
        {
            ConsoleEx.PrintExecption(e);
            return false;
        }
    }


    public void ConnectAsync(IPEndPoint endPoint)
    {
        try
        {
            _tcpClientSocket.BeginConnect(endPoint, ConnectAsyncCallback, null);
        }
        catch (Exception e)
        {
            ConsoleEx.PrintExecption(e);
        }
    }

    public virtual void Disconnect()
    {
        try
        {
            // https://stackoverflow.com/questions/35229143/what-exactly-do-sockets-shutdown-disconnect-close-and-dispose-do
            // Shutdown, Close, Disconnect 차이
            // Shutdown: 단순히 보내기, 받기 기능 차단 (패킷을 수신하더라도 Receive 함수가 반응 안하는듯)
            // Disconnect(true|false): Shutdown(Both) 포함, 보낼 패킷이 있으면 모두 보냄(상대방 마지막에 0바이트 수신), false 시에는 시스템 리소스 반환
            // Close(타임아웃): 데이터를 보내는 중간에 중단될 수 있음, 타임아웃 지정가능
            // Dispose: 타임아웃을 전달하지 않는 Close와 같음 - 시스템 리소스반환
            _tcpClientSocket.Shutdown(SocketShutdown.Both);
            _tcpClientSocket.Disconnect(true);
        }
        catch (Exception e)
        {
            ConsoleEx.PrintExecption(e);
        }
    }

    public void SendAsync(PktBase pkt)
    {
        byte[] sendData = Pkt.ToByteArray(pkt);
        _tcpClientSocket.BeginSend(sendData, 0, sendData.Length, SocketFlags.None, SendAsyncCallback, pkt);
    }

    public void ReceiveAsync(int offset = 0, int size = BufferSize)
    {
        _tcpClientSocket.BeginReceive(_recvBuffer, offset, size, SocketFlags.None, ReceiveAsyncCallback, null);
    }

    public virtual bool IsConnected() => _tcpClientSocket.Connected;

    private void ConnectAsyncCallback(IAsyncResult result)
    {
        try
        {
            _tcpClientSocket.EndConnect(result);
            OnConnectedSuccess?.Invoke(this);
            ReceiveAsync();
        }
        catch (Exception e)
        {
            ConsoleEx.PrintExecption(e, GetType().ToString());
            OnConnectedFailed?.Invoke(this);
        }
    }


    private void SendAsyncCallback(IAsyncResult result)
    {
        try
        {
            var sentBytes = _tcpClientSocket.EndReceive(result);
            var pkt = result.AsyncState as PktBase;
            Debug.Assert(pkt != null);

            OnSent?.Invoke(this, pkt, sentBytes);

            if (sentBytes != 0)
                return;

            OnDisconnected?.Invoke(this, true);
        }
        catch (Exception e)
        {
            ConsoleEx.PrintExecption(e, "", false);
            OnDisconnected?.Invoke(this, false);
        }
    }


    private void ReceiveAsyncCallback(IAsyncResult result)
    {
        try
        {
            var readOffset = 0;
            var recvBytes = _tcpClientSocket.EndReceive(result);
            if (recvBytes == 0)
            {
                OnDisconnected?.Invoke(this, true);
                return;
            }

            _unProcessedByteCount += recvBytes;

            // 여기서 _buffer의 상태
            // [■■■■[■■■■][■■■■■■■][■■■■■■■■][■■■
            // <-- _unProcessedByteCount -->
            // 4개의 패킷이 모두 처리되고 나머지 일부 수신한 패킷은 처리하지 않고
            // 다음번 ReceiveData 함수 호출시 패킷형성이 완료되면 처리한다.

            while (_unProcessedByteCount >= Pkt.HeaderLength)
            {
                // 헤더(패킷의 크기)를 포함한 패킷 크기
                var packetLen = BitConverter.ToInt32(_recvBuffer, readOffset) + Pkt.HeaderLength;

                if (_unProcessedByteCount >= packetLen)
                {
                    // 헤더를 제외해서 패킷으로 변환
                    var pkt = Pkt.ToPktBase(_recvBuffer, readOffset + Pkt.HeaderLength, packetLen - Pkt.HeaderLength);
                    OnReceived?.Invoke(this, pkt, recvBytes);
                    _pktParser.TryParse(this, pkt);

                    _unProcessedByteCount -= packetLen;
                    readOffset += packetLen;
                }
            }

            int srcOffset = readOffset;
            int dstOffset = 0;

            // 여기서 _buffer의 상태
            //                               [■■■
            // ↑                             ↑
            // dstOffset(0)                  srcOffset(readOffset)

            //                               <---> 
            //                               _unProcessedByteCount
            // 앞으로 당겨줘야한다.
            Array.Copy(_recvBuffer, srcOffset, _recvBuffer, dstOffset, _unProcessedByteCount);


            // 여기서 _buffer의 상태
            // [■■■
            // <---> 
            // _unProcessedByteCount
            // 이후 받아야할 데이터가 _buffer에 저장되어야할 위치(offset)는 _unProcessedByteCount가 된다.

            ReceiveAsync(_unProcessedByteCount, BufferSize - _unProcessedByteCount);

        }
        catch (Exception e)
        {
            int socketErrorCode = -1;

            if (e.GetType() == typeof(SocketException))
            {
                SocketException? se = e as SocketException;
                if (se != null) socketErrorCode = se.ErrorCode;
            }

            ConsoleEx.PrintExecption(e, $"소켓 오류: {socketErrorCode} 타입: {GetType()}");
            Disconnect();
            OnDisconnected?.Invoke(this, false);
        }
    }
}
