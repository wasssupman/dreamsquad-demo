# 4a — 사거리 술어를 몸 기준으로

## 목적
`AttackReach` 본체를 몸 기준 간격으로 교체한다. **자를 바꾸는 첫 커밋**이고, 광역·스킬·히스테리시스는
뒤 커밋이 맡는다(4b~4d). 여기까지만으로 컴파일과 판이 성립해야 한다.

## 변경 대상
- `Skills/SkillMath.cs` — **술어 본체 신설**(계약 8: 본체는 엔진 무참조 쪽에)
- `Combat/AttackReach.cs` — 몸통 비우고 **위임만**. `CellSlackTiles` 은퇴
- 호출부 8곳: `AttackSystem:594·741·879·925` · `EnemyAiStateSystem:176·200` · `PatrolAreaMath:171-172`
- `Tests/EditMode/AttackReachTests.cs`

## 구현
```
d  = length(max(|Δ| − (0.5 + 대상반폭), 0))
안 ⟺  lengthsq(v) ≤ (사거리 + 대상반경)²        ← sqrt 금지
```
- 공격자 반폭 `0.5` 는 **코어 `const`**(`SelfHalfWidthTiles`) — 계약 3.
- **`float2`/`math` 를 그대로 쓴다.** `Wassup.Skills` 는 `SkillCone`·`ISkillContext` 로 **이미**
  `Unity.Mathematics` 에 묶여 있어 한계 비용이 0 이다. primitive 로 풀면 `AttackReach` 쪽 변환만 는다.
  (그 패키지를 netstandard 로 어떻게 옮길지 — vendoring / 재구현 — 는 M1 의 결정이지 이 spec 의 것이 아니다.)
- `AttackReach` 는 `int2`/`float3` ↔ primitive 변환과 `tileSize` 곱만 남긴다.

## 완료 기준

- [ ] **`AttackSystem` 다중타격 2번째 이후 대상 × 순찰병 조합을 실제로 검증한다.**
      unit 1 이 그 지점(`:1551`)의 판정을 좁혔지만 **이 unit 전까지는 무해**하다 — 자가 안 바뀌어
      결과가 같기 때문이다. **틀렸을 때 라이브가 되는 시점이 정확히 여기다**: 술어를 몸 거리로
      돌리는 순간 「내가 때릴 수 있는 적」의 정의가 그 경로에서만 갈린다.
      코퍼스 `summoner` 는 이 조합의 **반대 방향만** 연다(순찰병이 공격자로 16회 · 순찰병이
      피해를 받은 기록 0건). 그러니 골든에 기대지 말고 **직접 확인**한다 — 순찰병이 적의
      다중타격 사거리 안에 서는 상황을 만들고 2차 대상 선정이 1차와 같은 정의를 쓰는지 본다.
      (unit 1 정정 1 · unit 3 이 대상 반경으로 이 입력을 한 번 더 흔든다.)
- [ ] `AttackReachTests` 의 대각 단언 4건이 **뒤집힌다 — 정상이다.** 이름·주석을 새 계약으로 갱신
      (특히 `WorldGate_IsChebyshevToo_SoDiagonalIsNotPenalized`).
- [ ] unit 0 안전망 **초록 유지**(상대 동치성만 보므로 자가 바뀌어도 초록이어야 한다).
- [ ] 골든 7건 red — 이 시점부터 골든은 오라클이 아니라 관측 도구다(계약 13).
