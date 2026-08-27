using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // skill-layer-migration unit 7e — **조준 사양은 저작이 선언한다.**
    //
    // 「두 칸을 받는다」가 예전엔 `effect == Portal` 로 이름표에서 나왔다. 저작 필드로
    // 옮기면서 생긴 위험이 하나 있다 — **필드를 안 켜면 조용히 한 칸 스킬이 된다.**
    // 포탈은 입구만 찍히고 출구를 못 찍어 카드가 그냥 안 먹는 것처럼 보인다.
    // 컴파일러도 런타임도 그 연결을 안 잡으므로 여기서 잡는다.
    public class ActiveSkillAimingTests
    {
        // ⚠ **`Resources.FindObjectsOfTypeAll` 을 쓰지 않는다.** 그건 «이미 메모리에
        // 로드된» 에셋만 본다 — 도메인이 새로 뜬 직후(에디터 재연결·스크립트 재컴파일)
        // 처럼 아직 아무것도 안 불러온 상태에서는 **빈 배열**이 나와 전제 단언이 깨진다.
        // 실제로 그렇게 빨개졌고, 이 레인의 다른 테스트는 전부 `AssetDatabase` 를 쓴다
        // (`MalphiteKnockupAuthoringTests` 는 그 함정을 주석에 적어 두기까지 했다).
        private static SkillData[] All()
            => UnityEditor.AssetDatabase.FindAssets("t:SkillData")
                        .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(
                            UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                        .Where(s => s != null && !string.IsNullOrEmpty(s.id))
                        .ToArray();

        [Test]
        public void TwoTileAiming_IsAuthored_OnExactlyThePortal()
        {
            var all = All();
            Assert.IsNotEmpty(all, "전제: 액티브 스킬 저작이 있어야 이 그물이 성립한다");

            foreach (var s in all)
            {
                bool isPortalEffect = s.effect == SkillEffectType.Portal;
                Assert.AreEqual(isPortalEffect, s.NeedsTwoTiles,
                    $"'{s.id}' — 이름표({s.effect})와 조준 사양(두 칸={s.NeedsTwoTiles})이 어긋난다. "
                    + "두 축은 서로 독립이지만 **오늘 저작에서는** 포탈 하나만 두 칸이다. "
                    + "새 두 칸 스킬을 만든다면 이 단언을 그 사실로 갱신하라 — "
                    + "그때가 「이름표로 조준을 알던 시절」이 정말 끝나는 지점이다.");
            }
        }
    }
}
