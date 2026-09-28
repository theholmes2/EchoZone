using UnityEngine;

namespace EchoZone.Player.Movement
{
    /// <summary>
    /// 플레이어 이동 시뮬레이션에 사용하는 조정 가능한 데이터를 보관합니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerMovementConfig",
        menuName = "EchoZone/Player/Movement Config")]
    /// <summary>PlayerMovementConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        /// <summary>플레이어의 초당 이동 거리입니다.</summary>
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        /// <summary>플레이어의 초당 이동 거리를 제공합니다.</summary>
        public float MoveSpeed => moveSpeed;
    }
}
