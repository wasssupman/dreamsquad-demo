using System.Collections.Generic;

namespace Wassup.Skills
{
    // skill-layer-foundation unit 3 — skillId → concrete.
    //
    // 디스패처가 이걸 들고 있고, 감지측(Burst)은 슬롯에 베이크된 **숫자**만 안다.
    // 그 분리가 계약 12 다 — Burst 코드는 managed 레지스트리를 읽을 수 없다.
    //
    // **fail-closed.** 미등록 skillId 는 조용한 no-op 이 아니라 loud 거절이다.
    // 선례: `DcApplicability` 의 `default → Unclassified` — 배선 누락을 침묵으로
    // 넘기면 「스킬이 안 나가는데 아무도 모르는」 상태가 된다.
    public sealed class SkillRegistry
    {
        // skill-layer-migration unit 8 — **이름이 바뀌었다**(구 `LegacyArmId`).
        //
        // 옛 이름은 「아직 이전 안 됐다」는 뜻이었고, 그 뜻이 참인 동안엔 이 값이 0 으로
        // 남은 슬롯이 곧 남은 숙제였다. 이전이 끝난 지금 이 값이 뜻하는 것은 **「스킬이
        // 아니다」** 다 — 숙제가 아니라 분류다. 오늘 여기 오는 것은 둘:
        //
        //   · `PlacementAura`  — **발동 규칙**이다. 지금 실행하는 게 아니라 앞으로 일어날
        //     배치에 적용될 규칙을 등록한다. 등록·조회·해지 세 시점이 있어 영수증이
        //     필요하고, 그것이 포트의 결함이 아니라 범주가 다르다는 신호다.
        //   · `HeavyStrike`    — **그 공격의 성질**이다. 「N번째 공격이 세진다」는 별도
        //     실행이 아니라 자기를 부른 사건 자체를 바꾼다. 그래서 감지가 공격 해결
        //     **앞**에서 값을 정해야 하는데, 스킬 seam 은 정의상 그 뒤다.
        //
        // ⚠ 둘의 이유가 **다르다**(시제 ↔ 자기참조). 「스킬이 아닌 것」을 한 이유로
        // 뭉뚱그리면 다음 후보를 잘못 분류한다.
        public const int NotRouted = 0;

        private readonly Dictionary<int, ISkill> _byId = new Dictionary<int, ISkill>();

        public void Register(ISkill skill)
        {
            if (skill == null)
                throw new System.ArgumentNullException(nameof(skill));
            if (skill.SkillId == NotRouted)
                throw new System.ArgumentException(
                    $"skillId {NotRouted} 은 «스킬 아님» 예약값이다 — concrete 가 쓸 수 없다.");
            if (_byId.ContainsKey(skill.SkillId))
                throw new System.ArgumentException(
                    $"skillId {skill.SkillId} 중복 등록: {_byId[skill.SkillId].GetType().Name} vs {skill.GetType().Name}");
            _byId.Add(skill.SkillId, skill);
        }

        // 미등록이면 false. 호출측이 loud 하게 알린다 — 여기서 던지지 않는 이유는
        // 드레인 루프 한복판이라 한 슬롯의 배선 실수가 그 프레임 전체를 죽이면 안 되기 때문이다.
        public bool TryGet(int skillId, out ISkill skill) => _byId.TryGetValue(skillId, out skill);

        public int Count => _byId.Count;

        // 완전성 테스트용 — 등록된 id 전수. 「저작된 스킬 전부가 등록됐나」를
        // 테스트가 물을 수 있어야 배선 누락이 런타임까지 안 간다.
        public IEnumerable<int> RegisteredIds => _byId.Keys;
    }
}
