using UnityEngine;

using EchoZone.Environment;

namespace EchoZone.Environment.View
{
    /// <summary>
    /// 로컬 오너의 FOV별 첫 벽 거리와 화면 위치를
    /// 건물 셰이더에 전달하는 화면 공간 마스크 View입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeeThroughTunnelController : MonoBehaviour
    {
        /// <summary>기획자가 조절하는 시스루 마스크 연출 데이터입니다.</summary>
        [SerializeField] private SeeThroughTunnelConfig config;

        private static readonly int MinAlphaID = Shader.PropertyToID("_MinAlpha");
        /// <summary>CameraPositionID 값을 저장합니다.</summary>
        private static readonly int CameraPositionID = Shader.PropertyToID("_SeeThroughCameraPosition");
        /// <summary>PlayerPositionID 값을 저장합니다.</summary>
        private static readonly int PlayerPositionID = Shader.PropertyToID("_SeeThroughPlayerPosition");
        /// <summary>PlayerDepthBiasID 값을 저장합니다.</summary>
        private static readonly int PlayerDepthBiasID = Shader.PropertyToID("_SeeThroughPlayerDepthBias");
        /// <summary>PlayerViewportPositionID 값을 저장합니다.</summary>
        private static readonly int PlayerViewportPositionID = Shader.PropertyToID("_SeeThroughPlayerViewportPosition");
        /// <summary>PlayerScreenRadiusID 값을 저장합니다.</summary>
        private static readonly int PlayerScreenRadiusID = Shader.PropertyToID("_SeeThroughPlayerScreenRadius");
        /// <summary>PlayerEdgeSoftnessID 값을 저장합니다.</summary>
        private static readonly int PlayerEdgeSoftnessID = Shader.PropertyToID("_SeeThroughPlayerEdgeSoftness");
        /// <summary>PlayerForwardID 값을 저장합니다.</summary>
        private static readonly int PlayerForwardID = Shader.PropertyToID("_SeeThroughPlayerForward");
        /// <summary>PlayerRightID 값을 저장합니다.</summary>
        private static readonly int PlayerRightID = Shader.PropertyToID("_SeeThroughPlayerRight");
        /// <summary>FieldOfViewAngleID 값을 저장합니다.</summary>
        private static readonly int FieldOfViewAngleID = Shader.PropertyToID("_SeeThroughFieldOfViewAngle");
        /// <summary>FieldOfViewSoftnessID 값을 저장합니다.</summary>
        private static readonly int FieldOfViewSoftnessID = Shader.PropertyToID("_SeeThroughFieldOfViewSoftness");
        /// <summary>LosDistancesID 값을 저장합니다.</summary>
        private static readonly int LosDistancesID = Shader.PropertyToID("_SeeThroughLosDistances");
        /// <summary>LosRayCountID 값을 저장합니다.</summary>
        private static readonly int LosRayCountID = Shader.PropertyToID("_SeeThroughLosRayCount");
        /// <summary>WholeFadeID 값을 저장합니다.</summary>
        private static readonly int WholeFadeID = Shader.PropertyToID("_SeeThroughWholeFade");

        /// <summary>현재 로컬 플레이어를 렌더링하는 플레이 카메라 캐시입니다.</summary>
        private Camera gameplayCamera;
        /// <summary>occluderHits 값을 저장합니다.</summary>
        private RaycastHit[] occluderHits;
        /// <summary>visibilityBrick 값을 저장합니다.</summary>
        private readonly SeeThroughVisibilityBrick visibilityBrick = new();
        /// <summary>losDistances 값을 저장합니다.</summary>
        private float[] losDistances;
        /// <summary>measuredLosDistances 값을 저장합니다.</summary>
        private float[] measuredLosDistances;
        /// <summary>propertyBlock 값을 저장합니다.</summary>
        private MaterialPropertyBlock propertyBlock;
        /// <summary>wholeFadeRenderer 값을 저장합니다.</summary>
        private Renderer wholeFadeRenderer;

        /// <summary>Awake 작업을 수행합니다.</summary>
        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Glue(접착제) 스크립트가 매 프레임 로컬 오너 캐릭터의 트랜스폼을 주입해 주는 함수입니다.
        /// </summary>
        /// <param name="playerTransform">로컬 오너 플레이어의 캐릭터 모델링 트랜스폼</param>
        public void UpdateShaderVariables(Transform playerTransform)
        {
            if (playerTransform == null || config == null) return;

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }

            if (gameplayCamera == null) return;

            Vector3 playerPosition = playerTransform.position;
            Vector3 cameraPosition = gameplayCamera.transform.position;
            Vector3 cameraToPlayer = playerPosition - cameraPosition;
            if (cameraToPlayer.sqrMagnitude <= 0.0001f) return;

            EnsureHitBuffer();
            EnsureLosBuffer();
            UpdateLosDistances(playerPosition, playerTransform.forward);
            UpdateWholeBuildingOccluder(playerPosition, cameraPosition);

            Vector3 playerForward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized;
            Vector3 playerRight = Vector3.Cross(Vector3.up, playerForward).normalized;
            Vector3 playerViewport = gameplayCamera.WorldToViewportPoint(playerPosition);
            Shader.SetGlobalFloat(MinAlphaID, config.MinimumAlpha);
            Shader.SetGlobalVector(CameraPositionID, cameraPosition);
            Shader.SetGlobalVector(PlayerPositionID, playerPosition);
            Shader.SetGlobalFloat(PlayerDepthBiasID, config.PlayerDepthBias);
            Shader.SetGlobalVector(PlayerViewportPositionID, playerViewport);
            Shader.SetGlobalFloat(PlayerScreenRadiusID, config.CharacterScreenRadius);
            Shader.SetGlobalFloat(PlayerEdgeSoftnessID, config.CharacterEdgeSoftness);
            Shader.SetGlobalVector(PlayerForwardID, playerForward);
            Shader.SetGlobalVector(PlayerRightID, playerRight);
            Shader.SetGlobalFloat(FieldOfViewAngleID, config.FieldOfViewAngle * Mathf.Deg2Rad * 0.5f);
            Shader.SetGlobalFloat(FieldOfViewSoftnessID, config.FieldOfViewEdgeSoftness);
            Shader.SetGlobalFloatArray(LosDistancesID, losDistances);
            Shader.SetGlobalInt(LosRayCountID, config.FieldOfViewRayCount);
        }

        /// <summary>각 FOV 방향에서 첫 번째 건물 벽까지의 연속 가시 거리를 계산합니다.</summary>
        private void UpdateLosDistances(Vector3 playerPosition, Vector3 playerForward)
        {
            Vector3 sightOrigin = playerPosition + Vector3.up * config.LineOfSightHeight;
            for (int rayIndex = 0; rayIndex < config.FieldOfViewRayCount; rayIndex++)
            {
                Vector3 direction = visibilityBrick.CalculateSampleDirection(
                    playerForward,
                    config.FieldOfViewAngle,
                    rayIndex,
                    config.FieldOfViewRayCount);
                measuredLosDistances[rayIndex] = FindFirstBuildingDistance(
                    sightOrigin,
                    direction,
                    config.ViewDistance);

            }

            // 시야거리는 현재 프레임 값으로 즉시 고정합니다. 인접 Ray 중 가장 가까운
            // 벽을 사용해 벽 끝에서 발생하는 계단식 확장/축소만 공간적으로 억제합니다.
            for (int rayIndex = 0; rayIndex < config.FieldOfViewRayCount; rayIndex++)
            {
                int previous = Mathf.Max(0, rayIndex - 1);
                int next = Mathf.Min(config.FieldOfViewRayCount - 1, rayIndex + 1);
                losDistances[rayIndex] = Mathf.Min(
                    measuredLosDistances[previous],
                    measuredLosDistances[rayIndex],
                    measuredLosDistances[next]);
            }

            for (int index = config.FieldOfViewRayCount; index < losDistances.Length; index++)
            {
                losDistances[index] = 0f;
            }
        }

        /// <summary>FindFirstBuildingDistance 작업을 수행합니다.</summary>
        private float FindFirstBuildingDistance(Vector3 origin, Vector3 direction, float maximumDistance)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                occluderHits,
                maximumDistance,
                config.OccluderLayerMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = maximumDistance;
            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = occluderHits[index].collider;
                if (collider == null) continue;

                Renderer renderer = collider.GetComponent<Renderer>() ??
                                    collider.GetComponentInChildren<Renderer>();
                if (!UsesSeeThroughShader(renderer)) continue;
                nearestDistance = Mathf.Min(nearestDistance, occluderHits[index].distance);
            }

            return Mathf.Max(0f, nearestDistance - config.PlayerDepthBias);
        }

        /// <summary>UsesSeeThroughShader 작업을 수행합니다.</summary>
        private static bool UsesSeeThroughShader(Renderer renderer)
        {
            if (renderer == null) return false;

            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material != null && material.shader != null &&
                    material.shader.name == "EchoZone/Building See Through")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 캐릭터 실루엣의 네 모서리가 모두 같은 건물에 가려졌을 때만
        /// 그 Renderer 전체를 반투명하게 만듭니다.
        /// </summary>
        private void UpdateWholeBuildingOccluder(Vector3 playerPosition, Vector3 cameraPosition)
        {
            Vector3 cameraRight = Vector3.ProjectOnPlane(gameplayCamera.transform.right, Vector3.up);
            if (cameraRight.sqrMagnitude <= 0.0001f)
            {
                SetWholeBuildingOccluder(null);
                return;
            }
            cameraRight.Normalize();

            float halfWidth = config.FullOcclusionHalfWidth;
            float lowerHeight = Mathf.Min(0.15f, config.FullOcclusionHeight);
            Vector3 lowerCenter = playerPosition + Vector3.up * lowerHeight;
            Vector3 upperCenter = playerPosition + Vector3.up * config.FullOcclusionHeight;

            Renderer lowerLeft = FindBuildingBetween(lowerCenter - cameraRight * halfWidth, cameraPosition);
            if (lowerLeft == null)
            {
                SetWholeBuildingOccluder(null);
                return;
            }

            Renderer lowerRight = FindBuildingBetween(lowerCenter + cameraRight * halfWidth, cameraPosition);
            Renderer upperLeft = FindBuildingBetween(upperCenter - cameraRight * halfWidth, cameraPosition);
            Renderer upperRight = FindBuildingBetween(upperCenter + cameraRight * halfWidth, cameraPosition);

            Renderer candidate = lowerLeft == lowerRight && lowerLeft == upperLeft && lowerLeft == upperRight
                ? lowerLeft
                : null;
            SetWholeBuildingOccluder(candidate);
        }

        /// <summary>SetWholeBuildingOccluder 작업을 수행합니다.</summary>
        private void SetWholeBuildingOccluder(Renderer candidate)
        {
            if (wholeFadeRenderer == candidate) return;

            ClearWholeBuildingOccluder();
            if (candidate == null) return;

            wholeFadeRenderer = candidate;
            SetWholeFade(wholeFadeRenderer, 1f);
        }

        /// <summary>FindBuildingBetween 작업을 수행합니다.</summary>
        private Renderer FindBuildingBetween(Vector3 playerSample, Vector3 cameraPosition)
        {
            Vector3 sampleToCamera = cameraPosition - playerSample;
            float distance = sampleToCamera.magnitude;
            float maximumDistance = distance - config.PlayerDepthBias;
            if (maximumDistance <= 0f) return null;

            int hitCount = Physics.RaycastNonAlloc(
                playerSample,
                sampleToCamera / distance,
                occluderHits,
                maximumDistance,
                config.OccluderLayerMask,
                QueryTriggerInteraction.Ignore);

            Renderer nearestRenderer = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = occluderHits[index].collider;
                if (collider == null || occluderHits[index].distance >= nearestDistance) continue;

                Renderer renderer = collider.GetComponent<Renderer>() ??
                                    collider.GetComponentInChildren<Renderer>();
                if (!UsesSeeThroughShader(renderer)) continue;

                nearestDistance = occluderHits[index].distance;
                nearestRenderer = renderer;
            }

            return nearestRenderer;
        }

        /// <summary>SetWholeFade 작업을 수행합니다.</summary>
        private void SetWholeFade(Renderer targetRenderer, float value)
        {
            if (targetRenderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(WholeFadeID, value);
            targetRenderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        /// <summary>ClearWholeBuildingOccluder 작업을 수행합니다.</summary>
        private void ClearWholeBuildingOccluder()
        {
            if (wholeFadeRenderer == null) return;

            if (propertyBlock != null)
            {
                SetWholeFade(wholeFadeRenderer, 0f);
            }

            wholeFadeRenderer = null;
        }

        /// <summary>EnsureLosBuffer 작업을 수행합니다.</summary>
        private void EnsureLosBuffer()
        {
            const int shaderCapacity = 64;
            if (losDistances == null || losDistances.Length != shaderCapacity)
            {
                losDistances = new float[shaderCapacity];
                measuredLosDistances = new float[shaderCapacity];
            }
            else if (measuredLosDistances == null || measuredLosDistances.Length != shaderCapacity)
            {
                measuredLosDistances = new float[shaderCapacity];
            }
        }

        /// <summary>EnsureHitBuffer 작업을 수행합니다.</summary>
        private void EnsureHitBuffer()
        {
            int capacity = config.MaximumOccluderHits;
            if (occluderHits == null || occluderHits.Length != capacity)
            {
                occluderHits = new RaycastHit[capacity];
            }
        }

        /// <summary>로컬 오너가 사라진 뒤 전역 셰이더에 이전 플레이어 값이 남지 않게 초기화합니다.</summary>
        public void ClearShaderVariables()
        {
            ClearWholeBuildingOccluder();
            Shader.SetGlobalFloat(MinAlphaID, 1f);
            Shader.SetGlobalInt(LosRayCountID, 0);
            gameplayCamera = null;
        }
    }
}
