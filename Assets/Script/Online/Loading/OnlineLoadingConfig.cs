using UnityEngine;

namespace EchoZone.Online.Loading
{
    /// <summary>온라인 로딩 표시와 단계 제한 시간을 Inspector에서 조절합니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Online/Loading Config")]
    public sealed class OnlineLoadingConfig : ScriptableObject
    {
        /// <summary>짧은 작업의 로딩창 깜빡임을 막는 표시 지연입니다.</summary>
        [Min(0)] public float ShowDelaySeconds = .2f;
        /// <summary>단계 완료 후 현재 표시값에서 새 목표까지 이동하는 시간입니다.</summary>
        [Min(0)] public float StepBlendSeconds = .4f;
        /// <summary>처리 중 표시 아이콘이 초당 회전하는 각도입니다.</summary>
        [Min(0)] public float SpinnerDegreesPerSecond = 240f;
        /// <summary>실제 성공 이후 현재 표시값에서 100%까지 채우는 시간입니다.</summary>
        [Range(.2f, .5f)] public float FinishSeconds = .3f;
        /// <summary>UGS 준비 및 계정 인증 요청의 단계 제한 시간입니다.</summary>
        [Min(1)] public float AuthenticationTimeoutSeconds = 30;
        /// <summary>Cloud 지갑 확인 단계의 제한 시간입니다.</summary>
        [Min(1)] public float WalletTimeoutSeconds = 30;
        /// <summary>세션·Relay·NGO 복합 SDK 요청의 제한 시간입니다.</summary>
        [Min(1)] public float SessionTimeoutSeconds = 60;
        /// <summary>로컬 플레이어 복제와 월드 복구 완료를 기다리는 제한 시간입니다.</summary>
        [Min(1)] public float RestoreTimeoutSeconds = 45;
        /// <summary>호스트 선출과 기존 복구 루프를 기다리는 최대 단계 시간입니다.</summary>
        [Min(1)] public float RecoveryTimeoutSeconds = 90;
        /// <summary>Leave 응답을 기다린 뒤 로컬 복귀를 허용하는 시간입니다.</summary>
        [Min(.1f)] public float LeaveTimeoutSeconds = 5;
        /// <summary>단계 완료에 배정할 가중치입니다. 남은 마지막 10%는 실제 성공 전까지 예약합니다.</summary>
        public float[] StageWeights = { 1, 3, 2, 2 };
        /// <summary>작업과 단계에 맞는 대기 제한을 반환합니다.</summary>
        public float Timeout(LoadingOperationKind kind, int stage)
        {
            if (kind <= LoadingOperationKind.SignUp) return AuthenticationTimeoutSeconds;
            if (kind >= LoadingOperationKind.Reconnect) return stage < 2 ? RecoveryTimeoutSeconds : RestoreTimeoutSeconds;
            return stage == 0 ? WalletTimeoutSeconds : stage == 1 ? SessionTimeoutSeconds : RestoreTimeoutSeconds;
        }
    }
}
