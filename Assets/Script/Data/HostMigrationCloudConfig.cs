using UnityEngine;

/// <summary>Host Migration Snapshot을 Cloud Code에 저장·조회할 때 사용하는 접속 규약 데이터입니다.</summary>
[CreateAssetMenu(
    fileName = "HostMigrationCloudConfig",
    menuName = "EchoZone/Network/Host Migration Cloud Config")]
public sealed class HostMigrationCloudConfig : ScriptableObject
{
    [SerializeField] private string moduleName = "HostMigrationModule";
    [SerializeField] private string saveFunctionName = "SaveCheckpoint";
    [SerializeField] private string loadFunctionName = "LoadLatestCheckpoint";

    /// <summary>배포된 Cloud Code C# Module의 이름입니다.</summary>
    public string ModuleName => moduleName;

    /// <summary>Snapshot 저장을 담당하는 Cloud Code 함수 이름입니다.</summary>
    public string SaveFunctionName => saveFunctionName;

    /// <summary>최신 Snapshot 조회를 담당하는 Cloud Code 함수 이름입니다.</summary>
    public string LoadFunctionName => loadFunctionName;
}
