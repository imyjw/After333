# Deck Building Pixel UI

The deck-building screen shares the teal, gold and cream palette and pixel
Back button used by GameStart, OwnedCards and Shop. This is presentation only:
offer generation, selection, account levels, save/resume and server calls are
unchanged. TitleText and StatusText keep their existing live text content.

## Layout

- Header: Back to Start on the left, title/status in the middle, account wallet
  on the right with room reserved for the global settings gear.
- Three card choices: thin gold frames; existing 550 x 733 reference size,
  spacing 50, stat anchors and stat font size are preserved.
- Right deck list: each row shows only quantity/card name on the left and resource cost on the right. Costs use a separate, right-aligned UI column; no DECK/Left, Type, Rarity, Curve, CostSum, CARD LIST or rarity suffix is displayed inside DeckListScrollView. Names wrap within their column without overlapping costs. The panel heading and existing vertical scrolling remain.
- Footer: a short selection hint, separated from the choices by a thin rule.
- The existing responsive card/deck calculation and CanvasScaler remain active.
  Decorative header regions use proportional anchors inside the safe area.

## Apply And Edit

The editor applies the layout once after importing the scripts, outside Play
mode. The version marker prevents a fresh reset on subsequent script reloads.
An already open scene with unsaved edits is not automatically saved or closed.

To deliberately reapply the layout, use:
`Tools > Project333 > Deck Building > Apply Pixel Deck Building Layout`.
This resets visual styling, so do not run it after customizing the design unless
you want to restore these defaults. It does not reset a player's draft progress.

Objects are materialized in the scene before Play, including BackToStartButton,
DeckBuildingHeader, DeckBuildingFooter and DeckListScrollView. Inspect them under
DraftOverlayPresenter. The editor refresh does not request offers or start a run.

Use DraftOverlayPresenter's Card Size, Option Spacing, Option Vertical Offset,
Deck Panel Reference Size and Deck Panel Right Padding for layout sizing. Those
areas remain driven by the responsive layout rather than arbitrary child offsets.
Use its Option Stat fields for ATK/HP styling and placement.

Title, status, deck heading, deck list and Back button retain their scene font
sizes and regions after applying the new layout. Their contents may still update
with game state. Back Button Label controls the Back button's caption. The
Interface Font is the bundled Korean fallback used when creating missing text.

## Current data and preview behavior (2026-10-01)

The 50-card draft pool is distinct from the 57 shared definitions and the 40-card actual random reward pool. Current rarity, account upgrades and offered cards follow server draft rules; dated layout sizes above are presentation defaults. Supported choice/deck-list long-press previews share trait/effect panels with the common card preview path. See [card_catalog.md](card_catalog.md), [draft_rules.md](draft_rules.md) and [occupant_status_tooltips.md](occupant_status_tooltips.md).## Name and cost columns (2026-10-09)

DraftOverlayPresenter enables DeckListCardHoldPreview's namesAndCostsOnly mode for DeckBuilding, including the native scene reused by tutorial practice. DeckListColumnsView generates CardName and CardCost Text children from the same grouped, sorted draft IDs. Font, size and color come from the authored DeckListText; the cost column is measured from the current costs and anchored to the right edge as the viewport width changes. A row's height follows its wrapped name. Long-pressing either column uses the existing preview lifecycle, viewport clipping and ScrollRect forwarding. Tutorial guidance uses the whole row bounds. The completed-deck Draft lobby retains its existing display.


## Editable UI before Play (2026-10-23)

The shared editor coordinator now prepares the current saved UI before Play, preserving existing scene objects and attached button components. See [editable_scene_layouts.md](editable_scene_layouts.md) for the common menu, dirty-scene saving policy and the distinction between editable UI templates and live account/battle data.

## 덱 목록 글자 크기 (2026-10-24)

현재 덱 목록은 두 씬 모두32pt다. 덱 빌딩의 카드 이름·오른쪽 비용 열은 DeckListText의 크기를 상속하고, 드래프트의 RuntimeCurrentDraftDeckText 및 원본/대체 목록도32pt로 맞췄다. 저장된 씬뿐 아니라 목록 생성·스타일 재적용 기본값도 동일하다. 제목·상태·요약·버튼의 글자 크기는 유지했다. 줄바꿈에 따른 높이·스크롤 범위와 길게 누르기는 기존 목록 레이아웃을 따른다.

추후 직접 조정할 때는 덱 빌딩 DeckListText, 드래프트 RuntimeCurrentDraftDeckText의 Text > Font Size를 수정한다. 명시적인 전체 스타일 재적용 도구는32pt 기본값을 적용한다.

검증: 격리 Unity6000.3.12f1 기존 DeckListCardHoldPreviewTests/DraftSceneVisualStyleTests40/40 통과. 두 씬의 변경은 목록 FontSize와 덱 빌딩 referenceFontSize뿐이며 fileID/참조/오브젝트 수 보존을 확인했다. 새 테스트·앱 빌드·설치·휴대폰 시각 확인·서버/DB 작업은 수행하지 않았다. 첫 검증은 사본의 누락된 DraftOverlaySceneBuilder 의존성으로 컴파일이 중단됐고, 원래 파일을 사본에 포함한 뒤 통과했다. 증거: 세션 시각화 DeckListFont_20261024/{Final.xml,Final.log,font-changes.json,verification.json,before}.
