using UnityEngine;

namespace EchoZone.Player.View
{
    /// <summary>캐릭터 이동 애니메이션 View가 사용하는 연출 전용 데이터입니다.</summary>
    [CreateAssetMenu(
        fileName = "CharacterAnimationConfig",
        menuName = "EchoZone/Player/Character Animation Config")]
    /// <summary>CharacterAnimationConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
    public sealed class CharacterAnimationConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float movementThreshold = 0.001f;
        [SerializeField, Min(0f)] private float turnDegreesPerSecond = 720f;
        /// <summary>마지막 승인 발사 이후 오른팔 조준 자세를 유지할 시간입니다.</summary>
        [SerializeField, Min(0f)] private float aimHoldSeconds = 0.4f;

        /// <summary>마지막 발사 이후의 조준 자세 유지 시간입니다.</summary>
        public float AimHoldSeconds => aimHoldSeconds;

        /// <summary>CharacterAnimationConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
        public float MovementThreshold => movementThreshold;
        /// <summary>CharacterAnimationConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
        public float TurnDegreesPerSecond => turnDegreesPerSecond;
    }
}
