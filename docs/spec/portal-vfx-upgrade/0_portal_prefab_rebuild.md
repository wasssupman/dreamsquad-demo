# 0 — 포탈 물빔 연결선 제거 (원안 스월 유지)

## 목적

포탈 입구/출구를 잇던 `PixPlays_WaterBeam`(WaterBeam 재활용 어거지)을 제거한다. 양끝 스월(WaterAOE 재활용)은 원안 그대로 둔다.

## 변경 대상

- `Assets/_Project/VFX/Portal_SKELETON.prefab` — `LinkBeam` 자식(중첩 프리팹 `PixPlays_WaterBeam`) 삭제만

## 구현

`PrefabUtility.LoadPrefabContents` → LinkBeam 자식 제거 → `SaveAsPrefabAsset`(같은 경로, guid 불변). 코드/씬 무접촉 — `SpawnPortal` 의 LineRenderer 배선 루프와 PixPlays 빔 핸들러는 대상이 없으면 no-op.

## 완료 기준

- Play: Portal 캐스트 → 입구/출구 스월 원안 그대로 표시, **연결선 없음**. 텔레포트 게임플레이 무회귀.
- git diff = WaterBeam 블록 삭제만(추가 0).

확인 2026-07-06 — diff 실측: 삭제 64줄 전부 `PixPlays_WaterBeam` 중첩 프리팹 블록, Entry/Exit 무접촉(자식 1개씩 유지), 추가 0. 인스턴스 구조 검증(LinkBeam kids=0). 콘솔 클린. 스월 육안은 원안 무변경이라 일반 플레이에서 자연 확인.
