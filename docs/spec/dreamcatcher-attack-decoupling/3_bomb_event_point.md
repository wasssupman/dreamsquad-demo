# 3 — 폭탄맨 사건 지점

## 목적

폭탄맨(`BombThrow`)에 **공격 성립 사건**을 준다. RESOLVE 는 손대지 않는다(계약 1) — 폭탄맨은 타겟팅/RESOLVE 경로를 타지 않고 `AttackSystem.cs:215` 에서 early-`continue` 하므로, 그 분기 안에 자기 사건 지점을 두는 것이 유일하게 일관된 방법이다.

이 단위가 끝나면 비수가 폭탄맨에서 실제로 발동한다 — unit 1 이 잠근 조합의 첫 해제다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 폭탄 분기(`:156~215`)에 dc 카운트/발동 훅
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs` — `BombThrow` 사건 지점 개통 + payload 데이터 축 판정
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs` · `BattleBridge.Dreamcatcher.cs` — 시그니처 변경 반영
- 테스트: `DcApplicabilityTests`(잠금 기대값 뒤집기) · `AttackSystemUnifiedLoopTests`(발동 실증)

## 구현

### 카운트 지점 = `landValid == true`

훅은 `if (!actionLocked && cooldownRemaining <= 0f && facing && projectileRef)` 블록 **안**, `if (landValid)` **안**에 둔다. 블록 밖이면 매 프레임 카운트가 돈다.

`landValid == false`(그리드 밖을 향해 배치)는 쿨다운만 돌고 폭탄이 손을 떠나지 않으므로 **카운트하지 않는다**(계약 2 표). 근거: 결정론은 둘 다 같고, 체감은 "던지는 걸 봤다"에 붙는다. 반대로 하면 아무것도 안 던지는 폭탄맨이 니들만 쏘는 그림이 된다.

### 대상 = 자체 탐색

폭탄맨은 적을 조회조차 하지 않으므로 `bestTarget` 이 없다 → unit 2 의 `DcNeedleTargeting.SelectNearest` 를 쓴다. 후보는 `OnUpdate` 상단 스냅샷(`:45~47`)을 그대로 재사용한다(별도 쿼리 금지).

`eligible` 채우는 규칙(unit 2 caller 계약): **진영 `Faction.Enemy` 고정**(host mask 재사용 금지) · 자기 자신 제외 · `tileDist` 는 Chebyshev.

후보가 없으면 발사를 건너뛴다 — 카운트는 이미 소비됐다(계약 5).

### 발동

기존 dc arm 과 동일한 캐리어 패턴: `ecb.CreateEntity()` → `ProjectileSpawnRequest{HomingToEntity × SingleSplash, damage=slot.magnitude, owner=폭탄맨}` + `ProjectileRequestCarrier`. 폭탄 분기의 `ecb` 를 그대로 쓴다(`:181` 의 폭탄 request 와 충돌하지 않는다 — 그건 attacker 엔티티에, 니들은 새 캐리어에 붙는다).

`AttackOutputLogEvent` 도 기존 arm 과 같이 남긴다(로그 일관성).

### 적용성 — payload 데이터 축 추가

`BombThrow` 에서 `ProjectileToTarget` 은 **`tileRange > 0` 을 요구**한다. host 가 대상을 못 주는데 폴백 반경까지 0이면 니들이 영영 안 나가고, 그건 계약 4("부착 가능 ⇒ 발동")위반이다.

이 조건은 host 종속도 순수 데이터 검증도 아닌 **host × 데이터 조합**이라, `EvaluateMechanic` 의 첫 인자를 `DcPayloadKind` → `in DcPayloadSpec` 으로 넓힌다. host 속성은 여전히 profile 하나로 접혀 있고(unit 1 취지 유지), payload 는 판정에 필요한 만큼만 본다.

```csharp
DcRejectReason EvaluateMechanic(in DcPayloadSpec payload, DcTriggerKind trigger, in DcHostProfile host)
```

새 거절 사유 `NeedsFallbackRange` 를 추가한다(append-only).

## 완료 기준

- [ ] 컴파일 클린 + EditMode 그린.
- [ ] `DcApplicabilityTests`: `AttackN × BombThrow` 기대값이 `NoEventPoint` → `None` 으로 **뒤집힌다**(잠금 해제의 증거). `tileRange 0` 이면 `NeedsFallbackRange`.
- [ ] `AttackSystemUnifiedLoopTests` 신규: 폭탄맨 + 비수 슬롯(period 5) → 5번째 **발사 성사** 프레임에 니들 캐리어 1개. `landValid == false` 프레임은 카운트되지 않는다.
- [ ] 기존 회귀 없음: 근접/원거리/머신거너의 카운트 지점·빈도 무변화(계약 1·8).
- [ ] Play: 폭탄맨에 비수 부착이 **허용**되고(`Would=True`), 실전투에서 5발마다 니들이 나간다.

---

확인 일자 / 커밋: (미완)
