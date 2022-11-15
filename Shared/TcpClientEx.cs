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
using System.Threading;


public delegate void OnDisconnectedHandler(TcpClientEx client, bool safeClosed);
public delegate void OnConnectedSuccessHandler(TcpClientEx client);
public delegate void OnConnectedFailedHandler(TcpClientEx client);

public delegate void OnSentHandler(TcpClientEx client, PktBase pkt);
public delegate void OnReceivedHandler(TcpClientEx client, PktBase pkt);

public class TcpClientEx
{
    protected Socket _tcpClientSocket;
    protected readonly IPktParser _pktParser;
    private readonly byte[] _buffer;
    private int _unProcessedByteCount;
    private const int BufferSize = 8192;
    
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

    public bool ReuseAddress
    {
        get => (int)_tcpClientSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)! != 0;
        set => _tcpClientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value);
    }
        

    public event OnDisconnectedHandler? OnDisconnected;
    public event OnConnectedSuccessHandler? OnConnectedSuccess;
    public event OnConnectedFailedHandler? OnConnectedFailed;
    public event OnSentHandler? OnSent;
    public event OnReceivedHandler? OnReceived;
    

    private long _sendPendingCount;
    private long _receivePendingCount;
    private long _connectPendingCount;


    // 클라에서 TcpParticipant 생성할 때 호출
#pragma warning disable CS8618
    public TcpClientEx(IPktParser pktParser)
#pragma warning restore CS8618
    {
        _pktParser = pktParser;
        _buffer = new byte[BufferSize];
        
        BindNew();
    }

    // 서버가 TcpSession 생성할 때 호출
    // 클라가 TcpPeer에서 ListeningHolepuncher 생성할 때 호출
    public TcpClientEx(Socket clientSocket, IPktParser pktParser)
    {
        _pktParser = pktParser;
        _buffer = new byte[BufferSize];
        _tcpClientSocket = clientSocket;

        ReuseAddress = true;
    }

    // 클라가 TcpPeer에서 ConnectionHolepuncher 생성할 때 호출
    public TcpClientEx(IPEndPoint localEndPoint, IPktParser pktParser)
    {
        _pktParser = pktParser;
        _buffer = new byte[BufferSize];
        _tcpClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        ReuseAddress = true;
        Console.WriteLine(localEndPoint);
        _tcpClientSocket.Bind(localEndPoint);
    }

    private void Initialize()
    {
        _sendPendingCount = 0;
        _receivePendingCount = 0;
        _unProcessedByteCount = 0;
    }


    // https://stackoverflow.com/questions/223063/how-can-i-create-an-httplistener-class-on-a-random-port-in-c
    // 랜덤 포트 얻는 방법
    public static int GetRandomUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void BindNew()
    {
        _tcpClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _tcpClientSocket.Bind(new IPEndPoint(IPAddress.Any, GetRandomUnusedPort()));

        ReuseAddress = true;
    }
    
    public bool TryConnectSynchronously(IPEndPoint endPoint, int timeout = -1)
    {
        try
        {
            Initialize();
            _tcpClientSocket.BeginConnect(endPoint, (s) => _tcpClientSocket?.EndConnect(s), this).AsyncWaitHandle.WaitOne(timeout);
            OnConnectedSuccess?.Invoke(this);
            ReceiveAsync();
            return true;
        }
        catch (Exception e)
        {
            Logger.PrintExecption(e);
            return false;
        }
    }



    public void ConnectAsync(IPEndPoint endPoint)
    {
        try
        {
            Initialize();
            Interlocked.Increment(ref _connectPendingCount);
            _tcpClientSocket.BeginConnect(endPoint, ConnectAsyncCallback, null);
        }
        catch (Exception e)
        {
            Logger.PrintExecption(e);
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
            _tcpClientSocket.Disconnect(false);
           
        }
        catch (Exception e)
        {
            Logger.PrintExecption(e);
        }
        finally
        {
            // 모든 비동기 송/수신 결과가 완료될때까지 기다린다.
            //while (Interlocked.Read(ref _connectPendingCount) > 0)
            //{
            //    Console.WriteLine($"수신 펜딩카운트 {_connectPendingCount} {GetType()}");
            //    Thread.Sleep(1000);
            //}

            //while (Interlocked.Read(ref _sendPendingCount) > 0)
            //{
            //    Console.WriteLine($"송신 펜딩카운트 {_sendPendingCount} {GetType()}");
            //    Thread.Sleep(1000);
            //}

            //while (Interlocked.Read(ref _receivePendingCount) > 0)
            //{
            //    Console.WriteLine($"수신 펜딩카운트 {_receivePendingCount} {GetType()}");
            //    Thread.Sleep(1000);
            //}
        }
    }

    public void SendAsync(PktBase pkt)
    {
        Interlocked.Increment(ref _sendPendingCount);
        byte[] sendData = Pkt.ToByteArray(pkt);
        _tcpClientSocket.BeginSend(sendData, 0, sendData.Length, SocketFlags.None, SendAsyncCallback, pkt);
    }

    public void ReceiveAsync(int offset = 0, int size = BufferSize)
    {
        Interlocked.Increment(ref _receivePendingCount);
        _tcpClientSocket.BeginReceive(_buffer, offset, size, SocketFlags.None, ReceiveAsyncCallback, null);
    }

    public virtual bool IsConnected() => _tcpClientSocket.Connected;

    private void ConnectAsyncCallback(IAsyncResult result)
    {
        try
        {
            _tcpClientSocket.EndConnect(result);
            OnConnectedSuccess?.Invoke(this);
        }
        catch (Exception e)
        {
            Logger.PrintExecption(e, GetType().ToString());
            OnConnectedFailed?.Invoke(this);
        }
        finally
        {
            Interlocked.Decrement(ref _connectPendingCount);
        }
    }


    private void SendAsyncCallback(IAsyncResult result)
    {
        Interlocked.Decrement(ref _sendPendingCount);

        try
        {
            var sendBytes = _tcpClientSocket.EndReceive(result);
            var pkt = result.AsyncState as PktBase;
            Debug.Assert(pkt != null);

            OnSent?.Invoke(this, pkt);

            if (sendBytes != 0)
                return;

            OnDisconnected?.Invoke(this, true);
        }
        catch (Exception e)
        {
            Logger.PrintExecption(e, "", false);
            Disconnect();
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
                var packetLen = BitConverter.ToInt32(_buffer, readOffset) + Pkt.HeaderLength;

                if (_unProcessedByteCount >= packetLen)
                {
                    // 헤더를 제외해서 패킷으로 변환
                    var pkt = Pkt.ToPktBase(_buffer, readOffset + Pkt.HeaderLength, packetLen - Pkt.HeaderLength);
                    OnReceived?.Invoke(this, pkt);
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
            Array.Copy(_buffer, srcOffset, _buffer, dstOffset, _unProcessedByteCount);


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
                socketErrorCode = se.ErrorCode;
            }

            Logger.PrintExecption(e, $"소켓 오류: {socketErrorCode} 타입: {GetType()}");
            OnDisconnected?.Invoke(this, false);
        }
        finally
        {
            Interlocked.Decrement(ref _receivePendingCount);
        }
    }
}
