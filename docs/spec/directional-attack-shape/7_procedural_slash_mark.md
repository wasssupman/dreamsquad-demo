# 7 — 참격 자국을 판정 도형에서 실시간 생성한다 (사용자 결정 2026-09-14)

## 목적

참격 자국 VFX 의 **모양이 판정 데이터에서 나오게** 한다. 지금은 부채꼴은 반각 30° 를 텍스처에, 띠는 3:1 비율을 메시에
**손으로 구워** 두고 `attackVfxScale` 균일 배율로 키운다 — 이쑤시개 사거리를 2→4 로 올리면 판정 상자는 5칸이 되는데
띠는 3칸으로 그려진다(축별 배율도 없다). 배치 가이드(unit 6)는 이미 각·반경·반폭을 인자로 받아 메시를 만들고 있어
같은 저작에서 **가이드는 참말, 참격은 거짓말**이 되는 구조다.

사용자 제안: *「메시를 굽는 게 아니라 실시간으로 필요한 모양을 생성하고 풀링하는 느낌은?」* → 가이드가 쓰는 메시 빌더를
참격도 쓴다. 굽는 에셋(텍스처 2·메시 2·프리팹 1)이 사라지고 각도 함정(반각 30° 손 저작)이 소멸한다.

## 검증 질문

> 이쑤시개 `attackRange` 를 바꾸거나 브루저 `angleDeg` 를 바꿨을 때, **코드·에셋 편집 없이** 참격 자국이 판정 상자/부채꼴
> (길이 = 사거리 + 내 몸 · 반폭/각 = 저작)과 같은 모양으로 그려지고, 배치 가이드와 겹쳐 보면 윤곽이 일치하는가.

## 변경 대상

| 파일 | 변경 |
|---|---|
| `Presentation/ShapeMeshBuilder.cs` **신규** | `TilemapMapView` 의 `BuildFan/BuildFanOutline/BuildBand/BuildBandOutline` 을 뽑아 순수 정적 빌더로. 채움·테를 **한 메시**에 UV 로 구분해 얹는 `BuildSectorMark/BuildBandMark` 추가(u=0 채움 · u=1 테) |
| `Core/TilemapMapView.cs` | 가이드가 빌더를 **호출만** 한다(픽셀 무변). `CellSize` 접근자 노출 |
| `Presentation/ProjectileViewPool.cs` | `PlayHit(..., Mesh meshOverride)` — 루트 `ParticleSystemRenderer.mesh` 교체. `GetShapeMarkMesh(in ShapeMarkSpec)` **캐시**(키 = kind·각/반폭·길이·cellSize) + 파괴(`OnDestroy`·`DespawnAll` 아님 — 메시는 풀과 수명이 같다) |
| `Bridge/BattleBridge.cs` | 히트 VFX 드레인 2경로(즉시·`PendingHitVfx`): `attackVfxAtAttacker && AttackState.shape ≠ Omni` 면 `ShapeMarkSpec` → 메시를 얹고 **`scale = 1`**(메시가 이미 월드 단위) |
| `VFX/SlashMark_Ramp.png` **신규** | 4×1 램프 — texel 0 = 채움 α, 1~3 = 테 α(오늘 텍스처의 0.42 / 0.95). Point·Clamp·무압축 |
| `VFX/SlashMark_SKELETON.mat` | `_BaseMap` → 램프 |
| `VFX/SlashMark_SKELETON.prefab` | 루트 메시 → 빌트인 Quad(폴백 — 오버라이드 없이 재생돼도 「빠진 참조」가 아니다) |
| `Data/Defenders/Defender_Slasher.asset` | `attackVfxPrefab` → `SlashMark_SKELETON`(띠 프리팹 은퇴) |
| `Defender_Bruiser/Malphite/Slasher.asset` | `attackVfxScale` 1.6·1.6·3 → **1**(참격 유닛엔 안 쓰이지만 옛 값이 남으면 다음 사람이 뜻을 오독한다) |
| **은퇴** | `SlashMark_Sector.png` · `SlashMark_Band.png` · `SlashMark_SectorQuad.asset` · `SlashMark_BandQuad_w1_l3.asset` · `SlashMark_Band_SKELETON.prefab/.mat` |
| `Tests/EditMode/ShapeMeshBuilderTests.cs` **신규** | 아래 「완료 기준」의 기하 단언 |
| `.claude/skills/unity-vfx-authoring/common-skill-vfx-reference.md` · `SKILL.md` 레시피 B | 띠 항목 은퇴 · 「도형은 빌더에서 실시간」 한 줄 |

## 구현

```
AttackState.shape(bake) + attackRange + BodyRadiusTiles ──▶ ShapeMarkSpec{kind, angleDeg | halfWidth, lengthTiles, cellSize}
        │                                                            │
        │ (같은 빌더)                                                ▼
   배치 가이드 ◀──── ShapeMeshBuilder ────▶ ProjectileViewPool 메시 캐시 ──▶ PlayHit(meshOverride)
```

- **빌더는 plain 입력 → 정점/인덱스/UV** 만 만든다(제약 10 — 아키텍처 타입을 모른다). 가이드 4함수는 이름·기하 그대로 옮기고
  마크용 2함수는 채움(uv.x 0) + 테(uv.x 1)를 같은 정점 버퍼에 이어 붙인다. +Y 전방·XY 평면 — 가이드(grid 자식)와 참격
  (`startRotation3D x=90°` 로 +Y→+Z)이 같은 관습이라 빌더가 평면을 몰라도 된다.
- **왜 정점색이 아니라 UV 인가** — 메시 파티클은 정점색 채널을 파티클 색 스트림이 덮어써 메시 자체 정점색이 안 보인다.
  UV 로 「채움/테」 두 영역만 가르고 4×1 램프 텍스처가 알파를 준다. 오늘 텍스처(채움 0.42 · 테 0.95)와 같은 룩, 셰이더·
  파티클 모듈(0.5s · 0.45 홀드 · 페이드) 무변.
- **캐시, 풀 아님** — 메시는 저작 조합 수만큼만 생기고(오늘 3종) 재사용된다. 파티클 GameObject 풀은 `PlayHit` 기존 그대로.
  캐시 키에 **cellSize** 를 넣는다(가이드가 같은 이유로 넣었다 — 맵별 타일 크기).
- **길이 = 사거리 + 내 몸**(가이드·링과 같은 값 — 판정 상자의 점-대상 코어). 대상 몸은 그리지 않는다(가이드도 안 그린다).
- **각·반폭은 bake 값에서** — `AttackState.shape`(sinHalf/cosHalf/halfWidth)를 읽는다. 저작 `angleDeg` 가 아니라 bake 를 읽어야
  reflex(180~360) 저작이 Omni 로 접힌 경우에도 sim 과 같은 답이 된다. 각은 `2·atan2(sinHalf, cosHalf)` 로 되돌린다.
- `attackVfxScale` 은 참격 유닛에서 **무시**된다(메시가 월드 단위). 다른 유닛의 히트 VFX 는 경로가 안 바뀐다.
- `PendingHitVfx` 는 **메시 참조**를 싣는다(재생 시점에 다시 풀지 않는다 — 시점이 달라도 도형은 같다).
- 파티클 루트의 `startSize 1` · 크기 커브 상수 1 이라 메시 단위 = 월드 단위 그대로.

## 지키는 것 / 안 하는 것

- sim 변경 0 · 골든 무관 · 채널 무변.
- 프리팹 이름(`_SKELETON`)은 이 unit 에서 안 바꾼다 — 접미사 제거는 카탈로그 「다음 편집 때」 규칙대로 별도 승인.
- 참격 색(주황)·가이드 색(빨강) 그대로 — 「타격」과 「예고」는 색으로 갈린다.
- 잔불(Embers): 프리팹이 `scalingMode Hierarchy` 라 루트 `localScale` 이 자식 크기까지 곱한다 — `attackVfxScale` 1.6→1 로 잔불이 37% 작아지는
  것을 막으려고 Embers `startSize` 를 ×1.6(0.288~0.48)으로 저작했다(리뷰 M-2). **메시가 월드 단위로 고정됐으니 이제 크기의 정본은 프리팹이다.**
  이쑤시개는 옛 띠 프리팹에 잔불이 없었는데 공용 프리팹으로 오면서 **잔불이 새로 생긴다**(의도 — 참격 한 어휘).

## 리뷰 반영 (2026-09-14 · code-reviewer)
- M-1 풀 인스턴스에 메시가 남는 함정 → `ViewRendererCache.rootMeshOriginal` 을 잡아 두고 오버라이드 없으면 **되돌린다**. 실제로 새던 경로 둘:
  공격자가 드레인 전에 죽어 메시를 못 지은 재생 · reflex 저작이 Omni 로 접힌 재생.
- M-2 잔불 축소 → 위 저작. M-3 루트가 메시 파티클이 아닌 프리팹에 메시를 넘기면 프리팹당 1회 경고. M-4 좁은 각(10°·15° 클램프)·부채꼴 uv 개수·폭 0 하한 테스트 추가.
- L-1 「정점 값 무변」 주석 정정(부채꼴 테 꼭짓점·모서리는 바뀌었다). L-2 아주 좁은 각에서 테 모서리가 반대편으로 넘던 것 → 안쪽 회전각을 반각으로
  클램프(15°·r 0.2 테스트가 실제로 잡았다). L-3 짧은 띠 테 뒤집힘 가드 + 테스트. **L-4 길이 원천을 SO 가 아니라 런타임 `AttackState.range`** 로 —
  사거리 버프가 생겨도 참격이 판정을 따른다. **L-5 가이드도 각을 bake 역산(`ShapeMarkSpec.AngleDegOf`)** 으로 — 「같은 빌더 = 같은 윤곽」이
  「bake ≡ 저작」 가정이 아니라 구조가 됐다. L-6 테스트 Mesh TearDown 회수. L-8 브리지 원점 판정 한 번만(`atAttacker`). L-9 반폭 하한을 캐시 키에서.
- L-10(미반영·기록): 「메시 단위 = 월드 단위」는 `ProjectileViewPool` 의 `lossyScale == 1` 에 기댄다. 참격만 균일하게 어긋나면 풀 부모 스케일부터 볼 것.

## 완료 기준

- [x] `ShapeMeshBuilderTests` 10건: 띠 마크 정점 전부 `x ∈ [−hw, hw]`, `y ∈ [0, L]` · 부채꼴 마크 정점 전부 `|p| ≤ r` 이고 축(+Y)에서 반각 안 ·
      테 정점은 바깥 경계 **안쪽**(테 폭만큼) · uv.x 는 {0, 1} 두 값만 · 가이드 4함수 정점 수·기하 회귀 · spec→메시(bake 각 복원 · cellSize · 길이가
      사거리를 따름 · 캐시 키 동치).
      ⚠ 이 단언이 **unit 6 가이드의 잠복 결함**을 잡았다 — 부채꼴 테의 꼭짓점 정점이 법선 오프셋 w 로 각 **밖**에 찍히고(반각 30° 에서 축과 61°)
      호 쪽 바깥 모서리가 반경을 w²/2r 넘었다. 발밑 7cm · 1mm 라 눈엔 안 보였지만 「테 = 판정 안쪽」이 거짓이었다. 꼭짓점을 마이터(축 위 `w/sinθ`)로,
      모서리를 호 위로 되돌렸다 — 가이드도 같은 빌더라 같이 고쳐졌다.
- [x] EditMode 코어 2680(실패 0 · ignored 3 선행) · 에셋 168(선행 실패 2건 bomb_man·boomerang 문안 외 0) — 2026-09-14 인-에디터.
- [x] 오프스크린 렌더 `scratchpad/live/slash_proc_tint.png`: 부채꼴 60°/길이 2(+Z) · 띠 반폭 0.5/길이 3(+X) · **띠 길이 5(대각)** — 격자 대조로
      크기·방향·채움(주황 α0.42)/테(크림 α0.95) 확인. 길이 5 는 spec 값으로 직접 만들었으므로 SO 를 건드리지 않았다(에셋 diff 0).
      ⚠ 첫 렌더는 회색이었다 — 옛 텍스처가 **주황을 RGB 에 굽고** 파티클 색은 흰색이었다. 램프 RGB 에 같은 값(채움 255,140,38 · 테 255,242,191)을
      실어 룩을 되찾았다. 색을 프리팹 쪽으로 옮기지 않은 이유: 프리팹 무변이 이 unit 의 약속.
- [ ] 라이브(Play): 배치 프리뷰 가이드와 공격 순간 참격 자국의 윤곽이 같은 자리에서 겹친다(가이드 = 판정 = 참격) — 사용자 육안.
      (2026-09-22 마무리 세션: 원 세션 종료로 `wip/slash-mark-unit7` 에 남아 있던 것을 main 에 편입. EditMode 2896 기지 2건 외 초록 · 전투 3판 콘솔 에러 0. 근접 유닛에 적이 안 붙어 참격 캡처는 못 함 — 육안 항목 그대로 사용자 몫.)
- [x] 은퇴 에셋 6개(+meta) 참조 0 확인 뒤 `git rm` · 카탈로그(`common-skill-vfx-reference.md`) · 레시피 B′(`SKILL.md`) 갱신.
- [ ] 사용자 Play 확인.
