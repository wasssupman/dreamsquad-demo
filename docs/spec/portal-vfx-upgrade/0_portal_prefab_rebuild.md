# 0 — 포탈 물빔 제거 + 스월 지속화 (원안 유지)

## 목적

① 포탈 입구/출구를 잇던 `PixPlays_WaterBeam`(WaterBeam 재활용 어거지) 제거. ② 양끝 스월이 **원샷 스플래시**(t=0 버스트, 수명 0.6~2s, 사이클 5s → 5초 중 대부분 빈 화면)였던 것을 **포탈 지속시간 내내 유지**되게 지속화. 시각 소재는 원안(WaterAOE) 그대로.

## 변경 대상

- `Assets/_Project/VFX/Portal_SKELETON.prefab` — ① `LinkBeam` 자식(중첩 프리팹 `PixPlays_WaterBeam`) 삭제 ② Entry/Exit 의 연속 계열 4系(Body/Foam/Splash(1)/Drops) **인스턴스 오버라이드**: `loop=true` + `duration≈startLifetime`(0.6/2/0.8/0.7 — 버스트 연속 재발화) + SourceDrops 버스트 60→15(모바일 예산). Flash 계열은 캐스트 순간 액센트로 원샷 유지. 공용 PixPlays 에셋 무접촉.

## 구현

`PrefabUtility.LoadPrefabContents` → 편집 → `SaveAsPrefabAsset`(같은 경로, guid 불변 — 씬 재배선 불필요). 코드 무변경: `SpawnPortal` 의 LineRenderer 루프·PixPlays 빔 핸들러는 대상 없으면 no-op, `LocationVfx.Play` 는 duration 을 무시(원인)하지만 root Destroy(durationSec)가 수명을 정리하므로 루프 시스템이 알아서 잘린다.

## 완료 기준

- Play: Portal 캐스트 → 스월이 **8초(durationSec) 내내** 표시, 연결선 없음. 텔레포트 무회귀.
- 프리팹 diff = WaterBeam 블록 삭제 + 4系 오버라이드만.

확인 2026-07-06 — 빔: diff 실측 삭제 64줄 전부 WaterBeam 블록. 지속화: 에디트 모드 Simulate 샘플 t=1.7s/3.3s 파티클 23/19(수정 전 0 — 사이클 중간 공백 증명→해소). 파티클 예산 ~23/포탈측. 콘솔 클린. 사용자 육안(라이브 8초 유지)은 일반 플레이 확인.
