/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/14/2022 12:25:04 PM
 * * * * * * * * * * * * *
 * 프로그램 설정
 */

using System.Net;

namespace Shared;

public class Config
{
    // true
    // false

    public static object PrintLock = new ();        // 콘솔 출력 색상 깔끔하게 변경시켜주기 위한 락

    public const bool PrintExecptionLog = false;    // 예외 발생시 출력할지
    public const bool PrintDebugLog = true;         // 디버그 로깅
    public const bool PrintNormalLog = true;        // 일반 로그 출력할지 (패킷 송/수신/접속 등)

    // 정도의 로컬 테스트용 주소
    public static readonly IPEndPoint IntroducerEndPoint = IPEndPoint.Parse("112.163.241.175:9999");

    // 정도의 가상머신 주소
    // public static readonly IPEndPoint IntroducerEndPoint = IPEndPoint.Parse("34.126.115.248:9999");
}


