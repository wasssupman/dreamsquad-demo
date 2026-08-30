# 빌보드 카메라 추종 (billboard-camera-follow)

> 상태: 설계 승인 대기 (2026-08-28 착수)
> 선행: `docs/spec/tilted-billboard/` units 7~9(블롭 접지) 완료. 이유는 아래 «선행 이유».

## 상위 목표

캐릭터·프랍 빌보드가 **카메라 pitch 를 추종**하게 만든다. 지금은 45° 고정이라
런타임에 pitch 를 바꿔도 판 위의 것들이 따라 서지 않는다.

## 검증 질문

> **카메라 pitch 를 바꿨을 때 캐릭터와 프랍이 함께 따라 서는가?**

`tilted-billboard` 의 질문("메인 씬이 2.5D 빌보드 룩으로 보이는가")과 다르다.
그 spec 은 **pitch 55° 고정** 세계에서 답을 냈고 결론이 「φ = 45° 고정」이었다.
이 spec 은 **pitch 가 변하는** 세계에서 같은 질문을 다시 묻는다 — 그래서 별도 spec 이다.

## 현행 상태 (실측 2026-08-28)

- **각도만 데이터고 모드는 코드다.** `BillboardMode.Tilted` 가 4곳에 하드코딩 —
  `SpineUnitView.cs:106` · `QuadUnitView.cs:67` · `DefenderDragPlacementController.cs:1621·1721`.
  `Billboard.mode` SerializeField 는 런타임 `AddComponent` 직후 `Setup` 이 덮어써 **죽은 필드**다.
  씬/프리팹에 박힌 `Billboard` 는 `SpriteCharacter.prefab`(확인용, 라이브 경로 아님) 하나뿐.
- **Tilted 는 카메라를 읽지 않는다.** `BillboardRotation.Compute` 의 `Facing.Tilted` = `Euler(tilt,0,0)`,
  카메라 인자를 받지도 않는다(`Billboard.cs:38` 이 `EnsureCamera` 를 건너뛴다).
- **각도 출처**: `BattleBridge.tilemapBillboardTilt = 45`(씬 `BattleScene.unity:4684`).
  주석이 «카메라 pitch × 0.7~0.8, pitch 55 → ≈45, 실측 튜닝» 이라고 근거를 남겼다 — 즉 **수동 동기화**다.
- **프랍은 이미 데이터다.** `PropData.billboardMode` — 53개 중 Tilted 39 / None 11 / FullCamera 3.
  Tilted 프랍의 저작 각은 38·40·42·**45(23개)**·48·50, 거리 보정은 `[28, 62]` 클램프.
  이 보정 산식은 `tilt = base + (elev − camPitch)×0.78` 이라 **설계상 camPitch 를 상쇄**한다(추종의 반대).
- **카메라 yaw 는 항상 0** (`CameraFramingMath.cs:61` — `Euler(pitchDeg, 0, 0)`).
  따라서 `Facing.Camera`(Full)는 사실상 «pitch 추종»과 동치다.

## 선행 이유 (블롭이 먼저인 이유)

빌보드 모드가 바뀌면 캐릭터의 **렌더 하단**이 움직인다. 블롭은 그 하단을 입력으로 쓰므로
(tilted-billboard unit 7 계약), 블롭이 −0.65 어긋난 상태로 모드를 바꾸면
「그림자가 이상한 게 모드 탓인지 평면 탓인지」 구분할 수 없다.
또 unit 7~9 의 회귀 가드(«0.19 스테이지 전후 동일»)가 기준선을 잃는다.

역으로, unit 7 의 카메라 투영 모델은 **모드 변경을 흡수하도록** 설계됐다 — 하단이 어디로 가든
카메라에서 본 접지점을 다시 푼다. 그래서 순서만 지키면 이 spec 은 블롭을 건드리지 않는다.

## 작업 단위

| # | 문서 | 작업 구분 | 목적 |
|---|---|---|---|
| 0 | `0_mode_authoring.md` | 저작면 | 모드 하드코딩 4곳 → 데이터 + 인스펙터 라이브 토글. **방향 중립** — 판정을 싸게 만든다 |
| 1 | `1_pitch_follow.md` | 결정 구현 | unit 0 육안 결과로 방식 확정: `Full` 채택 / `TiltedDynamic`(pitch×비율) 구현 / 현행 유지 |
| 2 | `2_prop_alignment.md` | 데이터 | 프랍 39종을 같은 결정에 정합. 거리 보정 계수의 부호 재해석 포함 |

**파생 영향은 별도 unit 으로 세우지 않는다** — 근거를 확인해보니 대부분 성립하지 않는다:
정렬은 `_simWorld`(sim 좌표)에서 계산돼 회전과 무관하고(`SpineUnitView.cs:294-298`),
`UseRealShadows` 는 0 이며, 무기 궤적 리그는 자기 정렬 대역을 소유한다.
남는 확인 항목(오버헤드 앵커·픽 사각형)은 **unit 1 의 완료 기준 회귀 체크리스트**로 흡수한다.

unit 1 은 **unit 0 의 Play 결과를 받아 쓴다.** 지금 문서를 미리 쓰면 결정을 추측으로 박는 꼴이라
unit 0 이 끝난 뒤 작성한다.

## Feature-wide 계약

1. **모드는 데이터다.** `tilted-billboard` 계약 «틸트 각은 데이터에서 온다(하드코딩 금지)» 의 미이행분을 갚는다 — 각도만이 아니라 모드도.
2. **`tilted-billboard` 의 «캐릭터 φ = 45° 고정» 계약을 이 spec 이 승계·개정한다.** 그 spec 은 pitch 고정 시절의 역사로 두고 되돌려 쓰지 않는다.
3. **캐릭터와 프랍은 같은 결정을 공유한다.** 한쪽만 추종하면 판 위에서 두 레이어가 서로 다른 각으로 서서 지금보다 나빠진다.
4. **블롭은 이 spec 이 건드리지 않는다.** `tilted-billboard` unit 7~9 가 소유하고, 카메라 투영이 모드 변경을 흡수한다.
5. **회귀 기준은 현행 pitch 다.** 전투 pitch 를 유지한 채 모드만 바꿨을 때 화면이 크게 달라지면 비율이 틀린 것이다.
6. **`Full` 은 카메라 roll 까지 복사한다** — 셰이크·브리딩이 캐릭터 회전에 그대로 실린다. 진짜 빌보드의 정상 동작이지만 룩이 달라지므로 unit 1 판정에 포함한다.

## 후속 후보

- **elevation(위치) 기반 동적 틸트** — `tilted-billboard` 후속 후보의 원래 항목. 유닛마다 각이 달라져 이동 캐릭터가 휘청인다는 사유로 배제됐고, 그 사유는 **pitch 파생에는 걸리지 않지만 elevation 파생에는 그대로 유효**하다. 배경 프랍은 unit 6 의 스폰 bake 로 이미 실현돼 있다.
- **`SpriteCharacter.prefab` 정리** — 라이브 경로가 아닌 확인용 프리팹에 `Billboard`(mode 3 / tilt 45)가 박혀 있다. 저작면이 열리면 같이 정리할지 판단.
