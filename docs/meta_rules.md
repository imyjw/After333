# Meta Rules

실제 적용 상태(2026-10-02): 별도 승인 후 운영 DB0020/0021/0022 및 카드팩 서버를 적용하고 Android APK(versionCode5)를 빌드했다. 현재 운영 강화 비용도 아래3/6/10/20/30/33표와 일치한다. [실제 적용·보존 검증](shop_card_packs.md)을 참고한다. 과거 소스 작업의 미배포 기록과 실기기 미검증은 구분한다.

최신 기준: 2026-10-02. 현재 카드 정의·강화 코드·서버 보상 필터와 대조하고 런 종료 보상 상자·결과 표시 복구 및 상점 카드팩 구매·개봉을 반영했다. 과거 날짜의 결정·검증 기록은 그대로 유지한다.

## Document Purpose

This document defines the full-game run structure, win/loss tracking, and out-of-battle reward economy.

- High-level overview: [game_design.md](./game_design.md)
- Battle details: [battle_rules.md](./battle_rules.md)
- Draft details: [draft_rules.md](./draft_rules.md)
- Account/database persistence: [account_persistence_db_design.md](./account_persistence_db_design.md)
- PostgreSQL implementation plan: [postgresql_db_implementation_plan.md](./postgresql_db_implementation_plan.md)

## Scope Separation

### Full Game Design Scope

The full meta loop is:

1. Start a run
2. Build a 33-card deck through draft
3. Fight repeated battles with that deck
4. End the run at 33 cumulative wins or 3 cumulative losses
5. Receive rewards after run end
6. Spend Resource Gold in meta systems

### Current Account-Backed Implementation Scope

Implemented through the server and PostgreSQL:

- Ticket spending when a new run starts
- Persistent draft picks, current draft offer, completed deck, and run resume
- Normal matchmaking battles against humans or fallback AI applied to the current run
- Separate Gold Dungeon: up to 3 attempts per active run started with 3 tickets, no midnight/relogin reset, victory Resource Gold +10, no run-score changes (see battle_modes.md)
- `33` cumulative win and `3` cumulative loss termination
- One-time run-end reward claim
- Persistent Resource Gold, card-copy collection, and upgrade levels
- Card upgrade spending and battle-time upgraded stat application
- Resource Gold ticket purchase
- Shop card packs: 33 Resource Gold for ten independently drawn, immediately granted card copies; durable purchase receipt and pack-opening presentation
- Server-verified rewarded video ticket grants

Still deferred:

- Individual card-shop inventory, pricing and refresh beyond the implemented ticket/card-pack products
- Crafting
- Paid purchase flows

## Run Definition

- A run is a multi-battle progression sequence.
- A run uses one drafted `33`-card deck.
- That deck is reused across repeated battles within the run.

## Run End Conditions

- A run ends immediately when cumulative wins reach `33`.
- A run ends immediately when cumulative losses reach `3`.

## Rewards

- Rewards are paid after the run ends.
- A completed run's reward can be claimed only once.
- 2026-10-02 보상 표시: 33승·3패 완료 런은 어두운 화면의 닫힌 보상 상자 → 클릭·개봉/빛 애니메이션 → 읽기 쉬운 전체 보상 목록 순서로 보여준다. 0승도 같은 흐름으로 지급 없음 문구를 표시한다.
- 미수령 완료 런은 앱 종료 후에도 보존한다. 지급 후 결과를 확인하지 못한 경우 같은 계정·RunId의 상자에 다시 진입해 사용자가 다시 열면 원래 영수증을 재조회하고 현재 지갑 상태를 유지한다. 로그인만으로 수령·개봉하지 않는다. 개봉 재생·재요청으로 추가 지급하거나 다시 추첨하지 않는다. [복구·예외 상세](run_reward_chest.md).
- Temporary reward formula:
  - `Resource Gold = final win count * 3`
  - `random card copies = final win count * 3`
- Each random card reward first rolls a rarity using these weights:
  - Common: `40`
  - Uncommon: `30`
  - Rare: `18`
  - Unique: `9`
  - Legendary: `3`
- 랜덤 카드 보상은 강화 가능 카드가 원칙이다. 사용자 확정 예외인 트랩: 리턴(`TrapReturn`)은 강화 불가여도 `includeInRewards: true`로 보상에 포함하며, 다른 강화 불가 카드에는 이 예외를 확대하지 않는다.
- The Master card is never included in random card rewards.
- If an eligible rarity has no rewardable card, the roll is normalized across the remaining eligible rarities.

현재 카드 현황(2026-10-08): JSON/domain 정의 60종, 드래프트 On 54종, 강화 가능 44종이다. `includeInRewards`의 적용값은 On 43종/Off 17종이며, 천라지망·초소형 발전기는 플래그 On이어도 강화 불가 정책으로 제외되어 실제 랜덤 보상 대상은 41종이다. 대환단은 강화 가능으로 변경되어 보상·카드팩 풀에 포함된다. 트랩: 리턴의 비강화 보상 예외는 포함하고 미러 필드·반응 장갑·아이언 돔은 제외한다. Unity 네이티브 카탈로그는 트랩 5종을 포함한 53종으로 공유 정의 60종과 별도로 집계한다. [카드 목록](card_catalog.md).

> TBD: The final reward formula is not fixed yet.

## Meta Currency

### Gold

- Gold is an in-battle resource.
- Gold is used inside battle rules.
- Gold is not the same as Resource Gold.

See [battle_rules.md](./battle_rules.md).

### Resource Gold

- Resource Gold is an out-of-battle currency.
- Resource Gold is gained through run-end rewards.
- Resource Gold is used to upgrade cards.
- Resource Gold is used to buy tickets to start the game.
- Resource Gold is used to buy a ten-card pack for 33 in the same shop. Pack cards commit to the collection at purchase, before opening.
- The first shop product exchanges `3` Resource Gold for `1` game ticket.
- Ticket purchases are made from the dedicated `Shop_VSlice` scene opened from the Game Start scene.

## Administrator Gift Inbox

Confirmed 2026-09-13: administrators may dispatch tickets and Resource Gold to specific accounts or all accounts existing at dispatch time. Future registrations do not receive older all-account gifts. Gifts have no expiry by default; the administrator may specify an expiry. Dispatch only creates inbox entries. Claiming applies the server-defined attachments exactly once, atomically with the wallet and transaction record. Account IDs determine ownership, server time determines expiry, and reconnect/retry cannot duplicate a credit. Claimed gifts disappear from the client inbox immediately and remain hidden after refresh or relaunch; server claim and transaction records are retained for idempotency. A local administrator tool dispatches gifts; no public administrator endpoint is added. See [gift_inbox.md](./gift_inbox.md) for the operator workflow and limits.
## Meta Systems

### Shop

Confirmed 2026-10-02:

- The existing ticket product costs 3 Resource Gold for 1 ticket.
- A card pack costs 33 Resource Gold and immediately grants ten random card copies in one server-owned DB transaction with its charge, ledger and original receipt.
- Each copy independently rolls Common 40%, Uncommon 30%, Rare 18%, Unique 9% or Legendary 3%, then uniformly selects one card from that rarity's common reward-eligible pool. Duplicates are allowed; no rarity is guaranteed within a pack.
- Master cards and other reward-ineligible cards are excluded using the same RewardCardEligibility policy as run rewards, including the confirmed non-upgradeable TrapReturn exception. All five rarity pools must be valid and nonempty; otherwise reject before spending instead of changing the promised odds.
- Purchase RequestId persists before sending. Interrupted purchases automatically resolve through the existing completion/cancellation protocol; opening never charges, rerolls or grants cards again.
- A confirmed purchase dims the shop and shows a sealed foil pack. Clicking tears off its top, ejects ten white glowing cards, then shows the actual ten copies in five columns and two rows. Rare/Unique/Legendary cards have blue/red/yellow animated border auras. Any click returns to the shop after results finish appearing and a short input guard.
- Quitting before opening or confirming keeps the paid cards. A local account/server/request presentation marker restores the same ten-card pack on shop reentry; it is not a server-held unopened-pack inventory or cross-device read history.
- Server migration 0022_card_pack_purchase_receipts.sql is required. Source changes do not apply the migration or deploy a running server/client.

See [card-pack purchase, opening and recovery](shop_card_packs.md).

> TBD: Individual card-shop inventory generation, pricing and refresh beyond the fixed ticket/card-pack products.

### Card Upgrade

Confirmed purpose:

- Upgrade cards

Confirmed rules (upgrade costs revised 2026-10-01; Lv.2 revised 2026-10-02):

- Each card has an independent upgrade level.
- Maximum card level is `13`.
- Upgrading requires Resource Gold and copies of that exact card.
- The target level selects the common cost below, regardless of an upgradeable card's rarity or type. Card copies and Resource Gold use the same amount; battle Gold is a separate resource.

<a id="card-upgrade-costs"></a>

| Target level | Same-card copies consumed | Resource Gold consumed |
| --- | --- | --- |
| Lv.1 | 3 | 3 |
| Lv.2 | 6 | 6 |
| Lv.3 | 10 | 10 |
| Lv.4 | 20 | 20 |
| Lv.5 | 30 | 30 |
| Lv.6 | 33 | 33 |
| Lv.7 | 33 | 33 |
| Lv.8 | 33 | 33 |
| Lv.9 | 33 | 33 |
| Lv.10 | 33 | 33 |
| Lv.11 | 33 | 33 |
| Lv.12 | 33 | 33 |
| Lv.13 | 33 | 33 |

- These are individual upgrade costs: Lv.1 -> Lv.2 consumes 6 copies and 6 Resource Gold; Lv.5 -> Lv.6 and Lv.12 -> Lv.13 each consume 33 and 33. Upgrading from Lv.0 through Lv.13 totals 333 copies and 333 Resource Gold.
- Unity's shared cost table supplies Owned Cards requirements and upgrade availability. The server still reads `card_upgrade_costs` for the authoritative charge; migration `0020_card_upgrade_costs_revision.sql` established the 13 transitions, and the subsequent `0021_card_upgrade_level2_cost_revision.sql` changes only Lv.1 -> Lv.2 to 6/6. Apply all pending migrations through the existing runner. Updating the client alone does not change a database that has not applied these revisions.
- Existing RequestId/ExpectedUpgradeLevel, atomic wallet/collection/ledger/receipt handling and saved-request resolution remain. A committed same-ID replay retains its original paid cost and returns current wallet/collection state; this revision does not refund past charges or rewrite levels/receipts.

- Cards at `Lv.13` cannot be upgraded further.
- Confirmed 2026-10-01 presentation: clicking the artwork area in the Owned Cards detail modal flips it to a black background/white table of each individual Lv.1–13 gain (HP, ATK and the actual damage/defense effect). Clicking that same area flips back. Gains use the current card definition and existing upgrade/stat rules, not cumulative totals or owned-card level. Non-upgradeable cards show 강화 불가. Same-card upgrade responses preserve the current face and refresh hidden front stats; closing or changing cards resets the front. The 0.36-second animation uses unscaled time and blocks repeated clicks while flipping.
- Upgrade stat gains are card-specific. These are the currently implemented schedules, not defaults to assign to a new card:
  - Standard schedule: ATK +1 at Lv.3/6/9/13; HP +1 at every other level (Lv.13 total ATK +4 / HP +9).
  - A-212 and Cerberus: HP +1 at Lv.1 through Lv.12; ATK +1 only at Lv.13 (total ATK +1 / HP +12).
  - Mana Pond, Robot Factory, Gaebang Branch, Merchant Caravan, Inn, Power Plant and Nuclear Power Plant: HP +1 at every level Lv.1 through Lv.13 (total HP +13; no ATK gain).
  - Demon King and Hero: ATK +1 at Lv.3/6/9, ATK +3 at Lv.13, and HP +1 at every other level (total ATK +6 / HP +9).
- Confirmed 2026-09-12: Cerberus reaches ATK 10 / HP 78 at Lv.13; Mana Pond reaches ATK 0 / HP 33. Card costs, production and other effects do not change under these schedules.
- Revised 2026-10-05: ManaPond costs 6 Mana at every upgrade level. Mana production +3, SpellPower +20 and the HP-only upgrade schedule remain unchanged.
- For every new card, ask the user for upgrade eligibility and the exact per-level gains before implementing upgrade behavior. Do not infer a schedule from its card type, rarity, attack capability, similar cards or a shared-code fallback. An explicitly supplied schedule may group levels if every level 1 through 13 is covered; do not ask again when that card's schedule has already been supplied.
- All currently registered Unit and Building cards are upgradeable; new cards still require the user-supplied eligibility and schedule above.
- Firebolt (`firebolt`), Firewall (`Firewall`), Timed Bomb (`TimedBomb`), Biochemical Bomb (`BiochemicalBomb`), Nuclear (`Nuclear`), Earthquake (`Earthquake`), Meteor (`Meteor`), and FireSpear (`FireSpear`) are upgradeable Spell cards; each level adds `+1` to their direct or stored damage. Nuclear was revised on 2026-09-30: immediate Fixed damage is 100 + level (Lv.13: 113), while Fallout remains 10 per stack. Since 2026-10-08 Fallout is applied only to enemy Units/Master present at cast time; later summons do not inherit it. After the user supplied its card artwork, draft/reward flags were enabled on the same date; existing rarity probabilities and upgrade rules apply.
- Revised 2026-10-03: PlasmaShield and ManaShield are not upgradeable. Granted Physical/Magic Defense stays at base +10 even with an old stored level. They remain in draft and are excluded from random run rewards and shop packs. No SpellPower or amplification bonus applies.
- Confirmed 2026-09-30: `ElfElder` / 엘프 장로 uses the canonical CardId without spaces (confirmed 2026-10-09). It is a Rare Fantasy Elf Unit costing `5 Mana`, with ranged Physical `30 ATK / 25 HP`, defense `0 / 0`, movement, Piercing and Rush; no separate card effect. Draft and random rewards are enabled. Explicit ATK `+1` at Lv.3/6/9/13 and HP `+1` at other Lv.1–13 yield `34 ATK / 34 HP`. Cost, defenses, race, Piercing and Rush remain unchanged by upgrades.
- Confirmed 2026-09-30: SkeletonArcher / 스켈레톤아처 is a Common Fantasy Undead Unit costing `1 Mana`, with ranged Physical `10 ATK / 3 HP`, defense `0 / 0`, movement enabled and no card effects or innate traits. Draft and random rewards are enabled. Its explicit upgrade schedule is ATK `+1` at Lv.3/6/9/13 and HP `+1` at every other level through Lv.13, reaching `14 ATK / 12 HP`. Cost, defenses, race and trait flags do not gain new effects.
- Confirmed 2026-09-30: OrcChieftain / 오크 족장 is a Rare Fantasy Orc Unit costing `7 Mana`, enabled in draft and random rewards. Explicit gains are ATK `+1` at Lv.3/6/9/13 and HP `+1` at every other level through Lv.13: base `50 ATK / 70 HP` becomes `54 ATK / 79 HP`, or `64 ATK / 89 HP` with its one active self aura. Each source grants the same `+10 ATK / +10 Max HP / +10 Current HP` at every upgrade level; no aura, cost, defense or race increase is added.
- Confirmed 2026-09-30, base ATK revised 2026-10-01: OrcBerserker / 오크 광전사 is an Uncommon Fantasy Unit of Orc race costing `5 Mana`, enabled in draft and random rewards. Its explicitly confirmed upgrade schedule is ATK `+1` at Lv.3/6/9/13 and HP `+1` at every other level through Lv.13. From base `20 ATK / 50 HP`, Lv.13 is `24 ATK / 59 HP` before Berserker. Cost, defenses, race and the innate Berserker rule do not change.
- Confirmed 2026-10-01 balance revision: OrcWarrior / 오크 전사 now has base `20 ATK / 40 HP`. Its existing standard schedule grants ATK `+1` at Lv.3/6/9/13 and HP `+1` at all other levels through Lv.13, yielding `24 ATK / 49 HP`. Cost, defenses, race, traits and draft/reward eligibility remain unchanged.
- Confirmed 2026-09-30: Earthquake is an Uncommon Neutral spell costing `3 Gold`, enabled in draft and random rewards. Each level Lv.1-13 adds `+1` Physical damage: Units/Masters use `10 + level`, Buildings use `3 * (10 + level)` before defense (Lv.13: 23/69). Gold cost and Building multiplier remain fixed. See [Earthquake presentation and card rules](earthquake_presentation.md).
- Confirmed 2026-09-30: Meteor is a Unique Fantasy spell costing `7 Mana`, enabled in draft and random rewards. Each level Lv.1-13 adds `+1` base Magic damage: `40 + level` (Lv.13: 53). Current allied SpellPower is added once per target before Magic Defense; the Mana cost does not increase. The user explicitly confirmed Fantasy affiliation and subsequently requested Unique rarity. See [Meteor presentation and card rules](meteor_presentation.md).
- Confirmed 2026-10-01: GaebangChief / 개방 방주 is a Unique Murim Human Unit costing `7 Qi`, enabled in draft and random rewards, with melee Physical base `10 ATK / 60 HP`, defense `0 / 0`, movement and no innate traits. Explicit upgrades are base ATK `+1` at Lv.3/6/9/13 and HP `+1` at all other levels through Lv.13, yielding printed `14 ATK / 69 HP`. The live bonus is `10 * current owner's battle Gold`, does not consume Gold, and is separate from upgraded BaseAttack and account Resource Gold. With `3 Gold`, active Lv.0/Lv.13 current ATK is `40/44`. Death, Drained, Erasure and Sealbound suppress this bonus; Gu immediately changes the resource owner. Gold used for cost substitution is deducted before calculating the bonus. Cost, defenses, race and Gold multiplier do not increase with upgrades.
- 2026-10-01 사용자 확정: ManaCannon / 마나 대포는 판타지 언커먼·종족 없는 Unit, 소환 마나 `2`, 원거리 Magic 기본 `40 ATK / 10 HP`, 방어 `0 / 0`, 이동 가능·고유 특수효과 없음이며 드래프트·보상에 포함한다. Lv.3/6/9/13 기본 ATK `+1`, 나머지 Lv.1–13 HP `+1`로 Lv.13은 `44 ATK / 19 HP`다. 적법한 일반 공격마다 마나 `1`을 한 번 지불하고 부족하면 전투 골드 `1`로 대신 낸다. 망각 중 비용 효과는 억제되며 검증 실패 시 미차감. 강화로 소환·공격 비용, 골드 대체율, 방어, 이동, 종족 없음은 바뀌지 않는다.
- Confirmed 2026-10-01: FireSpear is a Common Fantasy spell costing 3 Mana, enabled in draft and random rewards. A living allied/enemy Unit, Building or Master anchors its own field column (front/rear two cells). Each Lv.1–13 adds +1 Magic damage to base15, reaching28 before current caster SpellPower and defense. Cost and area remain fixed. See [FireSpear rules and presentation](fire_spear_presentation.md).
- Revised 2026-10-03: PlasmaShield / 플라즈마 쉴드 is a Common ScienceCivilization spell costing Power1, draft enabled, rewards/card packs disabled, and not upgradeable. Grants permanent stackable Physical Defense +10 to one allied/enemy living Unit (Master excluded) or Building. No damage, duration/count or SpellPower bonus. Formation VFX disappears after one cast. [Rules and assets](plasma_shield_presentation.md).
- Revised 2026-10-03: ManaShield / 마나쉴드 is a Common Fantasy scripted spell costing Mana 1 (existing Gold 1 substitution), with draft enabled, rewards/card packs disabled, and no upgrades. It grants one allied/enemy living Unit (Master excluded) or Building permanent, stackable Magic Defense +10; granted defense stays +10 regardless of stored levels. Physical Defense is unchanged and stacks independently with PlasmaShield. There is no damage, duration/count or SpellPower bonus. Existing target validation, Erasure removal, Drained suppression, Gu transfer and snapshot restoration rules remain. A smooth transparent mana sphere plays once for about 0.817 seconds and disappears, with Image peak alpha 0.28 and original source alpha/isotropic shape preserved. [Rules and assets](mana_shield_presentation.md).
- 2026-10-01 사용자 확정: 트랩-미러 필드(TrapMirrorField)는 판타지 Rare 마법, 설치 무료·발동 전투 마나2(골드 대체 가능), 강화 불가·드래프트 On·랜덤 보상 Off. Lv.1–13 수치 증가가 없고 공격자 현재 ATK와 발동 시 트랩 소유자 주문력을 사용한다. 트랩: 리턴의 보상 예외를 이 카드로 확대하지 않는다.
- 2026-10-01 사용자 후속 수정: 트랩 - 반응 장갑(`ExplosiveReactiveArmour`)은 과학문명 Rare 마법, 설치 무료·발동 전력 4(부족분 골드 대체), 강화 불가·드래프트 On·랜덤 보상 Off. Lv.1–13 증가량 없이 상대 일반 근거리 유닛의 내 마스터 근접 공격을 취소하고 공격자를 직접 파괴한다. 원거리 유닛·마스터·건물 공격자는 제외한다.
- 2026-10-01 사용자 확정: 트랩 - 아이언 돔(`IronDome`)은 과학문명 Rare 마법, 설치 무료·발동 전력4(부족분 골드 대체), 강화 불가·드래프트 On·랜덤 보상 Off. Lv.1–13 증가량 없이 상대 일반 원거리 유닛의 내 실제 마스터 공격을 취소하고 공격자를 직접 파괴한다. 근거리 유닛·마스터·건물 공격자는 제외한다. 전투 골드는 계정 Resource Gold와 별개이며 리턴의 비강화 보상 예외는 적용하지 않는다.
- 2026-10-01 사용자 확정: 트랩 - 서먼 트랩(`SummonTrap`)은 중립 Common 트랩 마법, 설치 무료·발동 전투 골드 1, 강화 가능·드래프트/랜덤 보상 On이다. Lv.1–13 매 레벨 기본 Physical 피해 +1로 Lv.0 20·Lv.13 33이며 설치 시 강화 피해를 포착한다. 정상 손패 소환 초기화 후 상대 일반 유닛에 직접 피해를 준다. 마스터·건물·이동·고독·반환·부활 제외, 쉴더 이전·주문력 없음. 발동 비용과 소환 경계는 강화로 변하지 않는다. [세부 규칙](battle_rules.md#summontrap).
- The following spells are not upgradeable: PlasmaShield, ManaShield, CheonraJimang, TenThousandYearSnowGinseng, Gu, HuanShu, RobotFusion, Microreactor, PowerBank, ManaStone, ManaStoneBundle, MagicAmplificationCircle, TrapReturn, TrapMirrorField, ExplosiveReactiveArmour and IronDome.
- 강화 불가 카드는 원칙적으로 랜덤 런 카드 보상에서 제외한다. 트랩: 리턴은 사용자 확정 보상 예외이며 강화 가능 카드 44종 집계에는 들어가지 않는다. 트랩: 리턴의 설치 비용은 0, 발동 비용은 전투 골드 4이고 Lv.1–13 증가량은 없다. 계정 Resource Gold와 별개의 비용이다.

### Ticket

Current temporary rules:

- Starting a new draft run costs `3` tickets.
- Tickets are consumed once when the server creates the new run, before draft begins.
- A player cannot start another run while a resumable run exists or completed-run rewards remain unclaimed.
- One ticket can be purchased for `3` Resource Gold.
- Ticket balance and ticket purchases are server-authoritative and persisted to the account wallet.
- Ticket/card-pack purchases and card upgrades require a non-empty UUID RequestId. A repeated account/request pair returns its committed result without another charge. Reusing that pair for different input or a different operation is rejected.
- Upgrade requests also include the displayed ExpectedUpgradeLevel. A different current level rejects the request before spending, even with a new request ID.
- Success receipts, wallet/collection changes and the account transaction ledger commit together. Receipts persist across server restarts and are not automatically expired.
- Replayed results preserve the original operation cost while returning current wallet and collection state. Unity retains uncertain requests across scene changes and restarts until the matching response or a definitive rejection is received.
- A completed, server-verified rewarded video grants `1` ticket.
- Rewarded video tickets are temporarily limited to `10` per account per UTC day with a `30`-second reward cooldown.
- Unity cannot grant an ad ticket directly; the wallet changes only after a valid provider server-to-server callback.
- Provider event IDs are idempotent, so callback retries cannot grant duplicate tickets.
- See [rewarded_ads.md](./rewarded_ads.md) for the integration and security contract.

## Persistence

- Account wallet, card collection, upgrades, ticket/card-pack purchase receipts, draft picks, current offer, completed deck, run record, and reward claim are server-owned data.
- Unity may cache session and display data but is not authoritative for progression.
- Closing and reopening the game restores the latest resumable run from the authenticated account.
- Normal battles use the same persisted run win/loss counters regardless of human/AI opponent. Gold Dungeon attempts never change these counters; their server result settles the challenge admission and separate Resource Gold reward. See [battle_modes.md](./battle_modes.md).
- Only results resolved by a server battle may advance account run wins/losses or unlock run rewards. Client-reported or offline results never update account progression.
- At battle start, the server fixes each participant's account, seat, run, deck contents and card upgrade levels. Rejoining or reconnecting restores this binding and cannot change the run receiving the result.
- Run-result persistence must match the bound account and the run's completed deck. A battle started without a run cannot attach one after it starts.
- Every battle has a server-generated result ID, retained by stored snapshots and finalized result journals. A committed result ID can never increment a run again.
- Result receipts, both participants' run counters, and any persisted PvP match completion commit in one DB transaction. Draws complete the match without incrementing wins or losses.
- The server durably queues terminal results before sending battle-end responses. Failed DB delivery remains pending for background retries and process-restart replay; client result uploads remain prohibited.
- Unity refreshes server run data through /me while awaiting result persistence; it must not upload local win/loss totals as a fallback.

- Server interruption cancels unfinished battles without a win/loss, draw, reward, ticket charge/refund, or run/deck reset. A Gold Dungeon canceled by server interruption restores its consumed attempt against the original run, without adding attempts to a later run. Existing completed results and pending durable terminal journals remain valid. See the server interruption policy in battle_rules.md.

## Relationship to Other Documents

- [game_design.md](./game_design.md) separates full-game design from the current slice scope.
- [draft_rules.md](./draft_rules.md) defines how the run deck is created before the run begins.
- [battle_rules.md](./battle_rules.md) defines what happens inside each individual battle during a run.
- [account_persistence_db_design.md](./account_persistence_db_design.md) defines the implemented account, collection, deck, run, reward, and PvP persistence model plus deferred extensions.
- [postgresql_db_implementation_plan.md](./postgresql_db_implementation_plan.md) records the PostgreSQL migrations and verification direction.

## Deferred Meta Systems

These items are intentionally reserved for later implementation:

- Individual card-shop inventory, refresh, pricing and purchases beyond the fixed ticket/card-pack products
- Server-held unopened-pack inventory and cross-device opening/confirmation history
- Crafting
- Paid purchases
- Ranked, season, and ladder rewards
- Final balancing of run rewards, upgrades, and ticket economy

## Open Issues / TBD

> TBD: Final reward formula details

> TBD: Individual card-shop pricing and inventory rules beyond existing ticket/card-pack purchases

Card-pack contents, price and probabilities are confirmed above. Additional pack products or guarantees require a separate user decision.

> TBD: Crafting rules

> TBD: Future AI difficulty tiers and encounter selection beyond the ten fixed AI decks

> TBD: Additional normal-run interim rewards beyond the separate, implemented Gold Dungeon reward

- 2026-10-08 대환단(Daehwandan) 변경: 강화 가능, Lv.1–13 매 레벨 효과수치 +1. Lv.0 30, Lv.13 43만큼 사용 즉시 내 마스터 공격력 증가. 이후 기 생산 삭제. 비용 기5·무림·레전더리·대상 선택 없음은 유지하며 보상/카드팩 포함.
