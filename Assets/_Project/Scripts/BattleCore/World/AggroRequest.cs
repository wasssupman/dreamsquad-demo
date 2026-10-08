namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 2 — 「이 적을 저 가디언에게 끌어라」.
    //
    // **요청이지 사실이 아니다.** 수용량·선점·도달 가능 게이트를 지나야 붙는다. 그 게이트를
    // 생산자 쪽에 두면 「먼저 온 쪽이 이긴다」가 생산자 순서에 매이고, 히트(전투 루프)와
    // 도발(배치 스킬)이 같은 틱에 섞이는 것은 예외가 아니라 평상이다.
    public struct AggroRequest
    {
        public SimEntityId Enemy;
        public SimEntityId Guardian;

        /// <summary>
        /// 도발인가. 도발은 **수용량과 선점 둘을 우회**한다(나중에 부른 쪽이 이긴다).
        /// 나머지 게이트(공격 수단 부재 · 도달 불가)는 그대로 적용된다 — 도달 불가를 풀면
        /// 「못 가는 곳을 향해 영원히 밀리는 적」이 부활한다.
        /// </summary>
        public bool Taunt;

        /// <summary>시한. 0 = **무기한 센티널**(히트 획득). 뒤집지 말 것.</summary>
        public float Seconds;

        public static AggroRequest Hit(SimEntityId enemy, SimEntityId guardian)
            => new AggroRequest { Enemy = enemy, Guardian = guardian, Taunt = false, Seconds = 0f };

        public static AggroRequest Taunted(SimEntityId enemy, SimEntityId guardian, float seconds)
            => new AggroRequest { Enemy = enemy, Guardian = guardian, Taunt = true, Seconds = seconds };
    }

    /// <summary>
    /// 어그로가 풀린 까닭(`CoreEvent.AggroReleased.Arg`). 해제 자리는 `FieldPrepPhase` 의 셋이 전부다.
    /// append-only — 트레이스 `i` 칸에 그대로 실린다.
    /// </summary>
    public enum AggroReleaseReason : byte
    {
        /// <summary>시한 도발의 시간이 다 됐다(0 = 무기한 센티널은 여기 안 온다).</summary>
        Expired = 0,
        /// <summary>가디언이 죽었거나 판에서 사라졌다(퇴근 포함).</summary>
        GuardianGone = 1,
        /// <summary>장애물이 바뀌어 추격판이 낡았다 — **히트 어그로만** 풀린다(도발은 표시가 남는다, M10).</summary>
        Rebuilt = 2,
    }
}
