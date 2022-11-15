/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/12/2022 9:08:36 PM
 * * * * * * * * * * * * *
 * 서버에 접속된 클라이언트
 *
 *
 * * * * * * * * * * * * * 
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Shared;

namespace Introducer;

public class TcpSession : TcpClientEx
{
        
    private string _privateEndPoint;

    public long Id { get; private set; }

    public Socket? ClientSocket
    {
        get
        {
            if (!IsConnected())
                return null;

            return _tcpClientSocket;
        }
    }
        
    public IPEndPoint? PublicEndPoint
    {
        get
        {
            if (!IsConnected())
                return null;

            return _tcpClientSocket.RemoteEndPoint as IPEndPoint;
        }
    }

    public IPEndPoint? PrivateEndPoint
    {
        get
        {
            lock (this)
            {
                if (_privateEndPoint == string.Empty)
                    return null;

                return IPEndPoint.Parse(_privateEndPoint);
            }
        }
    }

    public bool IsHolePunching
    {
        get => Interlocked.Read(ref _isHolePunching) == ConstLong.True;
        set => Interlocked.Exchange(ref _isHolePunching, value ? ConstLong.True : ConstLong.False);
    }

    public List<long> ConnectedSessions { get; } 

    public long DisconnectionTime { get; set; }     // 연결끊어진 후 경과된 시간

    private long _isHolePunching;
    private static int s_seqId;


    public TcpSession(TcpClient client, IPktParser parser) : base(client.Client, parser)
    {
        _privateEndPoint = string.Empty;
        _isHolePunching = ConstLong.False;

        ConnectedSessions = new List<long>();
    }

    public void UpdatePrivateEndPoint(string privateEndPoint, long defaultId, List<long> connectedPeers)
    {
        using var _ = DisposeLock.AutoLock(this);

        // 처음 접속하는 경우
        if (defaultId == -1)
            Id = Interlocked.Increment(ref s_seqId);
        else
            Id = defaultId;

        _privateEndPoint = privateEndPoint;

        ConnectedSessions.Clear();
        ConnectedSessions.AddRange(connectedPeers);
    }

    public PktSessionInfo GetSessionInfo() =>
        new()
        {
            Id = Id, 
            PrivateEndPoint = PrivateEndPoint?.ToString(),
            PublicEndPoint = PublicEndPoint?.ToString()
        };

       
}
