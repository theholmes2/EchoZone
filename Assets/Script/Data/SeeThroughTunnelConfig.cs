using UnityEngine;

namespace EchoZone.Environment
{
    /// <summary>화면 공간 FOV와 캐릭터 가림막 연출 수치를 보관합니다.</summary>
    [CreateAssetMenu(
        fileName = "SeeThroughTunnelConfig",
        menuName = "EchoZone/View/See Through Tunnel Config")]
    /// <summary>SeeThroughTunnelConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
    public sealed class SeeThroughTunnelConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float viewDistance = 27f;
        [SerializeField, Range(1f, 179f)] private float fieldOfViewAngle = 120f;
        [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.15f;
        [SerializeField] private LayerMask occluderLayerMask = ~0;
        [SerializeField, Min(1)] private int maximumOccluderHits = 32;
        [SerializeField, Min(0f)] private float playerDepthBias = 0.1f;
        [SerializeField, Range(0.01f, 0.5f)] private float characterScreenRadius = 0.2f;
        [SerializeField, Range(0.001f, 0.25f)] private float characterEdgeSoftness = 0.025f;
        [SerializeField, Min(0f)] private float fullOcclusionHalfWidth = 0.45f;
        [SerializeField, Min(0f)] private float fullOcclusionHeight = 1.5f;
        [SerializeField, Min(0f)] private float fieldOfViewEdgeSoftness = 0.75f;
        [SerializeField, Min(0f)] private float lineOfSightHeight = 0.5f;
        [SerializeField, Range(3, 64)] private int fieldOfViewRayCount = 64;

        /// <summary>SeeThroughTunnelConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
        public float ViewDistance => viewDistance;
        /// <summary>SeeThroughTunnelConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
        public float FieldOfViewAngle => fieldOfViewAngle;
        /// <summary>SeeThroughTunnelConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
        public float MinimumAlpha => minimumAlpha;
        /// <summary>OccluderLayerMask 값을 제공합니다.</summary>
        public LayerMask OccluderLayerMask => occluderLayerMask;
        /// <summary>MaximumOccluderHits 값을 제공합니다.</summary>
        public int MaximumOccluderHits => maximumOccluderHits;
        /// <summary>PlayerDepthBias 값을 제공합니다.</summary>
        public float PlayerDepthBias => playerDepthBias;
        /// <summary>CharacterScreenRadius 값을 제공합니다.</summary>
        public float CharacterScreenRadius => characterScreenRadius;
        /// <summary>CharacterEdgeSoftness 값을 제공합니다.</summary>
        public float CharacterEdgeSoftness => characterEdgeSoftness;
        /// <summary>FullOcclusionHalfWidth 값을 제공합니다.</summary>
        public float FullOcclusionHalfWidth => fullOcclusionHalfWidth;
        /// <summary>FullOcclusionHeight 값을 제공합니다.</summary>
        public float FullOcclusionHeight => fullOcclusionHeight;
        /// <summary>FieldOfViewEdgeSoftness 값을 제공합니다.</summary>
        public float FieldOfViewEdgeSoftness => fieldOfViewEdgeSoftness;
        /// <summary>LineOfSightHeight 값을 제공합니다.</summary>
        public float LineOfSightHeight => lineOfSightHeight;
        /// <summary>FieldOfViewRayCount 값을 제공합니다.</summary>
        public int FieldOfViewRayCount => fieldOfViewRayCount;
    }
}
