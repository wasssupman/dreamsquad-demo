# 0 — 어휘: 손패 조작 카테고리 + `RecallAttachedToFront`

## 목적

"퇴근하면 붙어 있던 다른 드림캐쳐가 손패 맨 앞으로"를 **데이터로 선언 가능**하게 만든다.
동시에 **손패 조작(hand op)이라는 payload 카테고리**를 1급으로 세워, 두 번째 손패 카드가
브리지·적용성을 건드리지 않게 한다(README 계약 8).
실행은 unit 1(손패 컨트롤러)이 한다 — 여기서 여는 것은 선언과 그 선언을 받는 관문뿐이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` (enum + 술어)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` (`ApplyDreamcatcherCardToUnit` 내부)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs`

## 구현

### 1) enum append + 카테고리 술어

`DcPayloadKind` 끝에 `RecallAttachedToFront` 를 추가한다(에셋이 int 로 직렬화 — **중간 삽입 금지**).
이름 근거: 대상은 **부착분**, 위치는 **앞**. "손패"라는 단어를 쓰지 않는다 — 손패 = 큐 앞 N 이라는
사실은 `DreamcatcherCycleDeck` 만 아는 것이다(계약 13).

같은 파일에 술어를 둔다:

```csharp
public static class DcPayloadKinds
{
    // 실행자가 sim 도 브리지도 아니고 DreamcatcherHandController 인 payload.
    // DcTriggerSlot 을 굽지 않는다. 새 손패 op 는 여기에 한 줄 추가하면 된다.
    public static bool IsHandOp(DcPayloadKind kind) => kind == DcPayloadKind.RecallAttachedToFront;
}
```

주석에 남길 것: `PlacementAura`(브리지 Mono 상태)보다 **한 칸 더 밖**이며, 그래서 sim 이 볼 슬롯이
없다는 사실.

### 2) 적용성 — host 무관

`DcApplicability.EvaluateMechanic` 의 **switch 앞**에서 `IsHandOp` 이면 `DcRejectReason.None`.
kind 별 `case` 로 적지 않는다 — 그러면 술어는 호출처 1개짜리 죽은 추상이 되고(제약 8), 두 번째
손패 op 가 올 때 `case` 를 또 더해야 한다. **술어의 호출처는 bake 와 여기 둘이다.**
근거: 발동 조건이 host 의 공격 모델이 아니라 **사건** 하나라 `targetsEnemies`·`HostProvidesTarget`·
`hasDamageOutput` 어느 축도 게이트가 아니다.
빠뜨리면 `default` 가 `Unclassified` 를 돌려주어 **카드가 조용히 안 붙는다**(fail-closed).

### 3) bake — 통과시키되 슬롯을 굽지 않는다

기존 가드 `if (trigger.kind == OnRetire && payload.kind != SelfTileAoe) → skipped` 를
`… && !DcPayloadKinds.IsHandOp(payload.kind)` 로 넓힌다.

슬롯 조립 **앞**에 전용 분기 — `PlacementAura` 분기와 같은 자리:

```csharp
if (DcPayloadKinds.IsHandOp(m.payload.kind))
{
    // 실행자는 DreamcatcherHandController. sim 슬롯 없음(계약 9).
    if (m.trigger.kind != DcTriggerKind.OnRetire) { /* loud 거절 */ continue; }
    if (m.trigger.gate != DcGateKind.None)        { /* loud 거절 */ continue; }
    attached++; continue;
}
```

⚠ **게이트 거절이 왜 여기 필요한가**(코드리뷰 지적): 이 분기는 게이트 검증 블록보다 **위**에 있어
`continue` 하면 그 검증을 건너뛴다. 그대로 두면 시트에 `gate=HpBelow` 를 적었을 때 **다른 모든
카드는 loud 거절인데 이 카드만 통과하고 게이트가 조용히 무시**된다 — 이 파일이 일관되게 피해 온
"조용한 무효"다. `GateComboSupported` 는 어차피 `OnRetire × 모든 게이트`를 미지원으로 판정한다.

**트리거 화이트리스트가 여기 있는 이유**(계약 11): 손패 컨트롤러가 host 귀속으로 볼 수 있는
사건은 `DefenderRetired`·`DefenderDied`·`EnemyGone` 뿐이고, 이 spec 이 배선하는 것은 퇴근 하나다.
sim 트리거(`AttackN`·`OnKill` …)와 조합하면 **영영 안 터지는 카드**가 되므로 조용히 통과시키지
않는다. `attached++` 가 필요한 이유는 `attached==0` → `-1`(부착 거절, 무차감)이기 때문이다.

### 4) 문안

`DreamcatcherCardText` payload switch 에 한 줄:
`effect = "함께 붙은 다른 드림캐쳐가 손패 맨 앞으로";` — **수치를 하나도 읽지 않는다**(계약 4 rev 2).
트리거 문안("이 유닛이 퇴근하면")은 이미 있다.
**"다른"이 load-bearing 단어다** — 선언 카드 자신은 맨 뒤로 가므로 단독 부착이면 아무 일도
일어나지 않는다. 이 단어가 빠지면 화면이 거짓말을 한다.

## 완료 기준

- 컴파일 0 에러 · 콘솔 경고 0.
- `DreamcatcherCardTextTests` +1 — 「이 유닛이 퇴근하면 → 함께 붙은 다른 드림캐쳐가 손패 맨 앞으로」
  + payload 에 값을 넣어도 문안에 숫자가 새지 않는지까지 고정.
- `DcApplicabilityTests` / `DcApplicabilityMatrixTests` 초록 — 새 kind 가 `Unclassified` 로
  새지 않는지 확인하는 total 판정이 여기 있다.
- **loud 거절 2종**: `AttackN × RecallAttachedToFront`(트리거 축) · `OnRetire × gate≠None`(게이트 축)
  — 둘 다 경고 + 미부착.
- 이 단계만으로는 **게임 동작이 아직 없다**(붙기만 하고 회수는 기존 맨 뒤) — 정상이다.
