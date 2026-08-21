# unit 1 — 파괴 직후: 포신이 떨어지고 연기가 터진다

## 목적

주저앉기만 해서는 「작아졌다」로 읽힐 수 있다. **실루엣이 바뀌어야** 부서진 것이다 —
포신이 몸통에서 떨어져 나와 바닥에 구르고, 그 순간 검은 연기가 한 번 터진다.

**메쉬 정점은 건드리지 않는다**(README 결정). `cannon_base` 리그는 이미
`base → turret → barrel` 3개의 별개 GameObject라, 떼어낼 것이 이미 떼어져 있다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/StructureWreckView.cs` — debris 낙하 + VFX 슬롯
- `Assets/_Project/Prefabs/Structures/Instinct_{Ally,Enemy}.prefab` — debris 지정 + 버스트 자식
- `Assets/_Project/VFX/InstinctWreck_Burst.prefab` (신규 — `VFX_Smoke` 사본)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — 잔해 VFX 대역 1개

## 구현

### 부품 낙하

```
[SerializeField] Transform[] debris;        // cannon_barrel_* (프리팹에서 지정, 이름 탐색 금지)
[SerializeField] float debrisPopSpeed  = 1.6f;   // 초기 상승
[SerializeField] float debrisGravity   = 9f;
[SerializeField] float debrisSpinDegPerSec = 320f;
```

- **물리(Rigidbody/Collider)를 쓰지 않는다.** 이 프로젝트의 뷰는 물리 씬에 의존하지 않고
  (벤더 통합 체크리스트가 콜라이더·RB 제거를 규칙으로 못박는다), 물리를 켜면 잔해가 보드 위를
  굴러다니며 어디로 갈지 저작할 수 없게 된다. **결정론 아치**로 던진다 — 초기 속도(위 +
  바깥 방향) + 중력, 착지 평면은 프랍 루트의 Y, 착지 후 회전 감쇠로 옆으로 누워 정지.
- 바깥 방향은 **포신이 마지막으로 겨눈 방향**을 쓴다 — 이미 그 각으로 서 있으니 그쪽으로
  넘어지는 게 자연스럽고, 프랍마다 다른 그림이 공짜로 나온다(랜덤 불필요).
- **떼어낸 debris 는 기존 스윕에 등록한다 — 새 정리 경로를 만들지 않는다.**
  debris 는 프랍 루트 밖(`root.parent` 밑 전용 컨테이너 `{propName}_Debris`)으로 옮겨야 한다
  — 주저앉는 몸통을 따라 같이 줄어들면 안 되기 때문이다. 그러면 `ClearStructureViews` 가
  프랍만 지우고 잔해는 다음 판까지 남는다(defender-clock-out 의 Detach 사고 유형).
  **채택안**: `Collapse()` 가 만든 컨테이너를 **반환**하고, 브리지가 그것을 `_structureViews`
  리스트에 그대로 넣는다. 그 리스트는 `ClearStructureViews` 가 이미 `if (… != null)` 로
  순회하며 지운다 — **정리 코드도, 새 수명 소유자도, `OnDestroy` 훅도 늘지 않는다.**

  ```
  var debrisRoot = wreck.Collapse();               // 없으면 null
  if (debrisRoot != null) _structureViews.Add(debrisRoot);
  ```

  ⚠ 대안으로 검토했다가 **버린 것**: `StructureWreckView.OnDestroy` 에서 컨테이너를 직접
  지우기. `TeardownCurrentBattle` 은 `OnDestroy` 에서도 불리고 그 시점엔 상대가 이미 Unity
  fake-null 이라, 이 파일에서 `retireFlight?.CancelAll()` 이 정확히 그렇게 터져 정리 전체가
  중단된 실측 사고가 있다(`BattleBridge.cs:648-658`, 2026-08-15). 컨테이너가 `OnDisable` 로
  자기를 치우는 안도 안 된다 — 브리지 자식 트리는 **아무도 비활성화하지 않아** `OnDisable` 이
  안 불린다(같은 주석이 그 이유로 명시적 호출을 쓴다).

### 연기 버스트

```
[SerializeField] GameObject[] wreckVfx;     // Collapse 시 SetActive(true)
```

- 프랍 프리팹 안에 **비활성 자식**으로 미리 넣어둔다. 그러면 위치·스케일·수명이 전부 프리팹
  저작이 되고 코드는 스위치 한 줄이다(계약 4). 벤더 프리팹의 「비활성 그룹」 함정을 이번엔
  **의도적으로** 쓰는 것이다.
- `InstinctWreck_Burst` = `VFX_Smoke` 사본에서: `looping` off · `rateOverTime` 0 +
  **Burst 1회**(count 12~18) · `startLifetime` 0.8~1.2 · `startSpeed`/`startSize` 상향 ·
  `startColor` 검댕 쪽으로 · `gravityModifier` 음수 유지(상승) · `stopAction = Destroy`.
- **`scalingMode = Hierarchy`** — 아니면 프랍의 `viewScale`(현재 0.4)이 연기에 안 먹어
  거점 하나가 맵 절반을 덮는다.
- 정렬: 벤더는 order 0~2 로 온다. 새 상수 `BoardSortOrder.StructureWreckOrder` 를 두고
  프리팹 내부 상대 순서는 **더해서** 보존한다(빔·브레스와 같은 규약).
  대역 선택은 완료 기준에서 Play 로 정한다 — 잔해는 「이미 끝난 배경 사건」이라 유닛 아래
  음수 대역이 기본값이고, 연기 기둥이 몸통에 잘려 안 읽히면 유닛 위로 올린다.
  **현재 음수 대역 점유**(`BoardSortOrder.cs`): `AimArrow −11` · `SpawnAlert −9~−6` ·
  `Shadow −5` · `TileGauge −4` · `UnitAttackAoe −3~−1` · 바닥 타일맵 `ground −20`/`overlay −10`.
  즉 유닛 아래에서 **비어 있는 자리는 −12 이하이거나 그림자~게이지 사이뿐**이다. 겹쳐도
  무해한지(둘 다 유닛 아래)까지 판단해서 고르고, 근거를 상수 주석에 남긴다.
- 기존 돌파편 VFX(`SpawnGoalCollapse`)가 같은 프레임에 이미 터진다. 둘이 겹쳐 과해지면
  **저작으로** 줄인다(버스트 count/크기). 코드에서 기존 호출을 끄지 않는다 — 그건 다른 거점
  전부에 영향을 준다.
  **저작 조정 2회로 안 잡히면 멈추고 하나를 고른다**: 돌파편(공용·셀 중심·거점 전부)과
  연기 버스트(프랍 부착·viewScale 추종) 중 무엇이 「본능이 부서졌다」를 말하는지 정하고
  나머지는 그 역할에서 뺀다. 두 애셋 사이 왕복 튜닝을 무한히 돌리지 않는다.

## ⚠ 이 unit 이 실제로 알아낸 것 — 연기 저작의 두 함정

둘 다 「값은 맞는데 화면이 이상하다」 형태라 로그·테스트로 안 잡힌다. 오프스크린 렌더 3회로 드러났다.

1. **`A_Smoke_2` 는 퍼프 한 장이 아니라 «퍼프 한 무더기»의 3×3 flipbook 이다.**
   그래서 **작게 여러 개 뿌리면 연기가 아니라 돌조각으로 읽힌다.** 크게·적게가 정답이다
   (버스트 `startSize` **4.8~8.4** · count 9 · 잔불 rate 2.2). 벤더 기본값(0.4~0.6)을 그대로 쓰면
   `scalingMode = Hierarchy` × `viewScale 0.4` 로 0.16~0.24 world 까지 줄어 정확히 그 증상이 난다.
   벤더 `renderMode` 가 `Mesh`(Quad)인 것은 **원인이 아니다** — Billboard 로 바꿔도 같았다.
2. **`gravityModifier` 는 배율이지 가속도가 아니다** (`× Physics.gravity 9.81`).
   `-0.2` 는 초당 약 2m 로 솟는다는 뜻이라, 수명 3초짜리 연기가 **잔해를 떠나 하늘로 간다**
   (첫 렌더에서 «공중에 뜬 돌덩이»로 보였던 것의 정체). 잔불은 `-0.09..-0.06` 이 맞다.
3. **잔불이 옆으로 흘러 잔해를 벗어나던 것**은 점 스피어에서 방사로 나가는 `startSpeed` 의
   수평 성분이다. 속도를 `0.02~0.08` 로 죽이고 **상승은 gravity 가 갖게** 한다 —
   상태 VFX 통합 때 이미 겪은 「꼬리 휨 원인 ①」과 같은 처방이다.

**최종 저작값**: 버스트 `size 4.8~8.4 · lifetime 0.6~1.0 · speed 1.0~2.2 · gravity -0.20~-0.10 ·
burst 9 · maxParticles 16 · stopAction Destroy` · 잔불 `size 3.9~6.5 · lifetime 1.8~2.6 ·
speed 0.02~0.08 · gravity -0.09~-0.06 · rate 2.2 · maxParticles 12`.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0 · EditMode 2 lane 신규 실패 0 (unit 0 참조)
- [x] 오프스크린 — 포신이 떨어져 바닥에 눕는다(`barrelWorldY = 0.020 = groundY + groundLift`).
      **몸통은 포신 없이 남는다**
- [x] 낙하 중 포신 회전이 원복되지 않는다 — 회전이 잔해 틱에만 쓰인다
- [x] 연기 버스트가 프랍 크기에 맞게 터진다 — 위 §함정 1·2 를 잡은 뒤 확정
- [x] 정렬 대역 결정 = `StructureWreckOrder = -2`(유닛 아래). 근거는 상수 주석에.
      실제 적용값은 프리팹 `ParticleSystemRenderer.sortingOrder`
- [x] 잔해 컨테이너가 `_structureViews` 에 등록돼 기존 스윕이 가져간다(새 정리 경로 0)
- [ ] **라이브 Play 체감(사용자)** — 유닛이 오가는 실제 판에서 −2 대역이 맞는지
