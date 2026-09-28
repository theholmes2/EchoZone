using UnityEngine;
using Unity.Netcode;
using EchoZone.Enemy;
using EchoZone.Player.View;

/// <summary>체력 변경을 서버 AI 중단과 각 피어의 사망 연출에 연결합니다.</summary>
[RequireComponent(typeof(PlayerStats))]
[DisallowMultipleComponent]
public class PoliceDeadEventGlue : NetworkBehaviour
{
    /// <summary>사망 판정의 원본인 체력 데이터입니다.</summary>
    private PlayerStats playerStats;
    /// <summary>서버 순찰·추격을 중단할 행동 Glue입니다.</summary>
    private PoliceEnemyBrainGlue brain;
    /// <summary>각 피어에서 사망 자세를 재생할 View입니다.</summary>
    private CharacterAnimatorView view;
    /// <summary>이번 생애가 종료될 때 반환할 네트워크 풀입니다.</summary>
    private EnemyNetworkPoolGlue pool;
    /// <summary>사망 연출 이후 반환할 서버 시각입니다. 음수이면 예약이 없습니다.</summary>
    private float returnAt = -1f;
    /// <summary>사망 연출 후 풀 반환까지 남은 시간입니다.</summary>
    public float CaptureReturn(float now) => returnAt < 0 ? -1f : Mathf.Max(0, returnAt - now);
    /// <summary>이미 사망한 경찰의 풀 반환 예약을 복원합니다.</summary>
    public void RestoreReturn(float remaining, float now) { if (IsServer) returnAt = remaining < 0 ? -1 : now + remaining; }

    /// <summary>풀 대여 시 반환 대상을 연결하고 이전 예약을 제거합니다.</summary>
    public void ConfigurePool(EnemyNetworkPoolGlue source)
    {
        pool = source;
        returnAt = -1f;
    }

    /// <summary>기존 적 중앙 업데이트에서 사망한 객체의 반환 시점만 처리합니다.</summary>
    public void ManualUpdate(float serverTime)
    {
        if (!IsServer || !IsSpawned || returnAt < 0f || serverTime < returnAt) return;
        returnAt = -1f;
        pool?.DespawnServer(NetworkObject);
    }
    /// <summary>동일 프리팹의 체력·행동·연출을 연결합니다.</summary>
    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        brain = GetComponent<PoliceEnemyBrainGlue>();
        view = GetComponentInChildren<CharacterAnimatorView>(true);
       
    }
    /// <summary>초기 네트워크 체력이 적용된 후 이벤트와 현재 상태를 연결합니다.</summary>
    protected override void OnNetworkPostSpawn()
    {
        playerStats.AddHealthChangedListener(DeadCheck);
        DeadCheck(0);
    }
    /// <summary>디스폰 시 이벤트 구독을 해제합니다.</summary>
    public override void OnNetworkDespawn()
    {
        playerStats.RemoveHealthChangedListener(DeadCheck);
        returnAt = -1f;
    }
    /// <summary>체력 변경 시 사망 여부를 행동·연출에 전달합니다.</summary>
    public void DeadCheck(int value)
    {
        brain?.SetDeathBlocked(playerStats.IsDead);
        view?.SetDead(playerStats.IsDead);
        if (!playerStats.IsDead) returnAt = -1f;
        else if (IsServer && returnAt < 0f && pool != null)
            returnAt = (float)NetworkManager.ServerTime.Time + pool.CorpseLifetimeSeconds;

    }

}
