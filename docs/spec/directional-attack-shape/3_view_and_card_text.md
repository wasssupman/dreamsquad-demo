# 3 — 뷰: 방향은 타격 순간이 말한다

## 목적

판정에 생긴 방향을 화면이 **틀리지 않게** 보여준다. 링은 원 그대로(계약 8), 방향은 타격 순간의
**지면 VFX** 가 맡는다. 캐릭터는 좌우 반전만(결정 4).

## 사실 관계 — 무엇이 돌 수 있나

| 축 | 돌 수 있나 | 근거 |
|---|---|---|
| Spine 스켈레톤 | ✗ 좌우 반전만 | `SpineUnitView.FaceToward` = `ScaleX` 부호 |
| 무기 궤적(weapon trail) | ✗ | **본 부착**이라 스켈레톤을 따라 좌우만(`weapon-trail-authoring.md`) |
| 히트 VFX(`attackVfxPrefab`) | ✓ 자유각 | `attackVfxFacesTarget` — 브리지가 **뷰 공간**에서 공격자→대상 방향으로 회전(`BattleBridge.cs:4823`) |

→ 설계 초안의 「무기 트레일만 회전」은 **불가**다. 도는 것은 지면 히트 VFX 뿐이고, 그것으로 충분하다 —
부채꼴 참격 자국·창 찌르기 자국을 바닥에 그 각도로 찍는다.

## 변경 대상

- `Data/DefenderUnitData.cs` · `Data/AttackUnitData.cs` — `attackVfxAtAttacker`(bool, 기본 false)
- `Bridge/BattleBridge.cs:4813~` `DrainUnitAttackVisualEvents` — 원점 분기 + 도형 크기 힌트
- `Data/UnitKitSummary.cs:32` — 문안
- VFX 프리팹 2종(부채꼴 참격 자국 · 직선 찌르기 자국) — `unity-vfx-authoring` 스킬, `_SKELETON` 후 통합

## 구현

- **원점 옵션**: 오늘 히트 VFX 는 `evt.targetWorld`(대상 자리)에 터진다. 부채꼴/직선 자국은 **공격자 자리**
  에서 대상 방향으로 뻗어야 하므로 `attackVfxAtAttacker = true` 면 `simPos = 공격자 위치`. 방향 계산은
  기존 `hitFacing` 그대로(뷰 공간). `hitDelaySec` 지연 경로(`_pendingHitVfx`)도 같은 분기를 지난다.
- **크기 힌트**: 브리지는 `defData.attackShape` 를 읽어 스케일을 정한다 — 부채꼴은 `attackVfxScale` ×
  사거리, 직선은 길이 = 사거리·폭 = width. 새 채널 0(`UnitAttackVisualEvent` 는 `attacker`·`targetWorld`
  를 이미 나른다 · 드래곤 브레스 `breathDir/breathHalfAngleDeg` 선례는 채널에 실었지만 여기선 SO 를
  브리지가 직접 본다 — 값의 주인이 SO 다).
- **카드 문안**(`UnitKitSummary.Build`) — 기존 어휘를 키운다(새 기호 X):
  - Omni: 「최대 N체 동시 타격」(무변)
  - Sector: 「**휘두르는 쪽 A° 안** 최대 N체 동시 타격」
  - Rect: 「**찌르는 방향 일직선(폭 W)** 최대 N체 관통」
  - ⚠ **「전방」이라는 단어를 쓰지 않는다**(리뷰 2026-09-11). 이 유닛엔 전방이 없다 — facing 은
    `distance-based-range/11` 에서 은퇴했다. 「전방」을 읽은 플레이어는 배치 방향을 찾고, 없다는 걸
    알면 규칙을 불신한다. 방향은 «때린 놈 쪽»이고 문안이 그 사실을 말해야 한다.
  - `attackTargetCount == 1` 이면 문안 없음(효과 0 — unit 1 경고와 일치).
- 링·프리뷰·그림자·대상 마크 — **무변**. 이 unit 이 그쪽 코드를 건드리면 계약 8 위반.

## 완료 기준

- [ ] `UnitKitSummary` 문안 테스트: Omni 유닛 전건 무변(기존 desc 단언 초록 · 선행 실패 2건 제외) ·
      Sector/Rect 가짜 SO 로 두 문안 단언.
- [ ] 오프스크린 렌더로 두 VFX 프리팹이 «공격자 자리 · 대상 방향» 으로 찍히는 것을 스크린샷 확인
      (`project_offscreen_render_vfx_verify` 기법 — Play/포커스 불필요).
- [ ] 좌우 반전과 VFX 각도가 어긋나는 경우(대상이 위/아래)에 화면이 「어디를 쳤는지」 읽히는지
      unit 4 Play 육안 항목으로 이관.
