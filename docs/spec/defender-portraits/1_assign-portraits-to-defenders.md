# 1 — 방어 SO 에 포트레이트 배정

## 목적

README "포트레이트 배정표"대로 16개 방어 유닛 SO 의 `portrait` 필드에 해당
Sprite 를 할당한다. (unit 0 의 필드/Sprite import 선행 필요.)

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_*.asset` (16개) — `portrait` 참조 설정.

## 구현

배정표(README)의 id → 원본 파일 매핑대로 각 SO 의 `portrait` 에 Sprite 를 연결한다.

| asset | id | 배정 스프라이트 |
|---|---|---|
| Defender_Archer | archer | bishoujo archer |
| Defender_Ranger | ranger | bishoujo ranger |
| Defender_Piercer | piercer | bishoujo piercer |
| Defender_Bastion | bastion | bishoujo bastion |
| Defender_Guardian | guardian | bishoujo guardian |
| Defender_Healer | healer | bishoujo healer |
| Defender_FireCaster | fire_caster | bishoujo fire_caster |
| Defender_IceCaster | ice_caster | bishoujo ice_caster |
| Defender_PoisonCaster | poison_caster | bishoujo poison_caster |
| Defender_BlockingCaster | blocking_caster | bishoujo blocking_caster |
| Defender_Scout | scout | modern scout |
| Defender_Sniper | sniper | modern sniper |
| Defender_Marksman | marksman | modern marksman |
| Defender_Artillery | artillery | modern artillery |
| Defender_Cannon | cannon | modern cannon |
| Defender_Bruiser | bruiser | modern fighter |

방법: unityMCP `manage_scriptable_object` 로 각 SO 의 `portrait` 프로퍼티에 Sprite
에셋 경로/GUID 를 세팅. 또는 일회용 MenuItem 에디터 스크립트로
`AssetDatabase.LoadAssetAtPath<Sprite>(path)` 후 `so.portrait = sprite;
EditorUtility.SetDirty(so); AssetDatabase.SaveAssets();` 일괄 처리.

Sprite 는 Single import 이므로 텍스처 경로에서 `LoadAssetAtPath<Sprite>` 로 로드된다.

## 완료 기준

- 16개 방어 SO 각각의 인스펙터 `Portrait` 슬롯에 올바른 클래스/스타일 스프라이트가
  배정되어 있다.
- `DefenderCatalog.asset` 의 units 배열 순서/참조는 변경되지 않는다(포트레이트만 추가).
- 컴파일/콘솔 클린.

---
완료 확인: 2026-07-08 · 커밋 c423be4c
