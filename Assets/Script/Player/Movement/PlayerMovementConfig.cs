using UnityEngine;

namespace EchoZone.Player.Movement
{
    [CreateAssetMenu(
        fileName = "PlayerMovementConfig",
        menuName = "EchoZone/Player/Movement Config")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        public float MoveSpeed => moveSpeed;
    }
}
