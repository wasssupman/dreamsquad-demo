# 4 — Handoff Summary

## Commit

| 해시 | 내용 |
|---|---|
| `d37e3196` | unit 0 — 본 추종 리그 + 정렬 대역 15500 + 벤더 패키지 편입 |
| `314c0033` | unit 1 — 공격 구동 배선 + 가시성 판정 |
| `4aab5bc7` `71117b42` `a340ed48` `851ee392` | unit 2 — 룩 세트 5종 → 과잉 교정 → role 로스터 → 확대·룩 배분 |
| `bd6f079a` `dd573654` | unit 3 — 호스트 일반화 + 보스 적용 + 실보드 확인 |
| `919f82b9` `edf4b67d` | README 정합·설계 오판 기록 / 리뷰 결함 2건 수정 + 검증 종료 |

## Implemented

- Spine 유닛이 공격할 때 `Gear` 본을 따라 리본 궤적이 남는다. **심 변경 0**(전부 프레젠테이션)
- 룩 7종(ToonFire·ToonBlue·ToonWater·Lightning·Simple·ToonGreen·Cyan) — 벤더 프리셋 복사본 +
  룩별 Prefab Variant. **교체 비용 = 유닛 SO 필드 하나**
- 대상: Guardian·Fighter role 7종 + 보스 2종. 나머지는 프리팹 미할당 = 무궤적
- 나이트메어에 무기 스킨 신규 부여(`gear_right_c_40`), 보스 2종 공격 애니를 `Attack3` 로 교체
- 호스트 비종속: 필드가 `ISpineUnitVisualData` 에 있고 `WeaponTrailRig.Bind/Play` 만 알면 붙는다

## 다음에 이걸 건드리는 사람은

**`docs/reference/weapon-trail-authoring.md` 를 먼저 읽는다.** 붙이는 법·룩 추가·모양 튜닝·본 없는
호스트 레시피와 증상→원인 표가 거기 있다. 이 spec 폴더는 **왜 그렇게 됐는지**(실측·기각된 대안·설계 오판)를 남긴 곳이다.

## Key Files

- `Assets/_Project/Scripts/Presentation/WeaponTrailRig.cs` — 리그 자립 컴포넌트(부착·타이머·파티클 정렬)
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `AttachWeaponTrail` / `PlayWeaponTrail` / `UpdateSortingOrder`
- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs` — 궤적 필드 2개
- `Assets/_Project/VFX/WeaponTrail_Slash*.prefab` · `WeaponTrailPreset_*.asset`
- `Assets/Hovl Studio/HSFiles/Scripts/Hovl.HSFiles.asmdef` — 없으면 `Wassup.Runtime` 이 벤더 타입을 못 쓴다

## Verified

- compile 클린 · 콘솔 에러 0
- 실전 보드 촬영: 디펜더 7종 혼합 룩 · 보스 2종 · 유닛당 드로우콜 1
- `LateUpdate` 순서 지연 0 · 레이어 회수 누수 0 · 슬로우모 창 비 1.00 · 파티클 정렬 스윕 통과
- **테스트 없음.** 순수 계산이 `Duration × norm ÷ (entryTS × skelTS)` 한 줄이고 호출처 하나,
  sim-critical 아님(프레젠테이션) → 제약 10 의 과잉 추상화 경계에 걸린다고 판단

## Notes — 되돌리면 안 되는 것

1. **리그 루트의 빈 `Animator`.** 없으면 벤더가 `transform.root` 에 이벤트 수신기를 붙이고
   `workWithoutAnimation=true` 로 켜서 **공격과 무관하게 상시 방출**한다
2. **프리셋의 `recalculatePointsOnAwake=false` / `startActive=false`.** `Awake` 가 `ApplyPresetValues`
   를 먼저 돌려 컴포넌트 인스펙터 값을 덮으므로 **프리셋이 유일한 소스**다
3. **정렬은 프리셋이 실제 적용값.** HS 가 매 `LateUpdate` 끝에 `renderer.sortingOrder` 를 되쓴다
4. **파티클 정렬은 리그 소유 + 호스트 스윕 제외가 한 쌍.** 한쪽만 있으면 매 프레임 다시 덮인다
5. **방출 창은 `_skeleton.timeScale` 까지 나눠야** 슬로우모에서 안 끊긴다
6. **Point A/B 는 무기 실측과 무관.** 클래스 전체가 같은 참격 형태를 갖는 게 의도다
7. **어두운 머티리얼은 이 보드에서 죽는다**(Red·Lightning 탈락). 룩 고를 때 1순위 필터

## 함정 (같은 자리에서 미끄러지지 말 것)

- **저작**: `AddComponent<HS_SwordMeshTrail>()` 가 `Reset()` 을 돌려 Point A/B 위치를 ±0.5 X 로
  덮는다. 위치는 컴포넌트를 붙인 **뒤에** 쓴다
- **하네스**: 스킨 적용 직후 프레임이 안 돌면 메시가 재생성되지 않아 무기가 없는 것처럼 보인다.
  **룩 판정은 반드시 `CopyFrom(main)`** 으로 — 맨 `Camera` 로 찍으면 어두운 헤이즈가 껴 오판한다
- **하네스**: 실전투 중에는 애니 override 가 유지되지 않는다(`PlayAttack` 이 루프를 끊는다).
  궤적 형태를 찍으려면 `StartBattle` 없이 배치만 한 뒤 스윙을 반복 재생
- **설계**: 스코프를 타입 경계에 새기지 말 것 — 상세는 README "설계 오판" 섹션

## Follow-up

- **보스 궤적 크기 결정** — 보스는 `spineVisualScale` 이 커서 호가 ~4타일(사거리 2). 줄이려면
  보스 전용 Variant 에서 Point A/B 만 좁힌다(코드 0)
- **잠재 결함**: `WeaponTrailRig._stopPending` 은 GameObject 비활성화 시 코루틴만 죽고 플래그가
  남아 이후 영구 방출이 된다. 현재 Spine 유닛은 `SetActive(false)` 없이 `Destroy` 만 해 도달
  불가 — **풀링을 도입하면 같이 손볼 것**
- 나머지 후속 후보는 `docs/spec/README.md` Follow-up Backlog 로 이관
