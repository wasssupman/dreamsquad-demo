# 1 — DefenderUnitData 컷신 필드 + Ranger 할당

## 목적

유닛→컷신 프레임 매핑을 데이터로 둔다. 프레임 없는 유닛은 자동 skip.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- Modify(asset): `Assets/_Project/Data/Defenders/Defender_Ranger.asset`

## 구현

- `DefenderUnitData` "Deployment Presentation" 헤더 근처에 추가:
  ```csharp
  [Header("Deploy Cutscene")]
  public Sprite[] deployCutsceneFrames;      // 비어 있으면 컷신 없음(skip)
  public float deployCutsceneFps = 24f;      // 플립북 재생 속도
  public float deployCutsceneScale = 1f;     // 유닛별 표시 배율(재생기 displayScale 에 곱)
  public Vector2 deployCutsceneOffset;       // 유닛별 도착 위치 오프셋(px, 공유 baseline 에 더함)
  ```
- 순서/기본값은 기존 필드 뒤에 누적(직렬화 순서 안정). fps 기본 24.
- `Defender_Ranger.asset` 에 `Ranger_001..033` 33장을 `deployCutsceneFrames` 에
  순서대로 할당(001→033). fps=24 유지.
- 동일 방식으로 `Defender_Archer.asset` 에 `Archer_001..049` 49장 할당(fps=24).
  Archer 원본은 이미 640×360 이라 축소 불필요, 역순 리넘버 + 누끼만 적용.
  Archer 는 `deployCutsceneScale=1.5`(Ranger 대비 1.5배) + `deployCutsceneOffset=(-150,0)`
  (도착 baseline -100 에서 왼쪽 150 더 → -250). Ranger 는 두 필드 미기재 → 기본값(scale 1,
  offset 0 → 도착 -100 유지).
  - 에셋 할당은 UnityMCP(`manage_scriptable_object` 또는 일회용 MenuItem)로 자동화.
    스프라이트 GUID/fileID 를 정확히 넣는다.

## 완료 기준

- 컴파일 통과(`read_console` clean).
- `Defender_Ranger` 인스펙터에 33 프레임이 001→033 순으로 채워지고 fps=24.
- 다른 Defender 에셋은 `deployCutsceneFrames` 비어 있음(기존 동작 불변).

_확인: 2026-07-14 — Ranger(33장)·Archer(49장, scale 1.5, offset -150) 할당, 컴파일 클린._
