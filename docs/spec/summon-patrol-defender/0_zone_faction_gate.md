# unit 0 — `ZoneApplySystem` 적 전용 게이트

## 목적

존 해저드(화염/독/빙결 장판)가 **`PathFollowState` 보유자 전원**에게 효과를 걸고 있다. 진영 필터가 없다. 지금 안전한 유일한 이유는 "`PathFollowState` 는 적만 갖는다"는 암묵 불변식이고, 그 전제는 `docs/reference/object-pipeline-map.md` Defender 행에 "이동 없음(고정) — PathFollowState 미부여"로 명문화돼 있다.

unit 2 가 순찰병에 `PathFollowState` 를 부여하는 순간 이 전제가 깨진다 → **아군이 만든 화염 장판에 아군 순찰병이 타고, 슬로우 장판에 아군이 느려진다.**

이건 이 spec 이 만드는 결함이 아니라 **기존 코드의 잠재 결함**이다. 그래서 계층 A 보다 먼저 닫는다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/ZoneApplySystem.cs`
- `Assets/_Project/Tests/EditMode/` — 신규 테스트

## 구현

`ZoneApplySystem` 의 피해자 쿼리에 진영 게이트를 추가한다. 현재:

```csharp
SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PathFollowState>()
```

`FactionTag` 를 함께 조회해 `Faction.Enemy` 가 아니면 건너뛴다. 형태는 같은 파일 이웃인 `HazardCastSystem` 이 이미 쓰는 것과 맞춘다 — 그쪽은 후보를 뽑은 뒤 `((int)targetFactions[i].value & mask) == 0` 으로 거른다.

**`HazardEffect` 에 대상 진영 필드를 추가하지 않는다.** 오늘 아군 대상 존이 하나도 없으므로 데이터 축을 여는 것은 투기다(제약 8). "존은 적에게만" 게이트 하나면 충분하고, 아군 대상 존이 실제로 생기면 그때 축을 연다(README 후속 후보).

`PathFollowState` 의 나머지 소비처도 이 커밋에서 전수 확인한다 — 현재 5곳:

| 소비처 | 판정 |
|---|---|
| `MovementSystem.cs:18,55` | unit 2 에서 patrol 분기로 다룬다 |
| `ZoneApplySystem.cs:37` | **이 커밋에서 교정** |
| `HazardCastSystem.cs:43` | 안전 — `targetMask` 로 이미 거른다 |
| `BattleBridge.cs:6755` | 적 스폰 bake. 무관 |
| `PathFollowState.cs` | 정의 |

## 완료 기준

- [ ] EditMode: 같은 셀에 있는 `Faction.Defender` 엔티티와 `Faction.Enemy` 엔티티에 존 효과를 적용했을 때, 적에게만 `CcEffect`/`DotEffect`/`StatModifierApplyEvent` 가 들어간다
- [ ] EditMode: 기존 적 대상 존 거동 무회귀 (Slow·DoT·Stack 3종 경로)
- [ ] 기존 EditMode 스위트 전량 통과 (회귀 0)
- [ ] Play: 화염 장판 위를 적이 지나가면 기존과 동일하게 탄다 (육안)
- [ ] 콘솔 에러/경고 0
