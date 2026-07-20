# 2 — Bridge 이중 경로

## 목적

기존 체력 표현을 삭제하지 않고 Legacy/UnifiedOverhead를 즉시 전환 가능한 seam을 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Data/UnitHealthPresentationMode.cs`
- `Assets/_Project/Scripts/Presentation/DcIconStripSpawner.cs`

## 구현

- enum은 두 값만 둔다. 동시 표시는 금지.
- Legacy에서는 기존 적 틴트/피격바/타일게이지/드림캐쳐 스트립 유지.
- Unified에서는 새 Layer만 갱신하고 기존 네 표현을 억제한다.
- Bridge 밖 EntityManager 접근은 추가하지 않는다.

## 완료 기준

- mode 전환 시 중복 표시가 없고 양쪽 경로가 모두 컴파일·동작한다.
