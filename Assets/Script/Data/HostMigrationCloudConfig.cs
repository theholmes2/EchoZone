using UnityEngine;

/// <summary>Host Migration Snapshot을 Cloud Code에 저장·조회할 때 사용하는 접속 규약 데이터입니다.</summary>
[CreateAssetMenu(
    fileName = "HostMigrationCloudConfig",
    menuName = "EchoZone/Network/Host Migration Cloud Config")]
/// <summary>HostMigrationCloudConfig 관련 기능과 데이터를 제공하는 형식입니다.</summary>
public sealed class HostMigrationCloudConfig : ScriptableObject
{
    [SerializeField] private string moduleName = "HostMigrationModule";
    [SerializeField] private string saveFunctionName = "SaveCheckpoint";
    [SerializeField] private string loadFunctionName = "LoadLatestCheckpoint";
    /// <summary>중첩 JSON 응답 여유를 남기는 프로젝트 자체 UTF-8 스냅샷 크기 한도입니다.</summary>
    [SerializeField, Min(1024)] private int maximumSnapshotBytes = 524288;
    /// <summary>업로드 전 검사할 원문 크기 한도입니다. 서비스의 더 작은 한도도 별도로 적용됩니다.</summary>
    public int MaximumSnapshotBytes => Mathf.Max(1024, maximumSnapshotBytes);

    /// <summary>배포된 Cloud Code C# Module의 이름입니다.</summary>
    public string ModuleName => moduleName;

    /// <summary>Snapshot 저장을 담당하는 Cloud Code 함수 이름입니다.</summary>
    public string SaveFunctionName => saveFunctionName;

    /// <summary>최신 Snapshot 조회를 담당하는 Cloud Code 함수 이름입니다.</summary>
    public string LoadFunctionName => loadFunctionName;
}
