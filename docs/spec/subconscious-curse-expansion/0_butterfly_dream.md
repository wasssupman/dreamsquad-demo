# 0. 호접몽 — 잠 완주형 개체 저주

## 목적

부착 즉시 호스트를 4초 재우고(리스크 선불·즉발), **피격 없이 완주해야만** 공격력 +35% 영구 버프를 주는(리턴 후불·지속) Unit 저주. 꿈 파탄 경로는 기존 wake-on-hit 를 그대로 재사용한다 — 신규 잠 변종을 만들지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.DreamCocoon = 14` append + 필드 해석 주석
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — bake 분기 (trigger 가드 앞 즉발 계열, SelfBuffLethal/PlacementAura 선례)
- 신규 `Assets/_Project/Scripts/Battle/Effects/DreamCocoon.cs` — Effects 소유 IComponentData
- 신규 `Assets/_Project/Scripts/Battle/Effects/DreamCocoonSystem.cs` — Effects ISystem
- `Assets/_Project/Data/Dreamcatcher/Card_ButterflyDream.asset` 신규 + `DreamcatcherCardCatalog.asset` 등록
- PlayMode 테스트 (기존 `DreamcatcherCursedRelicTest` 패턴)
- `Assets/_Project/Tests/EditMode/DreamcatcherCatalogSyncTests.cs` — 무의식 풀 로스터 4장으로 갱신 + 호접몽 에셋 계약 테스트

## 구현

**카드 인코딩** — `id=sub_butterfly_dream`, `displayName=호접몽`, `type=Unit`, `category=Subconscious`, `axis=All`,
`mechanics=[{ trigger: None, payload: DreamCocoon, magnitude: 35 (완주 버프 %), duration: 4.0 (잠 초), buffStat: AttackDamage }]`
(SelfStatBuff 의 buffStat 선택자 재사용 — 정의 계층은 Battle.StatKind 를 모른다.)

**bake (ApplyDreamcatcherCardToUnit 내 즉발 분기)**:
1. preflight — 호스트가 이미 `DreamCocoon` 보유 시 **아무 것도 적용하기 전에** 거절(-1, 무차감. LethalTimer preflight 선례).
2. `magnitude <= 0 || duration <= epsilon(0.05f)` → skip 경고 — `duration − epsilon` 이 음수면 무수면 즉시 완주 foot-gun (critic m3). epsilon 은 내부 상수이지 튜닝 노브가 아님을 주석으로 명문화(제약 6 대상 아님).
3. `MapDcBuff(buffStat, magnitude)` 로 (StatKind, 배율) 번역.
4. `EffectSpawner.ApplyCc(Sleep, duration)` + `AddComponentData(DreamCocoon { remaining = duration − 0.05f, stat, mult, stackId = _dcStackCounter++ })`.
   - epsilon 0.05s: 완주 프레임이 CcDecay 자연만료 프레임과 겹치지 않게 하는 **보조 안전핀** — 실제 파탄/완주 분기 결정은 아래 시스템의 `remaining` 가드다 (critic M2 교정).

**DreamCocoonSystem (Effects)**:
- **순서 지정 필수 (critic M2)**: `[UpdateInGroup(typeof(BattleSimGroup))]` + `[UpdateAfter(typeof(CcClearSystem))]` + `[UpdateBefore(typeof(CcDecaySystem))]` — 피격 wake(CcClear)가 같은 프레임에 반영된 뒤 판정하고, 자연만료(CcDecay)는 판정보다 늦게 일어난다 → 프레임 히치(dt>epsilon)에서도 "자연만료→파탄 오인"이 구조적으로 불가. (CcDecay 는 Movement 이후로만 핀이라 satisfiable — 구현에서 Play 실증 완료.)
- 프레임당 판정 순서(결정론): ① **파탄 체크** — Sleep 부재(`CcEffect` 버퍼) && `remaining > 0` → 꿈 파탄, 컴포넌트 제거(버프 없음). ② `remaining -= dt` 감산. ③ **완주 체크** — `remaining <= 0` → `StatModifierApplyEvents` 에 self 영구 버프 enqueue(`origin=Dreamcatcher`, TTL=DcDuration 급) 후 컴포넌트 제거. 마지막 프레임 동시(피격+만료) 케이스는 ①이 선행하므로 파탄으로 결정.
- **`remaining > 0` 가드가 파탄/완주의 실제 disambiguator 다** — 프레임 히치(dt > epsilon)에서도 이 가드가 정합을 유지한다. 구현 시 이 가드를 제거하면 안 된다 (critic M2).
- 소유권: DreamCocoon 은 Effects 소유(쓰기=본 시스템+bridge 부착 시. bridge 의 부착 시점 쓰기는 LethalTimer/CcEffect 선례).

**경계**:
- wake-on-hit 은 기존 경로(`DamageApplicationSystem` → CcClearRequests) 무수정 — 그 자체가 이 카드의 리스크.
- 잠든 채 사망 → 엔티티와 함께 컴포넌트 소멸, 회수 핸들 0(엔티티 부착형). 카드는 기존 host-사망 회수로 큐 복귀.
- 이미 잠든 호스트(느린 각성 placement sleep)에 부착 → `ApplyCc` 가 같은 kind 를 max(remainingTime) 병합(`EffectSpawner.cs:33-43`). 완주 판정은 cocoon 타이머 기준이라 안전하나, **완주 버프가 아직 잠든(공격 불가) 유닛에 부여될 수 있다**(기존 잠이 cocoon 보다 길 때) — 버그 아님, 의도된 상호작용 (critic m7 명문화).
- 완주 버프는 empower aura 가 자동 점등(Dreamcatcher origin) — 별도 연출 없음.

## 완료 기준

- [x] compile 0 에러 (`dotnet build` 또는 Unity)
- [x] PlayMode: ① 무피격 4초 완주 → DamageMul 활성(공격력 상승 실측) ② 잠 중 피격 → 잠 해제 + 버프 없음 + 컴포넌트 제거 ③ 이중 부착 → 거절 + 게이지 무차감
- [x] 기존 Sleep(느린 각성/placement-aura) PlayMode 무회귀

확인 2026-07-16 — dotnet build 0 에러 · PlayMode 8/8(DreamCocoonTest 3 + ActionLockTest 2 + PlacementAuraTest 3, MCP run_tests) · EditMode 854 중 예상된 풀 로스터 테스트 1건 갱신 후 카탈로그 suite 7/7 · 시스템 순서 핀 satisfiable 실증.
