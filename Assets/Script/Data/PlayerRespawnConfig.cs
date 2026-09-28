using UnityEngine;

namespace EchoZone.Player.Network
{
    /// <summary>플레이어 사망 후 자동 부활의 대기시간과 목적지를 보관합니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Player/Respawn Config")]
    public sealed class PlayerRespawnConfig : ScriptableObject
    {
        /// <summary>사망 애니메이션을 보여준 뒤 부활할 때까지의 서버 시간입니다.</summary>
        [SerializeField, Min(0.4f)] private float delaySeconds = 3f;
        /// <summary>현재 테스트 맵에서 사용할 부활 위치입니다.</summary>
        [SerializeField] private Vector3 position = new(0f, 2f, 0f);
        /// <summary>부활 시 본체 회전 각도입니다.</summary>
        [SerializeField] private Vector3 eulerAngles;
        /// <summary>사망 연출 이후 부활 대기시간입니다.</summary>
        public float DelaySeconds => Mathf.Max(0.4f, delaySeconds);
        /// <summary>서버가 이동시킬 목적지입니다.</summary>
        public Vector3 Position => position;
        /// <summary>서버가 적용할 본체 회전입니다.</summary>
        public Quaternion Rotation => Quaternion.Euler(eulerAngles);
    }
}
