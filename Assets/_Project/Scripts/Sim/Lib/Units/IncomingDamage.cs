namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-D — 이번 프레임에 들어온 피해 1건. 구 `IncomingDamage` 이식.
    ///
    /// **18-D 가 여는 이유**: `DotApplySystem` 이 생산자다. **소비자(`DamageApplicationSystem`)는
    /// 18-G** 가 가져오고 킬 귀속 규칙도 거기 소유다.
    ///
    /// `source` 는 킬 귀속용 공격자다. 공격·투사체는 채우고 **DoT·배치·환경 피해는 비운다**
    /// (`SimEntityId.Null` = 미귀속). 18-G 의 정산이 킬 프레임에 `source` 非Null 중 최대
    /// `amount` 를 killer 로 뽑는다.
    ///
    /// ⚠ **버퍼 부재가 게이트다.** 구 sim 의 `DamageApplicationSystem` 은 이 버퍼의 **부재**를
    /// 보고(빈 버퍼는 통과), `DotApplySystem` 은 이 버퍼가 **있는 대상만** 틱한다. 조회가
    /// 자동 생성하지 않는 것(`SimWorld.GetBuffer`)이 그 계약을 지킨다.
    /// </summary>
    public struct IncomingDamage
    {
        public float amount;
        public SimEntityId source;
    }
}
