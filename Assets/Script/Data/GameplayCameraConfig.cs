using UnityEngine;

namespace EchoZone.CameraSystem
{
    /// <summary>
    /// 플레이 카메라를 조정하는 기획 데이터를 보관합니다.
    /// 이 클래스는 수치만 가지며, 카메라를 이동하거나 입력을 읽지 않습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameplayCameraConfig",
        menuName = "EchoZone/Camera/Gameplay Camera Config")]
    /// <summary>GameplayCameraConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
    public sealed class GameplayCameraConfig : ScriptableObject
    {
        /// <summary>플레이어 위치에서 카메라가 바라볼 지점까지의 높이 보정값입니다.</summary>
        [SerializeField] private Vector3 targetOffset = new(0f, 1.2f, 0f);

        /// <summary>덕코프식 대각 구도를 만드는 수평 회전각입니다.</summary>
        [SerializeField, Range(-180f, 180f)] private float yawDegrees = 45f;

        /// <summary>지면을 내려다보는 카메라의 수직 회전각입니다.</summary>
        [SerializeField, Range(10f, 85f)] private float pitchDegrees = 55f;

        /// <summary>플레이 시작 시 목표 지점과 카메라 사이의 거리입니다.</summary>
        [SerializeField, Min(0.1f)] private float initialDistance = 16f;

        /// <summary>마우스 휠로 가장 가까이 당길 수 있는 거리입니다.</summary>
        [SerializeField, Min(0.1f)] private float minimumDistance = 8f;

        /// <summary>마우스 휠로 가장 멀리 밀 수 있는 거리입니다.</summary>
        [SerializeField, Min(0.1f)] private float maximumDistance = 26f;

        /// <summary>마우스 휠 입력 한 단위가 변경하는 카메라 거리입니다.</summary>
        [SerializeField, Min(0f)] private float zoomSensitivity = 2f;

        /// <summary>Input System의 원시 마우스 휠 값을 한 단계 단위로 정규화하는 배율입니다.</summary>
        [SerializeField, Min(0f)] private float wheelInputScale = 0.01f;

        /// <summary>로컬 카메라 줌 Input Action이 읽을 입력 시스템 바인딩 경로입니다.</summary>
        [SerializeField] private string zoomBindingPath = "<Mouse>/scroll/y";

        /// <summary>플레이어를 따라갈 때 위치가 목표값에 수렴하는 속도입니다.</summary>
        [SerializeField, Min(0f)] private float followSharpness = 10f;

        /// <summary>원근 카메라의 시야각입니다.</summary>
        [SerializeField, Range(1f, 179f)] private float fieldOfView = 45f;

        public Vector3 TargetOffset => targetOffset;
        /// <summary>덕코프식 대각 구도를 만드는 수평 회전각입니다.</summary>
        public float YawDegrees => yawDegrees;
        /// <summary>지면을 내려다보는 카메라의 수직 회전각입니다.</summary>
        public float PitchDegrees => pitchDegrees;
        /// <summary>플레이 시작 시 목표 지점과 카메라 사이의 거리입니다.</summary>
        public float InitialDistance => initialDistance;
        /// <summary>마우스 휠로 가장 가까이 당길 수 있는 거리입니다.</summary>
        public float MinimumDistance => minimumDistance;
        /// <summary>마우스 휠로 가장 멀리 밀 수 있는 거리입니다.</summary>
        public float MaximumDistance => maximumDistance;
        /// <summary>마우스 휠 입력 한 단위가 변경하는 카메라 거리입니다.</summary>
        public float ZoomSensitivity => zoomSensitivity;
        /// <summary>Input System의 원시 마우스 휠 값을 한 단계 단위로 정규화하는 배율입니다.</summary>
        public float WheelInputScale => wheelInputScale;
        /// <summary>로컬 카메라 줌 Input Action이 읽을 입력 시스템 바인딩 경로입니다.</summary>
        public string ZoomBindingPath => zoomBindingPath;
        /// <summary>플레이어를 따라갈 때 위치가 목표값에 수렴하는 속도입니다.</summary>
        public float FollowSharpness => followSharpness;
        /// <summary>원근 카메라의 시야각입니다.</summary>
        public float FieldOfView => fieldOfView;
    }
}
