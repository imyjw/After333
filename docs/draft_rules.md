# Draft Rules

최신 대조 기준: 2026-10-24. 공유 카드 정의 60종 중 드래프트 On 54종이며 Unity 네이티브 카탈로그도 54종이다. includeInRewards 적용값 On 43종과 실제 랜덤 보상 41종은 드래프트 후보와 별도 기준이다. 생략된 include 플래그의 기본값은 true이며, 최종 보상은 RewardCardEligibility의 추가 조건을 적용한다.

## Document Purpose

This document defines the full-game draft system used to create a 33-card deck before a run begins.

- High-level overview: [game_design.md](./game_design.md)
- Battle rules for the drafted deck: [battle_rules.md](./battle_rules.md)
- Meta progression context: [meta_rules.md](./meta_rules.md)

## Scope Separation

### Full Game Design Scope

- Draft is part of the full game design loop.
- The output of draft is a locked 33-card deck for a run.

### Current Account-Backed Implementation Scope

- Draft is implemented in the dedicated DeckBuilding scene.
- Starting a run creates a server-owned `draft_runs` record and spends the run ticket cost.
- The server generates every offer from the server card database and the run's draft seed.
- Unity sends only the run id, selected card id, and expected pick index.
- Every confirmed pick and the next displayed offer are persisted atomically through the server.
- If the client closes during draft, the same selected cards and the same current offer are restored after login.
- The server creates and locks the `33`-card run deck as part of accepting the final pick.

## Draft Objective

- Build exactly one `33`-card deck.
- That deck is then used for battle play.

## Core Draft Procedure

The draft loop is:

1. Present `3` different Legendary cards
2. Choose `1` Legendary card as the first deck card
3. Present `3` different eligible non-Legendary cards
4. Choose `1` card and add it to the deck
5. Repeat the non-Legendary offer for the remaining `32` picks

The full draft performs `33` choices: `1` Legendary opening pick and `32` non-Legendary picks.

## Deck Output Rule

- Final deck size after draft: `33` cards
- Deck editing after draft: not allowed

## Duplicate Limits

- Legendary cards: maximum `1` copy
- All non-Legendary cards: maximum `3` copies

## Rarity System

Confirmed rarity order from lowest to highest:

1. Common
2. Uncommon
3. Rare
4. Unique
5. Legendary

Confirmed appearance rule:

- The Legendary opening offer is separate from the non-Legendary rarity roll.
- Non-Legendary offer slots use these integer weights (total 10,000):
  - Common: `6600` (66%)
  - Uncommon: `2000` (20%)
  - Rare: `1000` (10%)
  - Unique: `400` (4%)
- Server and local draft use the shared `DraftRarityWeights` constants.
- These are per-slot probabilities, not predetermined deck counts. Under an ideal independent-slot model where the player always chooses the highest rarity, the expected non-Legendary selected proportions are Unique 11.53%, Rare 24.87%, Uncommon 34.86%, Common 28.75%. Player choices and exhausted candidate pools can change the actual proportions.
- When a rarity has no eligible card for the current slot, only currently available rarity weights participate in that roll.

## Offer Generation

Confirmed:

- Each pick presents exactly `3` different card ids; one offer cannot contain duplicate cards.
- The global card pool is non-consuming. Each new offer samples independently from all currently eligible cards.
- A card that has reached its deck copy limit is excluded from later offers.
- Non-Legendary rarity is rolled separately for each offer slot using `6600/2000/1000/400` weights (Common/Uncommon/Rare/Unique), subject to candidate availability.
- Before draft starts, the catalog must contain at least `3` unique Legendary cards.
- Before draft starts, the catalog must contain at least `13` unique non-Legendary cards.
- The non-Legendary pool must also provide at least `32` total copies under the three-copy limit.
- If fewer than `3` eligible non-Legendary card ids remain during draft, offer creation fails instead of presenting an invalid offer.

2026-09-30: Nuclear is enabled in draft after registration of the user's card artwork. As a Legendary card, it participates in the opening offer only; later offers and copy limits retain the existing rules. Random reward eligibility is also enabled under the existing upgradeable-card policy and rarity weights.

2026-10-01 사용자 확정: 트랩: 리턴(`TrapReturn`)은 중립 Rare 마법으로 드래프트에 포함한다. 설치 무료·발동 전투 골드 4이며 Rare 확률과 비레전더리 최대 3장 규칙은 유지한다. 덱에 3장을 넣을 수 있어도 동일 소유자의 같은 CardId는 동시에 하나만 설치할 수 있다. 강화 불가지만 사용자 지정 예외로 랜덤 보상에도 포함한다. 미러 필드에는 이 비강화 보상 예외를 적용하지 않는다.

- 2026-10-01: FireSpear / 파이어스피어 (Fantasy, Common, Mana3) is enabled in shared draft and the Unity card catalog. Existing Common odds and non-Legendary three-copy limits remain unchanged.
- 2026-10-03 변경: PlasmaShield / 플라즈마 쉴드 (ScienceCivilization, Common, Power1) is registered in shared draft (rewards/card packs excluded; not upgradeable) and the native Unity catalog. Common probabilities and the existing three-copy limit are unchanged.
- 2026-10-03 변경: ManaShield / 마나쉴드 (Fantasy, Common, Mana 1) is registered in shared draft (rewards/card packs excluded; not upgradeable) and the native Unity catalog. Existing Common probabilities and the non-Legendary three-copy limit are unchanged; permanent Magic Defense buffs do not change its rarity or draft cost.
- 2026-10-01 트랩-미러 필드(TrapMirrorField): 판타지 Rare, 기존 Rare 제시 확률·최대3장 규칙으로 덱 빌딩에 포함한다. 설치 무료, 발동 마나2·골드 대체, 강화 불가, 랜덤 보상 제외. 동일 CardId는 동시에 하나만 설치할 수 있다.

## Persistence And Resume

- `POST /runs/start` returns the server-generated Legendary opening offer.
- `POST /runs/draft-state` returns the server-owned ordered pick history and current offer.
- `POST /runs/select-draft-card` accepts only `runId`, `cardId`, and the client's expected zero-based `pickIndex`.
- The server reads the authoritative current offer from the saved `current_offer_card_ids`, preserving all three card IDs and their order. It does not regenerate an already issued offer from the seed or current catalog.
- A confirmed pick, its actual three-card offer, and the next offer are saved in one database transaction.
- Resume restores both the ordered selected-card list and the exact saved offer. Legacy runs without a saved offer initialize it once under the run row lock.
- Opening and later-offer rarity rules apply when generating new offers. Later changes to rarity or draft eligibility do not invalidate previously issued offers or accepted picks.
- A restored offer must contain exactly `3` distinct card IDs. A selection is checked against those saved IDs; the client cannot replace the offer.
- Cards always resolve through the current deployed card definitions: stats, effects and rarity are not frozen per run. Previously selected IDs and copy counts remain intact after a card update; newly generated offers use current rarity and eligibility.
- A missing current card definition or corrupt saved offer produces an error instead of silently substituting another card or rerolling. Existing IDs must remain available in the catalog even when excluded from future drafts.
- Leaving DeckBuilding does not abandon or reroll the current draft.
- 2026-10-02: 완료 런의 보상 진입은 중앙 단독 버튼 대신 닫힌 상자·개봉 애니메이션·전체 보상 결과 창을 사용한다. 상자를 누르지 않고 종료해도 완료 런은 유지하며, 지급 후 확인 전 종료는 같은 RunId의 원래 영수증으로 표시를 복구한다. 다음 런·기존 드래프트를 자동으로 시작하거나 보상을 재추첨하지 않는다. [보상 상자](run_reward_chest.md).
- A stale or replayed pick index is rejected instead of adding a duplicate card.
- Unity cannot submit an arbitrary offer, full pick history, or completed deck as authority.
- The former client-authoritative `/runs/save-draft-picks` and `/runs/complete-draft` endpoints return `410 Gone`.

## AI and Draft

Online PvE uses ten fixed cross-civilization AI decks from `Server/Project333.PvpServer/Data/pve_ai_decks.json`.
At each new battle the server chooses one deck uniformly (10% each), then shuffles it with the normal battle setup.
Each deck contains 33 cards: 1 Legendary, 5 Unique, 9 Rare, 11 Uncommon, and 7 Common.
Only includeInDraft=true cards are eligible (omitted flags default to true), with at most 3 copies per card and 1 Legendary total.
The selected composition stays fixed for that battle; restoring an existing battle does not reroll it.
AI upgrades remain level 0. This does not change player draft rules or PvP opponent decks.
See [pve_ai_decks.md](./pve_ai_decks.md) for all ten lists and maintenance rules.

## Relationship to Other Documents

- [game_design.md](./game_design.md) describes the account-backed run loop that owns the draft.
- [battle_rules.md](./battle_rules.md) defines how the finished deck behaves in battle.
- [meta_rules.md](./meta_rules.md) defines how the drafted deck is reused across a run.

## Open Issues / TBD

> TBD: Future PvE difficulty tiers or win-count-based encounter selection

> TBD: Undoing committed draft picks or previewing future offers beyond the current immediate-save and long-press card preview flow

## Deck List Card Preview

- In both Draft and DeckBuilding, hold a card entry in the player's deck list for 0.4 seconds to show its card artwork at the center of the screen.
- The preview uses the battle card artwork and the account's current upgrade level for displayed stats. Shared card tooltips show the card's actual abilities/effects at the sides; runtime buffs/debuffs below and field influences above are shown only when that preview has the corresponding battle state, so a draft card does not invent battle-only statuses.
- Releasing the mouse button or touch hides the preview immediately. Leaving the entry, scrolling/dragging, hiding the list, or switching away from the app cancels the hold.
- A quick tap does not open a preview. Deck summaries, section headings, and empty space are not card entries.
- Grouped duplicates and wrapped card names resolve to the displayed card ID. Existing list scrolling remains available.


### Draft offer hold preview (2026-09-25)

- Each of the three offered cards supports a 0.4-second hold to show its card image in the center. Release closes it immediately.
- A completed hold does not pick the card; a new short click/tap still picks normally. Leaving, moving beyond the drag threshold, hiding/replacing the offer, or losing app focus cancels the hold.
- Reuses the deck-list preview overlay and account upgrade stats. Offer identity and server pick/save rules are unchanged.

2026-10-01 트랩 - 반응 장갑(`ExplosiveReactiveArmour`): 과학문명 Rare 마법, 드래프트 포함·랜덤 보상 제외·강화 불가. 설치 무료·발동 전력4(부족분 골드 대체), 상대 일반 근거리 유닛이 내 마스터를 근접 공격할 때만 발동한다. 기존 Rare 확률과 비레전더리 최대3장 규칙을 따르며 동일 CardId는 동시에 하나만 설치할 수 있다. 리턴의 비강화 보상 예외는 적용하지 않는다.

2026-10-01 트랩 - 아이언 돔(`IronDome`): 과학문명 Rare 마법, 드래프트 포함·랜덤 보상 제외·강화 불가. 설치 무료·발동 전력4(부족분 골드 대체), 상대 일반 원거리 유닛이 내 실제 마스터를 공격할 때만 발동한다. 기존 Rare 확률과 비레전더리 최대3장 규칙을 따르며 동일 CardId는 동시에 하나만 설치할 수 있다. 반응 장갑과 별도 CardId이므로 함께 설치할 수 있고, 공통 설치 순서에 따라 해당 공격 방식에 맞는 트랩을 검사한다. 리턴의 비강화 보상 예외는 적용하지 않는다.