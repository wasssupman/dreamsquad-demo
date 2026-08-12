# unit 8 (rev 2) — 고유 리그 통로 개통 + 스파인 2종 스왑

## 목적

요구사항 1 을 닫는다 — 소환사와 순찰병에 **유닛 고유 스켈레톤**을 입힌다.

**rev 2 재정의 (2026-08-12 사용자)**: 이 작업의 본체는 에셋 교체가 아니라 **파츠형 단일 리그 전제를 데이터에서 끄고 고유 리그 렌더 모드를 처음 실사용에 넣는 것**이다. 오늘까지 디펜더·적 44개 유닛 전원이 `Casual Character` 하나(guid `ee98f82…`)를 공유했다 — 고유 리그 경로는 코드에 있으나 쓰인 적이 없다.

**코드 변경 0 이 계약이다.** 유닛 구조·로직·인터페이스를 건드리지 않고 마지막 visual 단계만 바꾼다.

## 변경 대상

- `Assets/_Project/Spine/` — CH1(소환사) · Doll(순찰병) 임포트 산출물. **현재 untracked** — 이 커밋에서 .meta 짝과 함께 편입
- `Assets/_Project/Spine/Doll_SkeletonData.asset` — `atlasAssets` 재지정
- `Assets/_Project/Data/Defenders/Defender_Summoner.asset` · `Defender_PatrolSoldier.asset` — Spine 블록 교체

## 왜 코드가 안 필요한가 (판정 2026-08-12)

- `ISpineUnitVisualData.SpineSkeletonDataAsset` 은 유닛당 SO 필드고, `SpineUnitView.Spawn`(`:77`) 이 그대로 렌더러에 꽂는다. 파츠형 전제는 **데이터 쪽에만** 있다.
- 파츠 합성이 조건부다 — `SpineCombinedSkinCache.ResolveSkin`(`:58`): `partSkins` 가 비면 단일 `spineSkinName`, 그것도 비면 `null` 반환 = 스킨 미적용(스켈레톤 default 유지). **끄는 스위치가 이미 있다.**
- 애니는 `ResolveAnimation`(`:786`)이 `Skeleton.Data.FindAnimation` 으로 실존을 확인하고 첫 히트를 쓴다. 리그별 이름 차이를 SO 필드가 흡수한다. **대소문자를 구분**하므로 정확히 저작할 것.
- 코드에 남은 유일한 리그 가정 = facing 부호(`:694` "ScaleX=+1 이 -x 를 본다"). 반대로 그려진 리그는 `SkeletonFlipXModifier` 를 `SkeletonDataAsset.skeletonDataModifiers` 에 꽂아 **데이터에서** 정규화한다 — 그 파일 주석이 못박은 규약이다. 코드 분기 금지.

## 에셋 실측 (2026-08-12)

| | CH1 (소환사) | Doll (순찰병) |
|---|---|---|
| export | Spine **4.3.23** | Spine **4.3.23** |
| 규모 | 106 본 / 30 슬롯 / 아틀라스 1284×427 | 30 본 / 7 슬롯 / 278×91 |
| 스킨 | `default` 하나 | `default` 하나 |
| 트랙 | `idle` `idle2` `idle3` `attack1` `attack2` `attack3` `attack-original` `drop` | `walk` `attack` |
| 없는 트랙 | **die** | **die** · **idle** |

런타임은 spine-unity **4.3**(4.3.00+ export 요구) — 두 에셋이 4.3.23 이라 맞다. rev 1 의 "런타임은 4.2" 는 폐기. attachment→아틀라스 리전은 양쪽 100% 해석된다(CH1 28/28 · Doll 7/7).

## 구현

### ① Doll_SkeletonData 아틀라스 재지정 (선행)

두 SkeletonData 가 **똑같이 `CH1_Atlas`**(guid `3c0e8caf…`)를 가리키고 `Doll_Atlas`(`06016f32…`)·`Doll_Material`·`Doll.png` 는 미사용이다. CH1 아틀라스가 doll 리전 6개를 전부 포함해서(소환사 리그가 인형을 들고 있다) **우연히 렌더돼 조용히 지나간다** — 인형 하나에 1284×427 텍스처를 물린 채로. `Doll_Atlas` 로 바꾼다.

### ② 파츠 경로 OFF (두 SO 공통)

| 필드 | 현재 | 변경 후 | 이유 |
|---|---|---|---|
| `skeletonDataAsset` | Casual Character | `CH1_SkeletonData` / `Doll_SkeletonData` | — |
| `partSkins` | 10줄 | **비움** | 고유 리그엔 파츠 스킨이 없다 |
| `spineSkinName` | `full_skins` | **비움** | CH1/Doll 은 `default` 뿐. 두면 미발견 경고 + 스킨 미적용 → 안 보일 위험 |
| `slotColors` | 소환사 3줄 | **비움** | Casual Character 슬롯명(`hair` 등) 기준 — 새 리그엔 없어 경고만 남는다 |
| `spineVisualScale` | 1.3 | **0.48**(CH1) / **0.55**(Doll) | 아래 |

**소환사 = 로스터 정합, 소환물 = 의도적으로 작다.** 출발값은 높이 정합(`1.3 × 187.92 ÷ 리그높이`)으로 뽑아 CH1 0.483 / Doll 0.787 이 나왔고, CH1 은 그대로 채택했다(다른 방어유닛과 같은 키). Doll 은 **0.55 로 낮췄다**(사용자 결정 2026-08-12) — 소환물이 유닛과 같은 키면 판에서 둘의 위계가 안 읽힌다. 실측 비교(규격 유닛 meshH 2.49 대비 0.65→2.05 · 0.55→1.73 · 0.45→1.42)에서 0.55 = 유닛의 약 70% 를 채택했다. 더 작게 가려면 0.45 가 다음 후보이고, 애완동물 쪽 실루엣으로 넘어간다.

**이후 로스터 전체 크기 패스(2026-08-12)로 방어유닛 27종에 ×1.3 이 곱해졌다** — 그래서 현재 저작값은 소환사 **0.624**, 소환물 **0.715** 다. 위 0.48/0.55 는 그 패스 **이전** 값이고, 둘의 비율(소환물 = 유닛의 약 70%)은 균일 배수라 그대로 유지된다.

### ②-1 Doll 루트 재중심 (리뷰에서 발견, 2026-08-12)

`Doll.json` 의 루트 본이 `x: -254.6658` 이었다. 아트가 그 루트를 중심으로 놓여 있어(bounds -356.44~-155.12, 중심 ≈ -255.8) **스켈레톤 원점 기준으로 통째로 왼쪽에 그려진다** — scale 0.01 이면 약 2.5 월드 유닛, 인형이 실제 위치에서 2 타일쯤 왼쪽에 보이고 타겟팅·충돌은 진짜 위치에서 일어난다.

`walk`·`attack` 어느 쪽도 root 를 키잉하지 않는 것을 확인하고 **소스에서 `root.x` 를 0 으로** 되돌렸다(헤더 `x` 도 -101.77 로 동반 갱신). `SpineVisualOffset` 보정을 쓰지 않은 이유: 그 보정값은 `spineVisualScale × CharacterVisualScale` 에 비례해서, 스케일을 만질 때마다 같이 틀어지는 숨은 결합이 생긴다. **재export 하면 재발할 수 있는 항목이다.**

### ②-2 순찰병 무기 궤적 해제

`weaponTrailPrefab`(`WeaponTrail_Slash_Simple`)의 `BoneFollower` 가 `Gear` 본에 바인딩돼 있다(`WeaponTrail_Slash.prefab:76`) — Casual Character 본이고 Doll 에는 없다. 그대로 두면 `BoneFollower.Initialize` 가 본을 못 찾아 경고만 남기고 궤적은 안 나온다. 프리팹 유무가 유일한 게이트이므로 **비운다**. Doll 본 기준 궤적 재저작은 후속.

### ③ 애니 필드 저작 (대소문자 정확히)

| 필드 | 소환사(CH1) | 순찰병(Doll) |
|---|---|---|
| `idleAnimation` | `idle` | `walk` — idle 트랙 없음, 대체 저작 |
| `walkAnimation` | 빈칸 (타일 고정) | `walk` |
| `attackAnimation` | `drop` | `attack` |
| `deathAnimation` | **빈칸** — Play 확인 후 재검토 (아래) | **빈칸** — 구조적 미사용 |
| `dragAnimation` / `deployAnimation` | `idle` / `attack1` | 빈칸 (배치 경로 없음) |

`attackAnimation: drop` — 소환 순간에 `AttackState` 를 재사용하므로(unit 3) 인형을 내려놓는 `drop` 이 의미상 맞다. 현재 값 `Attack3` 는 CH1 에 없어 `PlayAttack` 이 **조용히 no-op** 된다(단일 후보, 폴백 없음).

### ④ die 판정 (사용자 2026-08-12 + 코드 확인)

death 애니를 재생하는 유일한 구동원은 `BattleBridge.DrainDefenderDeathEvents`(`:3296`) → `SpineUnitPool.NotifyDeath`(`:73`) → `SpineUnitView.Kill()` 이고, 그 큐는 `_defenderByTile` 바인딩 기반이다.

- **순찰병**: 계약 1(`DefenderTile` 미부착)로 `DefenderDeathEvent` 를 발행하지 않아 `Kill()` 에 **도달할 수 없다** → `DespawnMissing`→`Dispose()` 즉시 소멸. die 필드는 무엇을 넣어도 나오지 않으므로 **빈칸이 정직하다.**
- **적**: `NotifyDeath` 호출처가 그 한 곳뿐이라 적도 death 애니를 재생하지 않는다.
- **소환사**: `DefenderTile` 보유 → 경로가 살아 있다. 빈칸이면 `Kill()` 이 즉시 `Destroy`(`:643-648`) 해서 다른 방어유닛과 달리 툭 사라진다. Play 육안 확인 후 `idle3` 대체 또는 빈칸 수용을 결정하고 결과를 이 문서에 기록한다.

### ⑤ facing 확인

CH1/Doll 이 규약(ScaleX=+1 → 왼쪽)과 반대로 보면 `SkeletonFlipXModifier` 에셋을 만들어 각 SkeletonData 의 `skeletonDataModifiers` 에 넣는다.

## 완료 기준

- [x] `Doll_SkeletonData.atlasAssets` = `Doll_Atlas`
- [x] 두 SO 에 파츠 리그 잔존 참조 0 (`Casual Character` guid · `full_skins` · `_c_` 파츠 경로)
- [x] `Doll.json` 루트가 원점 (②-1)
- [x] 두 리그가 새 외형으로 렌더된다 — 에디터 실측 메시 높이 소환사 2.31(규격 유닛 2.49 와 정합) · 순찰병 **1.73**(규격의 약 70%, 의도적 축소). 머티리얼도 각자 것(`CH1_Material` / `Doll_Material`)
- [x] **인형이 자기 위치에 그려진다** — x=0 스폰의 메시 중심 x=-0.04 (②-1 이전이면 약 -2.0). ②-1 회귀 확인
- [x] **코드 변경 0** — 이 작업으로 바뀐 `.cs` 0개
- [x] 콘솔 에러/경고 0 — `[SpineCombinedSkinCache]` 0건 · `BoneFollower` 본 미발견 0건
- [x] 회귀: **EditMode 2167 중 2164 통과 · 실패 0 · 스킵 3**(기존 `[Ignore]`) · **PlayMode `PatrolDefenderPlayTest` 2/2**
- [x] 파츠형 무회귀(데이터 레벨) — 대조군 Scout 이 `partSkins` 10 + `full_skins` 해석 정상, 조합 스킨으로 렌더
- [ ] 파츠형 무회귀(육안) — 디펜더 로스터 전원 + 적 웨이브 1회. 이 커밋 이후 파츠형 잔존은 **42유닛**(실측)이다
- [ ] `spineVisualScale` 0.48/0.55 를 실제 타일 위에서 확정 (소환물이 유닛보다 작게 읽히는지 포함)
- [ ] **순찰병이 `walk` 로 이동한다** — 이 유닛의 핵심. 정지 슬라이딩이면 실패
- [ ] 소환 순간 `drop` 이 재생된다
- [ ] facing 이 이동/타겟 방향과 맞는다 (틀리면 `SkeletonFlipXModifier` 로 교정, 코드 0 유지)
- [ ] 소환사가 전투 밖 3경로에서도 뜬다: 드래그 프리뷰(`DefenderDragPlacementController:1368`) · 스쿼드 상세(`SquadUnitDetailView.BindSpine`) · 항아리 피규어(`SpineFigureBuilder` — SkeletonGraphic 경로가 다른 아틀라스를 처음 만난다)
- [ ] 소환사 사망이 툭 사라지는 것으로 수용 가능한가 (death 빈칸)
- [ ] `Assets/_Project/Spine/` 가 .meta 짝과 함께 커밋됐다

## 범위 밖

**소환사 특수 대기 모션 → unit 10.** CH1 에 `idle2`/`idle3` 가 들어와 unit 5 의 보류 사유("쓸 트랙이 없다")는 해소됐지만, 재생에는 `SpineUnitView` 의 idle 오버라이드 API 신설이 필요해 이 unit 의 코드 0 과 양립하지 않는다.

`SpineUpgradeSmoke.cs` 는 Casual Character 를 하드코딩한 에디터 스모크 도구다. 고유 리그를 검증 대상에 넣을지는 별건.
