# 1. Holo Frame & Card Widget

## 목적

카드 1장을 "프레임(출신 표시) + 아트 + 이름"의 독립 위젯으로 분리하고, Lucid 금색 / Rim 적색 홀로그램 프레임을 기존 자산 재사용으로 만든다. 내 덱 10장은 프레임 없음.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftCardWidget.cs` (신규 — GiftPhaseView 내부 struct 승격)
- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` (위젯 생성/바인딩 위임)
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` + `GiftConfig_Default.asset` (프레임 색·크기·포일 파라미터)

## 구현

1. `GiftCardWidget` — 코드빌드 계층: `FrameRoot(Image, 카드보다 사방 +border)` 위에 `Art(Image)` + `Name(TMP)`. 루트 RectTransform 하나만 안무 대상(프레임·아트가 함께 움직임).
2. 프레임 비주얼 = `UiRoundedSprite` 절차 라운드 사각 스프라이트 + `DraftCardFoil_UI` 셰이더 머티리얼 인스턴스. 머티리얼은 위젯 생성 시 코드에서 `new Material(shader)` 2종(Lucid/Rim)만 만들어 공유 — 카드당 인스턴스 금지. 파괴 시 Destroy.
3. 틴트 전략: `_Color` 금색/적색 + `_Intensity`/`_Speed`/`_HueShift` 는 GiftConfig 필드. 무지개 시머가 틴트를 삼켜 금/적 구분이 안 살면 그때만 `GiftCardFoil_UI.shader` 변형(틴트 가중 blend 한 줄)을 추가한다 — 선제 셰이더 제작 금지.
4. `SetOrigin(GiftOrigin)` — None/Lucid/Rim. None 이면 FrameRoot 비활성(내 덱 10장). 판정은 호출측(unit 2)이 `GiftAddedEntryIds`+`GiftKind` 로 수행해 넘긴다. 위젯은 카드 데이터로 출신을 추론하지 않는다(계약 2).
5. **뒷면 + 플립 지원**(계약 10-ⓑ): `BackRoot(Image)` — 프레임 색 단색 + 절차 문양(UiRoundedSprite 겹침 2~3장, 아트 에셋 0). `SetFace(bool front)` 로 앞/뒷면 전환. 플립 자체(scale.x 1→0 스왑 0→1)는 안무(unit 2) 소관 — 위젯은 face 상태만 소유.
6. 카드 크기 상향: 130×180 → 기본 180×252 (`GiftConfig.cardSize`). 이름 폰트/배치도 비례 조정.

## 완료 기준

- 컴파일 클린. 임시 하네스(에디터 Play 또는 오프스크린 렌더)로 3종 위젯(무프레임/금/적) 스크린샷 — 프레임만으로 즉시 구분 + 포일 시머 동작 확인.
- 머티리얼 공유 2개 유지(프로파일러/로그로 카드당 인스턴스 없음 확인).
- Canvas additionalShaderChannels 요구사항 확인(DraftCardFoil 은 uv0 만 사용 — 추가 채널 불필요 예상, 실측 확인).
