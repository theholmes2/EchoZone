using UnityEngine;

namespace EchoZone.Environment
{
    /// <summary>화면 공간 FOV와 캐릭터 가림막 연출 수치를 보관합니다.</summary>
    [CreateAssetMenu(
        fileName = "SeeThroughTunnelConfig",
        menuName = "EchoZone/View/See Through Tunnel Config")]
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

        public float ViewDistance => viewDistance;
        public float FieldOfViewAngle => fieldOfViewAngle;
        public float MinimumAlpha => minimumAlpha;
        public LayerMask OccluderLayerMask => occluderLayerMask;
        public int MaximumOccluderHits => maximumOccluderHits;
        public float PlayerDepthBias => playerDepthBias;
        public float CharacterScreenRadius => characterScreenRadius;
        public float CharacterEdgeSoftness => characterEdgeSoftness;
        public float FullOcclusionHalfWidth => fullOcclusionHalfWidth;
        public float FullOcclusionHeight => fullOcclusionHeight;
        public float FieldOfViewEdgeSoftness => fieldOfViewEdgeSoftness;
        public float LineOfSightHeight => lineOfSightHeight;
        public int FieldOfViewRayCount => fieldOfViewRayCount;
    }
}
