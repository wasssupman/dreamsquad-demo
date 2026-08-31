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
- [ ] `AttackReachTests` 의 대각 단언 4건이 **뒤집힌다 — 정상이다.** 이름·주석을 새 계약으로 갱신
      (특히 `WorldGate_IsChebyshevToo_SoDiagonalIsNotPenalized`).
- [ ] unit 0 안전망 **초록 유지**(상대 동치성만 보므로 자가 바뀌어도 초록이어야 한다).
- [ ] 골든 7건 red — 이 시점부터 골든은 오라클이 아니라 관측 도구다(계약 13).
