using System.Diagnostics;
using UnityEngine;

namespace EchoZone.Online
{
    /// <summary>지난 온라인 기능 검증용 상세 로그를 개발 심볼로 선택 출력합니다. 경고·오류에는 사용하지 않습니다.</summary>
    public static class OnlineDebugLog
    {
        /// <summary>ECHOZONE_VERBOSE_LOGS 심볼이 있을 때만 성공·진행 로그와 문자열 계산을 포함합니다.</summary>
        [Conditional("ECHOZONE_VERBOSE_LOGS")]
        public static void Info(object message, Object context = null) => UnityEngine.Debug.Log(message, context);
    }
}
