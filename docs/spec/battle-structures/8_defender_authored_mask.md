# unit 8 — 방어 측 저작 타겟 마스크

## 목적

**방어유닛이 적 거점을 때릴 수 있게 한다.** 공성 승리 조건(«적 마음 HP 0»)의 물리적 전제다 — 지금은 불가능하다.

후보 풀은 **이미 열려 있다**: `AttackSystem:44` 의 후보 쿼리는 `FactionTag + Health + LocalTransform` 이라 거점이 이미 후보다. 막는 것은 `targetMask = (int)Faction.EnemyUnit` **리터럴**뿐이다. 그래서 이 unit 은 unit 1(적의 `EnemyTargetFilter.factionMask`)의 **정확한 거울**이다.

곁들임: `NeutralInstinct` 배치 배제 일반화(구 후속 후보 B-M9). 같은 «진영 리터럴 → 비트 술어» 성격이라 한 커밋에 든다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `targetFactions` 신설
- `Assets/_Project/Scripts/Battle/Combat/DefenderTargetDefaults.cs` (신설) — 순수 derive
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:6181`(배치 방어유닛) · `:6407`(`CreatePatrolEntity`) — 리터럴 치환
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:1065` — `EnemyInstinct` 리터럴 → 술어

## 구현

**저작 필드** — `DefenderUnitData` 에 `public Faction targetFactions = (Faction)Factions.AnyEnemy;`

필드 이니셜라이저는 **기존 에셋에도 적용된다**(unit 1 에서 실측: YAML 키가 없어도 런타임값은 이니셜라이저값). 즉 마이그레이션 없이 전 방어유닛이 `AnyEnemy` 를 받는다 — 그게 의도한 변화다.

**`targetAllies` 는 승격하지 않고 오버라이드로 남긴다.** 승격하면 기존 힐러 에셋의 `targetAllies: 1` 이 죽고 새 필드 기본값(`AnyEnemy`)이 이겨 **힐러가 적을 때리기 시작**한다. 그리고 `:6177` 주석이 경고하는 함정이 여기 있다 — 아군 타게팅을 `AnyDefender` 로 넓히면 `IncomingHeal` 버퍼가 없는 거점이 후보에 들어 **ECB playback 에서 던진다**. 힐러는 `DefenderUnit` **단독**이어야 한다.

```
DefenderTargetDefaults.Resolve(int authoredMask, bool targetAllies)
    targetAllies      → (int)Faction.DefenderUnit   // 힐러 — 거점 배제(버퍼 부재)
    authoredMask == 0 → (int)Faction.EnemyUnit      // 인스펙터에서 비운 경우의 레거시 폴백
    else              → authoredMask
```

호출처 2곳(`:6181`·`:6407`) + 테스트라 추출 기준을 통과한다(CLAUDE.md 제약 10 (b)(c)). `EnemyTargetDefaults.Resolve` 와 형태를 맞춘다.

**`HazardCastState.targetMask`(`:6223`) 는 손대지 않는다.** 그것은 «누구를 때리나» 가 아니라 «장판을 어디에 깔까» 의 조준점이다. 움직이지 않는 거점을 슬로우 장판의 조준점으로 삼는 것은 이 spec 이 답하는 질문이 아니다.

**NeutralInstinct 일반화** — `:1065` 의 `st.faction == Faction.EnemyInstinct` 를 술어로:

```
IsInstinct(faction) && (faction & Factions.AnyDefender) == 0   // 적대적 본능
```

`GeneratedMap.structures` 는 SO 참조를 실을 수 없어 그 자리에서 `targetFactions` 를 읽을 수 없다. 「방어 진영이 아닌 본능」이 그 자리에서 표현 가능한 가장 정확한 술어이고, 중립을 여는 날 추가 작업 없이 걸린다.

## 완료 기준

- 컴파일 0 (신설 파일이므로 `dotnet build` 검증 전 csproj 반영 확인 — 미반영이면 조용히 빠진다)
- EditMode 신설: 기본 저작(`AnyEnemy`) 베이크 · 힐러(`targetAllies`)가 `DefenderUnit` 단독 · 비운 마스크(0)가 `EnemyUnit` 로 폴백 · 중립 본능이 배치 배제를 받고 방어 본능은 안 받는다
- `rg 'targetMask = \(int\)Faction\.EnemyUnit'` **공집합** (`HazardCastState` 제외 — 위 명시)
- EditMode 전량 무회귀 (기준선 2049 / 실패 0 / 의도적 스킵 3)
- 침략 맵 게임플레이 변화 **0** 확인 — 적 거점이 없는 맵에서는 `AnyEnemy` 의 거점 비트에 해당하는 엔티티가 아예 없다. 기존 PlayMode 골 3종 그린으로 확인
