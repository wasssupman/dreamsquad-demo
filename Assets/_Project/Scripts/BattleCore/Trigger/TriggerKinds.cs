namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — 트리거 레이어의 **저작 어휘 미러**.
    //
    // 저작 enum(`Wassup.Data.DcTriggerKind` 10 · `DcPayloadKind` 33 · 게이트 2축)은 시트가 **enum 값으로
    // 왕복**하므로 append-only 다. 코어는 엔진을 모르는 어셈블리라 그 타입을 부를 수 없어 **같은 번호의
    // 미러**를 든다. 어셈블리가 갈려 컴파일러가 못 잡으므로 `CoreTriggerEnumPinTests`(EditModeAssets —
    // 저작 타입이 거기서만 보인다)가 값·개수를 모두 대조한다. 변환은 `MatchDefinitionBuilder` 한 곳이고
    // **이름으로** 옮긴다(`PatternSelectionRule` 이 번호 캐스트로 12 중 11 을 오독한 선례).
    //
    // ⚠ append-only. 앞에 끼우면 저작 에셋의 byte 값이 다른 뜻으로 읽힌다.

    /// <summary>「언제」 — 저작 `DcTriggerKind` 미러.</summary>
    public enum TriggerKind : byte
    {
        /// <summary>트리거 없음 = **부착되는 순간**(카드 3장). 감지자가 아니라 부착 지점이 발화시킨다.</summary>
        None = 0,
        AttackN = 1,
        OnDamagedN = 2,
        OnDeath = 3,
        PeriodicTimer = 4,
        HealthThreshold = 5,
        OnKill = 6,
        OnShieldBreak = 7,
        OnRetire = 8,
        /// <summary>배치 활성화 엣지. **`Periodic` seam 을 탄다**(S1).</summary>
        OnPlace = 9,
    }

    /// <summary>「무엇을」 — 저작 `DcPayloadKind` 미러(0~32 = 33값).</summary>
    public enum TriggerPayload : byte
    {
        None = 0,
        ProjectileToTarget = 1,
        SelfTileAoe = 2,
        NextAttackDoubleFire = 3,
        SelfBuffLethal = 4,
        /// <summary>**이관됨** — arm 이 철거되고 일이 발사 명세로 갔다. bake 가 안내하며 거절한다.</summary>
        AreaBarrage = 5,
        SelfBlink = 6,
        /// <summary>**죽은 값**(S17) — 핸들러 0 · 사용 카드 0. 번호만 보존한다.</summary>
        SelfWarmupBuff = 7,
        PlacementAura = 8,
        AllyMoveSpeedAura = 9,
        ApplyCcToTarget = 10,
        ApplyStackToTarget = 11,
        SelfStatBuff = 12,
        /// <summary>**어휘 밖** — 공격의 성질이라 `AttackMod` 가 소비한다(바인딩이 아니다).</summary>
        HeavyStrike = 13,
        DreamCocoon = 14,
        BountyMark = 15,
        AreaSleep = 16,
        EmitProjectilePattern = 17,
        UltimateLeap = 18,
        GrantShield = 19,
        /// <summary>분열 — 7d(`OnSlain`). 유닛 bake 는 그릇만 두고 항목을 건너뛴다(S8).</summary>
        SplitOnDeath = 20,
        AreaBreath = 21,
        SelfOrbitProjectile = 22,
        AreaTaunt = 23,
        SpawnHazard = 24,
        /// <summary>손패 UI 동작 — 코어 규칙이 아니다(7b 의 퇴근 회수 규칙).</summary>
        RecallAttachedToFront = 25,
        AllyStatAura = 26,
        OpponentStatAura = 27,
        GainCost = 28,
        ReduceSkillCooldown = 29,
        AreaApplyStack = 30,
        AreaCc = 31,
        AreaDot = 32,
    }

    /// <summary>게이트 종류 — 저작 `DcGateKind` 미러.</summary>
    public enum GateKind : byte { None = 0, HpBelow = 1 }

    /// <summary>게이트의 주어 — 저작 `DcGateSubject` 미러.</summary>
    public enum GateSubject : byte { Self = 0, EventTarget = 1 }

    /// <summary>
    /// 이 바인딩이 **누구의 사건**을 듣나. `Self` = 소유자 자신의 사건(유닛 스킬 전부) ·
    /// `Any` = 판 위 누구의 사건이든(배치 오라 · Squad 상속 — 7b). 저작 노출이 없는 코어 축이다.
    /// </summary>
    public enum BindingSubject : byte { Self = 0, Any = 1 }

    /// <summary>
    /// 수명 — 「언제 떨어지나」. `fireCap`(발동 횟수 상한)과 **다른 축**이다(정정 5 · M9).
    /// ⚠ 소유자의 소멸은 **어느 수명이든** 바인딩을 떨어뜨린다 — 등록부가 소유자에 매여 있다.
    /// 아래 값은 「그 전에 떨어질 수 있는가」를 말한다.
    /// </summary>
    public enum BindingLifetime : byte
    {
        /// <summary>소유자가 판에서 사라질 때까지(사망·퇴근·제거). 유닛 저작 스킬의 기본.</summary>
        Owner = 0,
        /// <summary>판 끝까지. Match 호스트(레드불 cadence — 7d)가 쓴다.</summary>
        Match = 1,
        /// <summary>`Seconds` 뒤 만료(판의 시계).</summary>
        Timed = 2,
        /// <summary>`fireCap` 을 다 쓰면 떨어진다(발동 N회 = 수명 N회). 표식처럼 「1회 발동 + 계속 부착」과 다르다.</summary>
        UntilFireCap = 3,
        /// <summary>누군가 명시적으로 떼기 전까지(카드 회수 — 7b). 소유자 소멸은 여전히 뗀다.</summary>
        Manual = 4,
    }

    /// <summary>바인딩의 출처 꼬리표 — 진단·7c 카드 펄스가 읽는다. 병합 키가 아니다.</summary>
    public enum BindingOrigin : byte
    {
        UnitAuthored = 0,
        Card = 1,
        Gimmick = 2,
        Match = 3,
    }

    /// <summary>떨어진 이유. `BindingDetached.Arg` 로 실린다.</summary>
    public enum BindingDetachReason : byte
    {
        OwnerRemoved = 0,
        Expired = 1,
        FireCapReached = 2,
        Manual = 3,
        MatchEnded = 4,
    }
}
