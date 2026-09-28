namespace EchoZone.Enemy
{
    /// <summary>경찰 AI가 현재 수행할 행동 단계를 구분합니다.</summary>
    public enum PoliceEnemyState
    {
        Patrol,
        Suspicious,
        Chase,
        Combat,
        Search
    }
}
