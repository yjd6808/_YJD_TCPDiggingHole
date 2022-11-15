/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/13/2022 5:42:51 PM
 * * * * * * * * * * * * *
 * int 형은 Interloced.Read가 없네 
 */

namespace Shared;


public static class InterlockedInt
{
    public static int Read(ref int value) => Interlocked.CompareExchange(ref value, 0, 0);
}
