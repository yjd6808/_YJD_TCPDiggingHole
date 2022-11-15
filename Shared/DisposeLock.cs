/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/14/2022 3:57:09 PM
 * * * * * * * * * * * * *
 * lock 뛰어쓰기 거슬려서 없애기 위해서 내가 생각해낸 방법
 * using var _ = DisposeLock.AutoLock(this);
 */

using System.Diagnostics;

namespace Shared;

public class DisposeLock : IDisposable
{
    private object? _lockObject;
    private DisposeLock() {}

    public static DisposeLock AutoLock(object lockObject)
    {
        var disposeLock = new DisposeLock();
        disposeLock._lockObject = lockObject;
        Monitor.Enter(lockObject);
        return disposeLock;
    }

    public void Dispose()
    {
        Debug.Assert(_lockObject != null);
        Monitor.Exit(_lockObject);
    }
}
