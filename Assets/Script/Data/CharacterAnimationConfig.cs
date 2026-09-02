using UnityEngine;

namespace EchoZone.Player.View
{
    /// <summary>캐릭터 이동 애니메이션 View가 사용하는 연출 전용 데이터입니다.</summary>
    [CreateAssetMenu(
        fileName = "CharacterAnimationConfig",
        menuName = "EchoZone/Player/Character Animation Config")]
    public sealed class CharacterAnimationConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float movementThreshold = 0.001f;
        [SerializeField, Min(0f)] private float turnDegreesPerSecond = 720f;

        public float MovementThreshold => movementThreshold;
        public float TurnDegreesPerSecond => turnDegreesPerSecond;
    }
}
