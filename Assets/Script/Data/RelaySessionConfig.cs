using UnityEngine;

/// <summary>
/// Relay Session 생성에 필요한 변경 가능한 설정을 보관하는 데이터입니다.
/// </summary>
[CreateAssetMenu(
    fileName = "RelaySessionConfig",
    menuName = "EchoZone/Network/Relay Session Config")]
public sealed class RelaySessionConfig : ScriptableObject
{
    [Header("Session Capacity")]
    [SerializeField, Min(2)] private int maxPlayers = 2;

    [Header("Client Reconnect")]
    [SerializeField, Min(1)] private int reconnectMaxAttempts = 3;
    [SerializeField, Min(0.1f)] private float reconnectRetryDelaySeconds = 2f;
    [SerializeField, Min(1f)] private float reconnectStateCacheSeconds = 30f;
    [SerializeField, Range(16, 64)] private int reconnectTicketByteLength = 32;
    [SerializeField, Min(1f)] private float connectionShutdownTimeoutSeconds = 10f;

    /// <summary>새 연결 전에 이전 NGO 종료를 기다리는 최대 시간입니다.</summary>
    public float ConnectionShutdownTimeoutSeconds => Mathf.Max(1f, connectionShutdownTimeoutSeconds);

    [Header("Host Migration")]
    [Tooltip("Dashboard Disconnect Host Migration Time과 맞출 안내용 유예 시간. 서버 설정을 변경하지 않습니다.")]
    [SerializeField, Min(1f)] private float hostMigrationGraceSeconds = 15f;
    public float HostMigrationGraceSeconds => Mathf.Max(1f, hostMigrationGraceSeconds);
    [SerializeField, Min(1f)] private float sessionRecoveryPollSeconds = 5f;
    [SerializeField, Min(1f)] private float sessionRecoveryTimeoutSeconds = 240f;
    public float SessionRecoveryPollSeconds => Mathf.Max(1f, sessionRecoveryPollSeconds);
    public float SessionRecoveryTimeoutSeconds => Mathf.Max(1f, sessionRecoveryTimeoutSeconds);
    [SerializeField, Min(1f)] private float migrationSnapshotIntervalSeconds = 5f;
    [SerializeField, Min(1f)] private float migrationDataTimeoutSeconds = 5f;
    [SerializeField, Min(0f)] private float postMigrationInvulnerabilitySeconds = 3f;

    /// <summary>Host를 포함해 Session에 참가할 수 있는 최대 플레이어 수입니다.</summary>
    public int MaxPlayers => maxPlayers;

    /// <summary>일시적인 연결 단절 후 같은 Session에 재접속을 시도할 최대 횟수입니다.</summary>
    public int ReconnectMaxAttempts => reconnectMaxAttempts;

    /// <summary>각 재접속 시도 사이에 대기할 초 단위 시간입니다.</summary>
    public float ReconnectRetryDelaySeconds => reconnectRetryDelaySeconds;

    /// <summary>연결이 끊긴 플레이어의 인벤토리·스탯을 서버 메모리에 유지할 시간입니다.</summary>
    public float ReconnectStateCacheSeconds => reconnectStateCacheSeconds;

    /// <summary>예측하기 어려운 재접속 티켓을 생성할 때 사용할 난수 바이트 길이입니다.</summary>
    public int ReconnectTicketByteLength => reconnectTicketByteLength;

    /// <summary>현재 Host 상태를 Session 마이그레이션 데이터로 업로드하는 간격입니다.</summary>
    public float MigrationSnapshotIntervalSeconds =>
        Mathf.Max(1f, migrationSnapshotIntervalSeconds);

    /// <summary>마이그레이션 데이터 업로드·다운로드 한 번의 제한 시간입니다.</summary>
    public float MigrationDataTimeoutSeconds =>
        Mathf.Max(1f, migrationDataTimeoutSeconds);

    /// <summary>새 Host의 상태 복원이 끝난 뒤 서버가 피해를 차단할 시간입니다.</summary>
    public float PostMigrationInvulnerabilitySeconds =>
        Mathf.Max(0f, postMigrationInvulnerabilitySeconds);
}
