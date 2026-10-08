namespace Somnia.Battle.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — 트리거 레이어의 어휘. **이 enum 들이 정본이다**(skill-data-table 2026-09-28).
    //
    // 저작(`Somnia.Battle.Data` — 카드 · 유닛 · 적 규칙 저작)이 **이 enum 들을 직접** 든다(skill-data-table unit 4 — 거울 enum
    // `DcTriggerKind` · `DcPayloadKind` · `DcGateKind` · `DcGateSubject` · `DcTriggerSubject` 와 번역 함수 · 번호 핀 은퇴).
    // 에셋은 정수로 직렬화하므로 번호가 곧 저장 형식이다.
    //
    // ⚠ append-only. 앞에 끼우면 저작 에셋의 byte 값이 다른 뜻으로 읽힌다.

    /// <summary>「언제」 — 저작이 직접 든다.</summary>
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
        /// <summary>
        /// skill-data-table unit 4 — **시전**(액티브 카드 · 플레이어 입력). 사건이 아니라 감지자가 없다 — 저작 · 검증 어휘다.
        /// 정의표의 규칙 줄에는 실리지 않는다(카드 빌더가 `None` + `UntilFireCap` 으로 굽는다 · 해시 무변).
        /// </summary>
        Cast = 10,
    }

    /// <summary>「무엇을」 — 효과 종류(0~42 = 43값 · 33~38 = 액티브 시전 6 · 39~42 = 상시 효과 4). 저작이 직접 든다. 옛 이름 `TriggerPayload`.</summary>
    public enum EffectKind : byte
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
        // skill-data-table unit 4 — **액티브 시전 6**(옛 `SkillData.effect` 의 `SkillEffectType` 1:1 · `tables.md` §3). 트리거가 아니라
        // 시전(`TriggerKind.Cast`)과만 짝이다(`EffectComboRule` ⓪). 라우팅 표 밖 — 실행자는 카드 빌더가 레지스트리 id 로 고르고
        // 정의표 효과 줄의 종류는 옛 굽기처럼 `None` 으로 남긴다(해시 무변).
        ActiveMeteor = 33,
        ActiveSlowField = 34,
        ActivePowerSurge = 35,
        ActiveRapidFire = 36,
        ActiveTornado = 37,
        ActivePortal = 38,
        // skill-data-table unit 8 — **상시 효과 4**(옛 카드 전용 저장처 `effects` · `attackMods` 를 효과 줄로 · README 계약 11). 트리거 `None`
        // (보유 시작 순간부터 계속)과만 짝이다(`EffectComboRule` ⓪'). 라우팅 표 밖 — 코어에 「상시」 트리거를 만들지 않고 빌더가 기존
        // 코어 모양으로 편다: 진영 버프 = 「남의 배치 × 자기 스탯 버프(영구)」 줄(카드 `SquadBindings`) · 나머지 셋 = 공격 수식자(`AttackModDef`).
        /// <summary>아군 전체 스탯 — 수혜 대상 = 효과의 `allyFilter`(계약 12).</summary>
        FactionStatBuff = 39,
        /// <summary>투사체 튕김(공격 수식자).</summary>
        ProjectileBounce = 40,
        /// <summary>최전방 우선(공격 수식자).</summary>
        FrontmostTarget = 41,
        /// <summary>수면 적 특효(공격 수식자 · 배율 &gt; 1).</summary>
        DamageVsSleeping = 42,
    }

    /// <summary>게이트 종류 — 저작이 직접 든다.</summary>
    public enum GateKind : byte { None = 0, HpBelow = 1 }

    /// <summary>게이트의 주어 — 저작이 직접 든다.</summary>
    public enum GateSubject : byte { Self = 0, EventTarget = 1 }

    /// <summary>
    /// 이 바인딩이 **누구의 사건**을 듣나. `Self` = 소유자 자신의 사건(유닛 스킬 전부) ·
    /// `Any` = 판 위 누구의 사건이든(배치 오라 · Squad 상속 — 7b). unified-effect-layer unit 5 부터 저작에 노출된다 —
    /// `TriggerSpec.subject` 「남의 배치」(`Any`) + `BindingSubjectFilter.PlacedDefender`(빌더가 옮긴다 · 배치에만 뜻이 있다).
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

    /// <summary>
    /// unit 7d — `Any` 바인딩의 **주어 필터**(코어 축 · unified-effect-layer unit 5 부터 저작 「남의 배치」가 이 값을 싣는다 — rev 3 §1). 직업·코스트 필터
    /// (`SubjectClassMask`·`SubjectCost`)와 **곱(∧)** 으로 읽는다. 닫힌 집합이라 제약 8 에 맞는다.
    /// ⚠ append-only(트레이스·해시에 번호로 실린다).
    /// </summary>
    public enum BindingSubjectFilter : byte
    {
        /// <summary>필터 없음 — 누구의 사건이든.</summary>
        None = 0,
        /// <summary>
        /// **판에 배치된 방어유닛**만(점유가 있는 방어유닛 — 순찰 소환물·거점 제외). 사직서 드랍의 대상이다 —
        /// 옛 `ResignationDropSystem` 이 `DefenderFootprint` + `DefenderUnitTag` 를 함께 물었다(순찰은 점유가 없다).
        /// </summary>
        PlacedDefender = 1,
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
