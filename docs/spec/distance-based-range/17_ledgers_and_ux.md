# 17 — 원장 2장 · UX 문구 · 전복 인벤토리 (문서)

> 외부 세션 확정 9~11 의 문서 이행. **B트랙(아트 발주) 선행 과제.**

## 목적

판정 몸의 정본 표(원장)와, 게임이 의식적으로 채택/기각한 관습의 기록을 만든다.

## 변경 대상 (전부 신설 문서)

- `docs/blueprint/ledger-ally-bodies.md` — 아군 원장: footprint → 반경(min(W,H)/2) · 피벗
  (점유 박스 하단 중앙 · 루트모션 금지 — 런지·넉백은 시각 오프셋만) · 소켓.
  「그림자는 UI 다, 연출이 아니다」 발주 문구와 UX 확정 문구 3종 포함.
- `docs/blueprint/ledger-enemy-bodies.md` — 적 원장: 티어 → 반경 · 접지폭(≈2r) · 소켓.
  유효 사거리(R+targetR) 티어 표 포함 — HTK 워크벤치는 repo 에 없으므로, 신설되는 날 이 표를
  소스로 쓴다는 포인터만 남긴다.
- `docs/blueprint/convention-inventory.md` — 전복 인벤토리 신설. 첫 등재: 「저지(블로킹) 관습
  미채택 — 의식적 선택」(Arknights 직관 충돌 계열) + 상속 조항(블로킹 도입 시 블로킹 반경 =
  bodyRadius 강제).

## 완료 기준

- [ ] 문서 3건 존재 + CLAUDE.md 또는 spec README 에서 도달 가능한 링크.
- [ ] 코드 변경 0.
