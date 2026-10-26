# Draft Pixel UI

The Draft lobby uses the same teal, gold, cream and PixelMenuButton sprite as
GameStart, OwnedCards, Shop and DeckBuilding. The existing scene controller and
button handlers remain the source of behavior; no server or battle rules change.

## Layout

- Header: battle preparation title, Back to Start, wallet and existing gear.
- Left: current deck, live count/summary and a larger scrolling card list.
- Right: Wins/Losses and Last Battle, then the normal battle and Gold Dungeon buttons (legacy PVP/PVE component names remain internally). The existing
  conditional Start Draft button has a separate slot and is not forced visible.
- Bottom right: the existing live status text, including save/connection errors.
- Completed runs show a dimmed reward-chest modal, then the original server receipt. The result closes only with Confirm. The reward Canvas now stretches to its Draft parent instead of the authored100×100 host (2026-10-20); see [reward chest](run_reward_chest.md). Matchmaking and reconnect handling are unchanged.

The panels use proportional anchors. The CanvasScaler uses a 1920 x 1080
reference and height matching, consistent with the wallet and other game UI.
SafeAreaFitter keeps controls inside the safe area and the backdrop full bleed.

## Apply And Customize

The first script import outside Play applies version 1. Once applied, the version
marker prevents resetting the design on every compile or game launch. A loaded
scene with unsaved edits is marked dirty, not automatically saved or closed.

To deliberately restore the layout defaults:
`Tools > Project333 > Draft > Apply Pixel Draft Layout`.
Do not use the scene-creation tools merely to restyle this scene.

Under Canvas/DraftSceneController, edit HeaderPanel, RunStatusPanel, ControlPanel
and DeckPanel before Play. RuntimeDeckListScrollView and the other missing UI
elements are materialized in edit mode. Fonts, sizes, images and layout remain
scene-authored after applying the pixel layout. Keep Apply Runtime Draft UI
Layout disabled to avoid overwriting the deck panel's authored regions.

Labels describing wins/losses, last battle, deck contents, wallet, button state
and status remain dynamic. In particular, PVP's caption still switches to its
reconnect countdown, and completed runs still control Claim Rewards visibility.
The style builder does not start a run, make a pick, contact the server or claim
any rewards. All decorative images/text ignore raycasts.

DraftSceneVisualStyleTests covers preservation of button/wallet styling and the
legacy layout defaults. Full gameplay and mobile visual checks require Unity.

## Current behavior reference (2026-10-01)

Normal battle uses human-first matchmaking and a 20-second AI fallback. Gold Dungeon shows server-owned remaining attempts out of 3 for the current run started with 3 tickets. Midnight and relogin do not reset them; a new paid run has its own three attempts. Rewards remain separate without changing run score. Both reuse the persisted 33-card deck and support a 70-second reconnect grace. Notice/confirmation/remaining-time panels use [notice_ui.md](notice_ui.md); the current mode rules live in [battle_modes.md](battle_modes.md). These runtime policies do not require resetting authored layout.


## Editable UI before Play (2026-10-23)

The shared editor coordinator now prepares the current saved UI before Play, preserving existing scene objects and attached button components. See [editable_scene_layouts.md](editable_scene_layouts.md) for the common menu, dirty-scene saving policy and the distinction between editable UI templates and live account/battle data.

## 덱 목록 글자 크기 (2026-10-24)

현재 덱 목록은 두 씬 모두32pt다. 덱 빌딩의 카드 이름·오른쪽 비용 열은 DeckListText의 크기를 상속하고, 드래프트의 RuntimeCurrentDraftDeckText 및 원본/대체 목록도32pt로 맞췄다. 저장된 씬뿐 아니라 목록 생성·스타일 재적용 기본값도 동일하다. 제목·상태·요약·버튼의 글자 크기는 유지했다. 줄바꿈에 따른 높이·스크롤 범위와 길게 누르기는 기존 목록 레이아웃을 따른다.

추후 직접 조정할 때는 덱 빌딩 DeckListText, 드래프트 RuntimeCurrentDraftDeckText의 Text > Font Size를 수정한다. 명시적인 전체 스타일 재적용 도구는32pt 기본값을 적용한다.

검증: 격리 Unity6000.3.12f1 기존 DeckListCardHoldPreviewTests/DraftSceneVisualStyleTests40/40 통과. 두 씬의 변경은 목록 FontSize와 덱 빌딩 referenceFontSize뿐이며 fileID/참조/오브젝트 수 보존을 확인했다. 새 테스트·앱 빌드·설치·휴대폰 시각 확인·서버/DB 작업은 수행하지 않았다. 첫 검증은 사본의 누락된 DraftOverlaySceneBuilder 의존성으로 컴파일이 중단됐고, 원래 파일을 사본에 포함한 뒤 통과했다. 증거: 세션 시각화 DeckListFont_20261024/{Final.xml,Final.log,font-changes.json,verification.json,before}.
