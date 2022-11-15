/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/12/2022 10:37:54 PM
 * * * * * * * * * * * * *
 * 패킷 파서 최상위
 */

using System.Diagnostics;

namespace Shared;

public interface IPktParser
{
    bool TryParse(TcpClientEx client, PktBase pkt);
}

public abstract class PktParser<T> : IPktParser where T : TcpClientEx
{
    protected Dictionary<int, Action<PktBase, T>> _ptkHandlers;

    protected PktParser()
    {
        _ptkHandlers = new Dictionary<int, Action<PktBase, T>>();
    }

    public abstract void Initialize();

    public bool TryParse(TcpClientEx client, PktBase pkt)
    {
        if (_ptkHandlers.ContainsKey(pkt.Type))
        {
            T? castedClient = client as T;
            Debug.Assert(castedClient != null);
            _ptkHandlers[pkt.Type](pkt, castedClient);
            return true;
        }

        return false;
    }
}
