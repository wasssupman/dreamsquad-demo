using UnityEngine;
using Wassup.Core;
using Wassup.Core.Api;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-card-visibility unit 2 — 로그인 직후 저장 덱에서 숨김 카드를 장착
    // 해제한다. LoginPanelView.onSignedIn 은 모든 진입 경로(로그인 성공/게스트 skip/
    // 자동 로그인)가 지나는 단일 seam — LoginAutoImport 가 쓰는 그 지점이다.
    //
    // LoginAutoImport 와 달리 dev 게이트를 걸지 않는다: 자동 import 는 dev API 호출이라
    // 릴리즈에서 꺼야 하지만, 이 정리는 빌드에 박힌 visible 값만 읽는 로컬 연산이라
    // 릴리즈에서도 돌아야 한다.
    //
    // 정리 결과 덱이 정원 미달이 되는 것은 의도된 동작이다 — LoadoutGate 가 START 를
    // 막고 덱 페이지로 유도한다. 임의의 카드로 자동 보충하지 않는다.
    //
    // LoginAutoImport 가 dev 에서 비동기로 시트를 당기므로, 이번 로그인의 판정은 직전
    // 빌드/에셋 값 기준이다. 방금 시트에서 숨긴 카드는 다음 진입에 정리된다(그쪽의
    // non-blocking 설계와 같은 성질).
    //
    // UI 에 두는 이유는 LoginPanelView 의존 때문 — 기존 의존 방향이 UI -> Core 다.
    public class HiddenCardDeckPruner : MonoBehaviour
    {
        [SerializeField] private LoginPanelView loginPanel;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;

        private void Awake()
        {
            if (loginPanel != null) loginPanel.onSignedIn += OnSignedIn;
        }

        // 이미 로그인된 채 로비에 들어온 경로(세션 중 재방문 / 에디터
        // DisableDomainReload 로 static UserSession 이 살아남은 Play 재시작)에서는
        // LoginPanelView.Start 가 onSignedIn 을 쏘지 않는다 → 구독만으로는 안 돈다.
        // LoginAutoImport 와 같은 보강. 멱등하므로 양쪽이 다 울려도 무해하다.
        private void Start()
        {
            if (UserSession.IsSignedIn) OnSignedIn();
        }

        private void OnDestroy()
        {
            if (loginPanel != null) loginPanel.onSignedIn -= OnSignedIn;
        }

        // onSignedIn 은 앱 세션에서 여러 번 발화할 수 있다(자동 로그인 후 SKIP 등).
        // 멱등한 연산이라 별도 1회 가드를 두지 않는다 — 두 번째 호출은 제거 0 → 저장 없음.
        private void OnSignedIn()
        {
            var profile = profileSO != null ? profileSO.profile : null;
            if (profile == null || cardCatalog == null) return;

            // 살아 있는 인스턴스를 그대로 수정한 뒤 저장한다. 새 PlayerProfile 을 만들어
            // 저장하면 스쿼드 등 다른 섹션이 통째로 날아간다.
            int removed = DeckPrune.RemoveHiddenCards(profile, cardCatalog);
            if (removed <= 0) return;

            ProfileStore.Save(profile);
            Debug.Log($"[HiddenCardPrune] 숨김 카드 {removed}장을 저장 덱에서 해제했다.", this);
        }
    }
}
