# 5 — Handoff Summary

## Commit

- `1434911` `feat(combat-feedback): 데미지 숫자 팝업 + 라이브 점수 HUD`
  (데미지/점수 두 spec 이 공유 파일을 함께 건드려 단일 커밋. score-hud spec 과 묶임)

## Implemented

- 방어유닛이 적(`AttackUnitTag`)을 때릴 때 적 머리 위에 데미지 숫자 팝업.
- 펀치 스케일-인 → 위로 드리프트 → 페이드, 카메라 빌보드.
- 데미지 클수록 폰트 크게 + 색 흰→노랑→주황→빨강, 큰 히트 펀치 증폭.
- 적만 표시(디펜더 피격 제외). DoT/다단 히트는 프레임당 합산 1회.
- Bangers SDF(OFL) 폰트 + 아웃라인 머티리얼. 팝업 풀링으로 GC 억제.
- 사용자 요청으로 폰트 크기 1.3배(5.2~11.7).

## Key Files

- `Assets/_Project/Scripts/Battle/Units/DamageNumberEvent.cs` / `DamageNumberEventsSingleton.cs` — 채널 #15.
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — enqueue(적 + totalDamage>0).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 채널 생성/해제 + `DrainDamageNumberEvents`.
- `Assets/_Project/Scripts/Presentation/DamageNumberStyle.cs` / `DamageNumberView.cs` / `DamageNumberPool.cs` / `DamageNumberSpawner.cs`.
- `Assets/_Project/Fonts/Bangers SDF.asset` + `DamageNumber Outline Mat.mat` + `Bangers-Regular.ttf`(+OFL).
- `Assets/_Project/VFX/DamageNumber_Popup.prefab` — TMP(3D) + Bangers + DamageNumberView.
- BattleScene: VfxSpawner 오브젝트에 `DamageNumberSpawner`, `BattleBridge.damageNumberSpawner` 연결.

## Verified

- compile: CS/Burst 에러 0 (UnityMCP force refresh + read_console).
- Play(Squad, 사용자 2026-06-05): 데미지 숫자 표시·연출 반복 확인, 1.3배 크기 적용.

## Notes

- enqueue 는 Units 맥락(`DamageApplicationSystem`) 한 곳만 — 맥락 경계 준수.
- 모든 연출 수치는 `DamageNumberSpawner`(`DamageNumberStyle`) 직렬화 — 하드코딩 없음.
- **씬 직렬화값이 런타임에 코드 기본값보다 우선**. 크기 변경 시 BattleScene 의 `DamageNumberSpawner.style` 값과 코드 기본값 둘 다 갱신할 것.
- 페이스 알파만 페이드(아웃라인 머티리얼 고정) — 0.8s 단발이라 충분.

## Follow-up

- 세로 그라데이션/전체 알파 페이드(아웃라인 포함), 킬 위치 "+점수" 플로팅.
- 디펜더 피격 데미지(색 구분), 데미지 타입별 색 — 현재 범위 밖.
