using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Wassup.Data;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 24 — **표기 기준 상수가 로스터와 갈리지 않게.**
    //
    // `SkillMath.StandardDefenderBodyRadiusTiles`(1.0)는 착지 슬램 예고가 「이 칸에 유닛을 놓으면
    // 맞나」를 그릴 때 쓰는 **대상 항**이다. 근거는 「로스터에서 가장 흔한 footprint 폭이 2」인데,
    // 그건 **저작에서 오는 사실**이라 코드만 보면 참인지 알 수 없다.
    //
    // 이 파일이 없으면: 로스터가 3×2 위주로 바뀌어도 상수는 1.0 에 남고, 예고가 조용히
    // 좁아진다. 화면이 규칙을 틀리게 가르치는 그 결함이 정확히 unit 24 가 고친 것이다.
    // `RangeDisplayContractTests`(적 쪽 표기 기준 `StandardBodyRadiusTiles`)의 형제다 —
    // 저쪽은 합성 SO 기본값을 보고, 여기는 **실제 `.asset` 을 센다**.
    public class StandardDefenderBodyDriftTests
    {
        private static IEnumerable<DefenderUnitData> AllDefenders()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:DefenderUnitData"))
            {
                var so = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) yield return so;
            }
        }

        [Test]
        public void 표준_방어유닛_몸이_로스터_최빈값과_같다()
        {
            var widths = AllDefenders().Select(d => d.Footprint.x).ToList();
            Assert.IsNotEmpty(widths, "DefenderUnitData 를 하나도 못 찾았다 — 스캔이 깨졌나?");

            // 최빈 폭. 동률이면 **작은 쪽**이 이긴다(예고가 넓어지는 쪽으로 우연히 기울지 않게).
            int modal = widths.GroupBy(w => w)
                              .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                              .First().Key;
            var histogram = string.Join(", ", widths.GroupBy(w => w).OrderBy(g => g.Key)
                                                    .Select(g => $"{g.Key}칸폭×{g.Count()}종"));

            Assert.AreEqual(SkillMath.StandardDefenderBodyRadiusTiles, modal * 0.5f, 1e-6f,
                "착지 슬램 예고의 대상 항이 로스터와 갈렸다. 저작 분포: " + histogram + ". "
                + "상수를 최빈 폭/2 로 맞추고, 바뀐 예고 모양을 Play 로 다시 보라 — "
                + "이 값은 «화면이 가르치는 규칙»이라 조용히 바뀌면 안 된다.");
        }

        // ⚠ **몸은 저작 필드가 아니라 footprint 파생이다**(unit 12, 계약 1 rev 3).
        // 이 항등이 깨지면 위 테스트의 전제(`폭/2`)가 무너진다.
        [Test]
        public void 방어유닛_몸은_전부_가로의_절반이다()
        {
            foreach (var d in AllDefenders())
                Assert.AreEqual(d.Footprint.x * 0.5f, d.BodyRadiusTiles, 1e-6f,
                    $"{d.name}: 몸이 footprint 가로/2 파생이 아니다");
        }
    }
}
