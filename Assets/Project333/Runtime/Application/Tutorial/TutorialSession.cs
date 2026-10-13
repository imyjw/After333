using System;
using System.Collections.Generic;
using Project333.Runtime.Domain.Draft;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.Runtime.Application.Tutorial
{
    public enum TutorialAction { None, StartRun, PreviewOffer, PickCard, PreviewDeck, CompleteDeck, StartGoldDungeon, SelectMulliganCard, ConfirmMulligan, ExpandHand, SelectGoblin, SummonGoblin, SelectManaPond, SummonManaPond, EndTurn, AttemptBlockedAttack, SummonArcherAndEndTurn, RangedMasterAttack, MeleeCounterattack, EndTurnForGolem, AttackGolem, CastPracticeFireball, PreviewOccupant, MovePracticeUnit, SwapPracticeUnits, AttemptBuildingMove, FinishPracticeBattle, SelectUpgradeCard, ShowUpgradeTable, RestoreCardArtwork, UpgradePracticeCard }
    public enum TutorialCue
    {
        Introduction, Controls, Isolation, Civilizations, Resources, Gold, Substitution, MixedDeck,
        CardKinds, Deck, Run, StartPractice, DraftOffers, LegendaryOpening, SpecialEffects,
        OfferPreview, OfferPick, RarityCommonUncommon, RarityRareUniqueLegendary, RarityRole,
        DeckList, DeckPreview, CopyLimits, LockedDeck, ManualDraftPick, DraftFivePicked, AutoDraft, DraftReady, GameModes, LobbyDeckPreview, PvpMode, RunRecord,
        GoldDungeon, GoldDungeonReward, GoldDungeonRules, GoldDungeonRemaining, Reconnect,
        GoldDungeonPractice, BattleEntry, MulliganIntro, MulliganSelection, MulliganConfirm, BattleTurns, PlayerResources, OpponentResources, TurnIncome, UniqueResources, CarryResources, GoldSeparation, PracticeGrant, HandExpand, GoblinSelection, GoblinCost, GoblinSummon, PondSelection, PondSummon, ResourceProduction, PracticeEndTurn, ResourcesReturned, DrawReturned, Battlefields, SummoningSickness, MasterObjective, MasterStats, CombatReady, CombatAttackLimit, Frontline, FrontlineRules, BlockedAttackPractice, BlockedAttackResult, RangedSummon, RangedAttackPractice, RangedCounter, MeleeCounter, MeleeCounterPractice, GolemTurn, DamageTypes, AttackTypeIcon, DamageTypeColor, GolemAttackPractice, PhysicalDefense, MagicDefense, FixedDamage, SpellPowerPractice, DamageModifiers, FireballIntro, FireballCost, FireballPower, FireballResult, OccupantPreviewPractice, PreviewStatuses, PreviewInfluences, UnitMovePractice, UnitSwap, BuildingMovePractice, BuildingMoveResult, DeckExhaustion, DrawFailureFirst, DrawFailureTiming, VictorySetup, VictoryAttack, PracticeVictory, PracticeVictoryIsolation, ShopIntro, ShopAds, ShopTickets, ShopPacks, ShopDuplicates, ShopAccountGold, CollectionIntro, CollectionEligibility, CollectionDetail, UpgradeTableFlip, UpgradeTableExplanation, UpgradeVariations, UpgradeTableReturn, UpgradePracticeIntro, UpgradePracticeCost, UpgradePracticeResult, UpgradeMaximum, UpgradeAccountIsolation, TutorialConclusion, TutorialStrategy, TutorialFarewell
    }

    public sealed class TutorialStep
    {
        public TutorialStep(string dialogue, TutorialCue cue, TutorialAction requiredAction = TutorialAction.None)
        { Dialogue = dialogue; Cue = cue; RequiredAction = requiredAction; }
        public string Dialogue { get; }
        public TutorialCue Cue { get; }
        public TutorialAction RequiredAction { get; }
    }

    // An ephemeral lesson, not an account/run store. Re-entering always starts from the same state.
    public sealed class TutorialSession
    {
        public const int FirstChapterStep = 5;
        public const int SecondChapterStep = 15;
        public const int ThirdChapterStep = 30;
        public const int FourthChapterStep = 45;
        public const int FifthChapterStep = 71;
        public const int SixthChapterStep = 110;
        public const int ManualDraftPicks = 5;
        private static readonly TutorialStep[] Steps =
        {
            new TutorialStep("어서 오게! 나는 개방 방주라네. 자네에게 이 세계에서 살아남는 법을 알려주러 왔지.", TutorialCue.Introduction),
            new TutorialStep("원래 이런 가르침에는 골드가 필요한 법이지만… 이번에는 특별히 공짜로 해주겠네.", TutorialCue.Introduction),
            new TutorialStep("가르침이 필요없다면, 왼쪽 상단‘스킵’을 누르면 바로 연습을 끝낼 수 있네.", TutorialCue.Controls),
            new TutorialStep("또한 ‘튜토리얼 선택’으로 원하는 주제부터 배울 수 있네. 그럼, 시작해볼까?", TutorialCue.Controls),
            new TutorialStep("참고로 여기는 연습 공간이라네. 티켓과 골드를 쓰거나 카드를 강화해도, 실제 계정에는 아무 변화가 없으니 안심하게.", TutorialCue.Isolation),
            new TutorialStep("이 세계에는 각각 판타지 문명, 무림 문명, 과학 문명을 이룩한 세 대륙이 있네.", TutorialCue.Civilizations),
            new TutorialStep("판타지는 마나를, 무림은 기를, 과학 문명은 전력을 자원으로 사용하지. 각 대륙의 카드를 쓰려면 그에 맞는 자원이 필요하네.", TutorialCue.Resources),
            new TutorialStep("그리고 어디에서나 통하는 자원이 있으니… 바로 골드라네!", TutorialCue.Gold),
            new TutorialStep("마나나 기, 전력이 부족하면 골드로 대신 지불할 수 있지. 부족한 자원 하나를 골드 하나로 바꾸는 셈이네.", TutorialCue.Substitution),
            new TutorialStep("서로 다른 대륙의 카드를 함께 쓰는 것도 가능하네. 다만, 카드를 쓸 자원을 어떻게 마련할지 함께 생각해야 하지.", TutorialCue.MixedDeck),
            new TutorialStep("카드는 크게 유닛, 건물, 마법으로 나뉘네. 싸울 병력을 소환하고, 필요한 자원을 마련하고, 마법으로 전세를 바꾸는 것이지.", TutorialCue.CardKinds),
            new TutorialStep("자네는 제시되는 카드 중에서 하나씩 골라, 모두 33장으로 이루어진 덱을 만들게 되네.", TutorialCue.Deck),
            new TutorialStep("그 덱으로 전투를 거듭해 33승을 달성하거나, 3패를 기록할 때까지 도전하지. 이 한 차례의 도전을 ‘런’이라고 부르네.", TutorialCue.Run),
            new TutorialStep("새로운 런을 시작하려면 게임 티켓 3개가 필요하네. ‘게임 시작’을 누르면 티켓을 지불하고 덱을 만들기 시작하지.", TutorialCue.StartPractice),
            new TutorialStep("이번에는 연습용 티켓을 준비해두었네. 실제 티켓은 줄어들지 않으니, ‘게임 시작’을 눌러보게.", TutorialCue.StartPractice, TutorialAction.StartRun),
            new TutorialStep("덱 빌딩 단계에서는 매번 무작위로 카드 세 장이 제시되고, 그중 한 장을 선택할 수 있네.", TutorialCue.DraftOffers),
            new TutorialStep("33번의 선택을 통해 33장의 덱을 만드는데, 그 중 1번의 선택은 레전더리 카드를 고르는 선택이라네.", TutorialCue.LegendaryOpening),
            new TutorialStep("카드의 효과에 쓰여진 글자 중 굵게 쓰여진 글자는 특수효과라네.", TutorialCue.SpecialEffects),
            new TutorialStep("특수효과를 자세히 알고 싶다면 카드를 꾹 눌러보게. 손을 떼면 미리보기가 닫히네.", TutorialCue.OfferPreview, TutorialAction.PreviewOffer),
            new TutorialStep("선택하려면 짧게 누르면 되네. 살펴보는 것과 고르는 것은 다른 동작이라는 점을 기억하게. 원하는 카드를 선택해보게.", TutorialCue.OfferPick, TutorialAction.PickCard),
            new TutorialStep("카드 아래쪽의 보석은 희귀도를 나타내네. 하얀색-커먼, 초록색-언커먼, 파란색-레어, 빨간색-유니크, 노란색-레전더리로 커먼, 언커먼, 레어, 유니크, 레전더리 순으로 희귀도가 높지.", TutorialCue.RarityCommonUncommon),
            new TutorialStep("희귀한 카드라고 해서 언제나 자네 덱에 가장 좋은 선택은 아니네. 비용과 역할, 다른 카드와의 조합도 살펴보게.", TutorialCue.RarityRole),
            new TutorialStep("고른 카드는 오른쪽의 덱 목록에 쌓이네. 이렇게 한 장씩 선택해서 33장을 채우는 것이지.", TutorialCue.DeckList),
            new TutorialStep("목록에 있는 카드도 꾹 누르면 다시 살펴볼 수 있네. 방금 고른 카드부터 확인해보게.", TutorialCue.DeckPreview, TutorialAction.PreviewDeck),
            new TutorialStep("레전더리 카드 선택은 덱 빌딩 중 딱 한 번만 등장하네. 레전더리를 제외한 희귀도는 같은 카드를 최대 세 장까지 넣을 수 있지.", TutorialCue.CopyLimits),
            new TutorialStep("덱을 완성하면 이번 런이 끝날 때까지 그 덱을 사용하게 되네. 완성한 뒤에는 구성을 바꿀 수 없으니 신중히 고르게.", TutorialCue.LockedDeck),
            new TutorialStep("이제 카드를 몇 장 더 원하는대로 골라보게.", TutorialCue.ManualDraftPick, TutorialAction.PickCard),
            new TutorialStep("잘했네! 벌써 다섯 장이나 골랐군.", TutorialCue.DraftFivePicked),
            new TutorialStep("연습에 필요한 나머지 28장은 내가 골라주겠네. 실제 게임에서는 자네가 끝까지 직접 선택해야 하네.", TutorialCue.AutoDraft, TutorialAction.CompleteDeck),
            new TutorialStep("연습 덱 33장이 완성되었네. 이제 덱 목록을 확인할 수 있지.", TutorialCue.DraftReady),
            new TutorialStep("이제 덱이 완성되었네. 이 화면에서 덱을 확인하고, 어떤 전투에 나갈지 고를 수 있지.", TutorialCue.GameModes),
            new TutorialStep("카드 내용이 기억나지 않으면 목록을 꾹 눌러보게. 전투에 들어가기 전에 덱을 살펴두면 도움이 될 걸세. 표시한 카드를 꾹 누르고 놓아보게.", TutorialCue.LobbyDeckPreview, TutorialAction.PreviewDeck),
            new TutorialStep("이제 게임 모드에 대해 알려주겠네.‘전투’는 다른 플레이어와 일대일로 겨루는 모드라네.", TutorialCue.PvpMode),
            new TutorialStep("전투 버튼을 누르면 나와 겨룰 다른 플레이어를 찾고, 매칭이 되면 그 플레이어와 전투하지.", TutorialCue.PvpMode),
            new TutorialStep("전투에서 이기면 Win이 하나, 지면 Loss가 하나 올라가네.", TutorialCue.RunRecord),
            new TutorialStep("이번 런에서 누적 33승을 달성하거나, 누적 3패를 기록하면 런이 끝나는 것이지.", TutorialCue.RunRecord),
            new TutorialStep("‘골드 던전’은 AI와 싸워 골드를 얻는 모드라네. 게임 티켓 세 개로 시작한 런마다 최대 세 번 도전할 수 있지.", TutorialCue.GoldDungeon),
            new TutorialStep("승리하면 상점과 강화에 쓸 수 있는 10골드를 얻네. 골드라니… 아주 훌륭한 보상이지! 전투 안에서 쓰는 골드와는 별개라네.", TutorialCue.GoldDungeonReward),
            new TutorialStep("골드 던전의 결과는 이번 런의 Win과 Loss를 바꾸지 않네. 던전 입장마다 추가 티켓은 필요없는 보너스 스테이지지. 런이 끝나고 티켓 3개로 새로운 런을 시작하면, 그 런에서 다시 3번 도전할 수 있네.", TutorialCue.GoldDungeonRules),
            new TutorialStep("‘골드 던전 1/3’이라고 표시되어 있으면 이번 런에서 도전 가능 횟수가 한 번 남았다는 뜻이네. 이미 한 번 사용했다는 뜻은 아니지. 남은 횟수는 다음 런으로 가져갈 수 없네.", TutorialCue.GoldDungeonRemaining),
            new TutorialStep("전투 중 연결이 끊겼다면 서둘러 돌아오게. 서버가 연결 끊김을 감지한 뒤 70초 동안은 ‘재접속’으로 같은 전투에 복귀할 수 있네. 지금 보이는 재접속 버튼은 연습용 예시라네.", TutorialCue.Reconnect),
            new TutorialStep("다만, 그동안 턴 시간과 상대의 행동은 계속 진행되네. 오래 자리를 비워도 전투가 기다려주지는 않지.", TutorialCue.Reconnect),
            new TutorialStep("이제 골드 던전으로 전투를 배워보세. 이번 연습에서는 실제 도전 횟수가 줄어들지 않네.", TutorialCue.GoldDungeonPractice),
            new TutorialStep("초록색으로 표시한 ‘골드 던전’ 버튼을 눌러보게. 실제 계정과 연결되지 않은 연습 전투로 들어가겠네.", TutorialCue.GoldDungeonPractice, TutorialAction.StartGoldDungeon),
            new TutorialStep("잘했네! 연습 전투에 들어왔군. 다음에는 자원과 전투를 배워보세.", TutorialCue.BattleEntry),
            new TutorialStep("전투를 시작하기 전에는 처음 받은 카드 다섯 장 중에서 바꾸고 싶은 카드를 고를 수 있네. 이것을 ‘멀리건’이라고 하지.", TutorialCue.MulliganIntro),
            new TutorialStep("이번에는 표시한 파이어볼을 눌러보게. 붉은 X와 ‘교체’가 표시되면 바꿀 카드로 선택된 것이네. 다시 누르면 취소할 수 있지.", TutorialCue.MulliganSelection, TutorialAction.SelectMulliganCard),
            new TutorialStep("좋네. 이제 아래의 ‘확인’을 눌러보게. 고른 카드들만 새 카드로 바꾸고, 나머지는 그대로 가져가네.", TutorialCue.MulliganConfirm, TutorialAction.ConfirmMulligan),
            new TutorialStep("전투의 목표는 상대 마스터 유닛의 체력을 0 이하로 만드는 것이네. 반대로 자네 마스터가 쓰러지면 패배하지.", TutorialCue.MasterObjective),
            new TutorialStep("마스터는 체력 333, 공격력 3으로 시작하네. 마스터도 직접 공격하거나 이동할 수 있으니, 가만히 서 있는 장식은 아니라네.", TutorialCue.MasterStats),
            new TutorialStep("전투에서는 서로 번갈아 턴을 진행하네. 내 턴에 카드를 사용하고, 유닛을 움직이고, 공격할 수 있지.", TutorialCue.BattleTurns),
            new TutorialStep("화면 왼쪽의 타일들은 자네의 전장이고, 오른쪽의 타일들은 상대의 전장이라네. 자네의 유닛과 건물은 왼쪽 전장에 소환하게 되지.", TutorialCue.Battlefields),
            new TutorialStep("왼쪽 아래에는 자네가 가진 자원이 표시되네. 마나, 기, 전력, 골드의 순서라네. 전투를 시작하면 선공 플레이어는 3골드, 후공은 4골드를 보유하고 시작하네.", TutorialCue.PlayerResources),
            new TutorialStep("왼쪽 위는 상대의 자원이지. 상대가 무엇을 사용할 수 있을지 짐작하는 데 도움이 되네.", TutorialCue.OpponentResources),
            new TutorialStep("내 턴이 시작되면 덱에서 카드 한 장을 뽑네. 양쪽 모두 첫 턴에는 골드를 추가로 받지 않으니 선공은 골드 3, 후공은 4로 시작하지. 그다음 자기 턴부터는 기본 골드를 3씩 얻네.", TutorialCue.TurnIncome),
            new TutorialStep("마나와 기, 전력은 기본적으로 매 턴 저절로 늘어나지 않네. 자원을 만들어주는 카드나 효과를 마련해야 하지.", TutorialCue.UniqueResources),
            new TutorialStep("남은 자원은 다음 턴에도 유지되네. 당장 전부 쓸 필요는 없다는 말이지.", TutorialCue.CarryResources),
            new TutorialStep("여기서 알아야 할 것이 있네. 전투에서 쓰는 골드와 상점·강화에 쓰는 골드는 서로 별개라네.", TutorialCue.GoldSeparation),
            new TutorialStep("전투 골드를 쓴다고 계정의 골드가 줄어들지는 않지. 새로운 전투가 시작되면 전투 자원도 새로 시작하네.", TutorialCue.GoldSeparation),
            new TutorialStep("이번에는 소환과 자원 생산을 바로 배울 수 있도록 연습용 마나 3과 골드 5를 더 준비해두었네. 실제 전투에서도 이렇게 많이 시작하는 것은 아니니 기억하게.", TutorialCue.PracticeGrant),
            new TutorialStep("아래쪽에 모여 있는 카드가 자네의 손패라네. 먼저 눌러서 펼쳐보게.", TutorialCue.HandExpand, TutorialAction.ExpandHand),
            new TutorialStep("카드의 왼쪽 상단에 쓰여있는 것이 그 카드의 비용이네. 비용을 낼 수 있는 카드를 선택하면 사용할 수 있는 타일이 밝게 깜빡이네. 고블린 카드를 선택해보게. ", TutorialCue.GoblinSelection, TutorialAction.SelectGoblin),
            new TutorialStep("고블린의 비용은 마나 2. 지금 가진 마나 3 중에서 둘을 먼저 지불하니, 마나 1과 골드 8이 남게 되지.", TutorialCue.GoblinCost),
            new TutorialStep("깜빡이는 빈 타일을 눌러 소환해보게. 이미 소환물이 있는 타일에는 다른 소환물을 놓을 수 없네.", TutorialCue.GoblinSummon, TutorialAction.SummonGoblin),
            new TutorialStep("잘했네. 유닛은 기본적으로 소환된 턴에는 공격할 수 없네. 다만, 예외적으로 ‘속공’ 특수효과가 있으면 소환된 턴에도 즉시 공격할 수 있네.", TutorialCue.SummoningSickness),
            new TutorialStep("이번에는 자원을 생산하는 건물을 세워보세. ‘마나의 샘’을 선택하게.", TutorialCue.PondSelection, TutorialAction.SelectManaPond),
            new TutorialStep("초록색으로 표시한 빈 타일을 눌러 ‘마나의 샘’을 소환해보게. 비용은 마나 6이지만, 남은 마나 1을 먼저 쓰고 부족한 5는 골드 5로 대신 지불하네. 마나가 부족하면 골드로 대신 낼 수 있는 것이지.", TutorialCue.PondSummon, TutorialAction.SummonManaPond),
            new TutorialStep("마나의 샘은 자네의 턴이 시작될 때 마나를 3씩 생산하네. 이런 생산 시설을 마련하면 이후에 더 많은 카드를 사용할 수 있지.", TutorialCue.ResourceProduction),
            new TutorialStep("준비를 마쳤다면 ‘턴 종료’를 눌러보게. 상대의 턴이 지나면 자네의 다음 턴이 돌아오네.", TutorialCue.PracticeEndTurn, TutorialAction.EndTurn),
            new TutorialStep("자네의 턴이 돌아왔군! 기본 골드 3을 얻고, 마나의 샘에서 마나 3도 얻었네.", TutorialCue.ResourcesReturned),
            new TutorialStep("또 카드 한 장을 드로우해서 파이어볼 카드가 핸드에 들어왔네. 내 턴마다 이렇게 다음 행동을 준비하게 되는 것이네. 다음은 공격과 이동, 마법을 배워보세.", TutorialCue.DrawReturned),
            new TutorialStep("보통 유닛은 소환한 턴에는 공격할 수 없네. 다음 내 턴이 되어야 공격할 수 있지. 다만, ‘속공’이 있는 유닛은 소환한 턴에도 공격할 수 있네.", TutorialCue.CombatReady),
            new TutorialStep("고블린은 지난 턴에 소환했으니 이제 공격할 수 있네. 일반적으로 유닛은 내 턴에 1번 공격할 수 있지.", TutorialCue.CombatAttackLimit),
            new TutorialStep("그런데 상대 마스터 앞에 유닛이 서 있군. 이런 배치에서는 ‘전열 차단’을 알아야 하네.", TutorialCue.Frontline),
            new TutorialStep("앞칸의 소환물이 전열을 차단하면, 같은 열의 뒤칸은 근접 공격으로 직접 공격할 수 없네.", TutorialCue.FrontlineRules),
            new TutorialStep("앞칸 하나가 뒷줄 전체를 지켜주는 것은 아니네. 바로 뒤의 같은 열만 보호하는 것이지. 고블린으로 상대 마스터를 공격 시도해보게.", TutorialCue.BlockedAttackPractice, TutorialAction.AttemptBlockedAttack),
            new TutorialStep("보았나? 고블린은 근접 유닛이라 앞의 적을 먼저 상대해야 하네.", TutorialCue.BlockedAttackResult),
            new TutorialStep("이번에는 원거리 유닛을 써보세. 스켈레톤아처를 소환하고, 다음 내 턴까지 기다려보게.", TutorialCue.RangedSummon, TutorialAction.SummonArcherAndEndTurn),
            new TutorialStep("원거리 공격은 전열 차단을 무시하고 뒤쪽의 적을 공격할 수 있네. 마스터 앞에 적이 있어도 공격이 닿는 것이지.", TutorialCue.RangedAttackPractice, TutorialAction.RangedMasterAttack),
            new TutorialStep("대신 원거리 유닛은 반격이 불가능하네.", TutorialCue.RangedCounter),
            new TutorialStep("그래서 오직 근접 유닛끼리 싸울 때만 반격이 발생하지. 반격이 발생하면 공격자도 피격자의 공격력만큼 피해를 입네.", TutorialCue.MeleeCounter),
            new TutorialStep("두 유닛의 피해가 함께 적용되므로, 적을 쓰러뜨리는 공격이라도 자네 유닛이 반격으로 함께 쓰러질 수 있네.", TutorialCue.MeleeCounterPractice, TutorialAction.MeleeCounterattack),
            new TutorialStep("다음은 데미지의 속성에 대해 알려주겠네. 턴을 종료해보게.", TutorialCue.GolemTurn, TutorialAction.EndTurnForGolem),
            new TutorialStep("공격 방식과 피해 종류는 따로 살펴봐야 하네. 근접이라고 반드시 물리 피해인 것도, 원거리라고 반드시 마법 피해인 것도 아니지.", TutorialCue.DamageTypes),
            new TutorialStep("카드 이미지의 비용 부분의 아래쪽에 공격 방식과 데미지 속성 문양이 그려져있네. 검이면 근접 공격, 활이나 지팡이라면 원거리 공격 유형이라네.", TutorialCue.AttackTypeIcon),
            new TutorialStep("또한 무기의 색깔이 빨간색이면 물리 데미지를, 파란색이면 마법 데미지를 입히네. 가끔 무기의 색깔이 흰색인 경우가 있는데, 그런 카드는 방어력을 무시하는 고정 데미지를 입히네.", TutorialCue.DamageTypeColor),
            new TutorialStep("스켈레톤 아처로 골렘을 공격해보게.", TutorialCue.GolemAttackPractice, TutorialAction.AttackGolem),
            new TutorialStep("물리 피해는 물리 방어력으로 줄어드네. 물리 피해 10을 물리 방어력 3인 적에게 주면, 실제 피해는 7이지.", TutorialCue.PhysicalDefense),
            new TutorialStep("같은 식으로 마법 피해는 마법 방어력으로 줄어드네. 물리 방어력이 높아도 마법 피해를 줄여주지는 않지.", TutorialCue.MagicDefense),
            new TutorialStep("고정 피해는 물리 방어력과 마법 방어력을 무시하네. 다만, 무적 같은 별도의 보호 효과까지 무조건 무시하는 것은 아니지.", TutorialCue.FixedDamage),
            new TutorialStep("마나의 샘에는 주문력을 높이는 효과도 있네. 주문력은 마법 피해를 더 강하게 만들어주지.", TutorialCue.SpellPowerPractice),
            new TutorialStep("피해가 예상과 다르다면 공격력뿐 아니라 방어력과 적용 중인 효과도 함께 확인하게.", TutorialCue.DamageModifiers),
            new TutorialStep("이제 스펠 카드도 직접 써보세. 핸드의 ‘파이어볼’을 상대방 마스터에게 사용해보게.", TutorialCue.FireballIntro),
            new TutorialStep("파이어볼은 마나 하나를 쓰고, 마법 피해 10을 입히네. 당연히 마나가 부족하면 골드로 대신 지불할 수 있지.", TutorialCue.FireballCost),
            new TutorialStep("지금은 마나의 샘 덕분에 주문력이 20 높아졌네. 상대의 마법 방어력이 0이니, 이번 파이어볼은 10에 20을 더한 피해 30을 입히는 것이지.", TutorialCue.FireballPower, TutorialAction.CastPracticeFireball),
            new TutorialStep("잘했네! 스펠 카드의 비용과 대상, 카드를 꾹 눌러서 적용되고 있는 주문력을 함께 살펴보면 더 효과적으로 싸울 수 있지.", TutorialCue.FireballResult),
            new TutorialStep("소환물을 꾹 누르면 카드와 현재 상태를 자세히 볼 수 있네. 아무 소환물이나 길게 눌러보게.", TutorialCue.OccupantPreviewPractice, TutorialAction.PreviewOccupant),
            new TutorialStep("카드 양옆에서는 카드의 특수효과를, 아래에서는 붙어 있는 버프와 디버프를 확인할 수 있네.", TutorialCue.PreviewStatuses),
            new TutorialStep("위쪽에는 주문력이나 전장 효과처럼 그 소환물에게 영향을 주는 효과가 표시되지. 상황을 판단할 때 자주 확인하게.", TutorialCue.PreviewInfluences),
            new TutorialStep("유닛의 위치를 바꾸고 싶다면 유닛을 선택한 뒤 내 빈 타일을 누르면 되네. 스켈레톤 아처를 표시한 자리로 옮겨보게.", TutorialCue.UnitMovePractice, TutorialAction.MovePracticeUnit),
            new TutorialStep("다른 이동 가능한 아군 유닛이 있는 타일을 선택하면, 두 유닛의 자리를 서로 바꿀 수도 있네.", TutorialCue.UnitSwap, TutorialAction.SwapPracticeUnits),
            new TutorialStep("이번에는 건물인 마나의 샘을 옮겨보게.", TutorialCue.BuildingMovePractice, TutorialAction.AttemptBuildingMove),
            new TutorialStep("건물은 이동할 수 없네. 처음 놓을 때부터 자리를 잘 생각해야 한다는 뜻이지.", TutorialCue.BuildingMoveResult),
            new TutorialStep("마지막으로 덱이 바닥났을 때를 알아두게. 덱이 비면, 카드를 뽑으려 할 때마다 마스터가 고정 피해를 받네.", TutorialCue.DeckExhaustion),
            new TutorialStep("처음에는 3, 다음에는 6, 그다음에는 9… 뽑지 못할수록 피해가 3씩 커지지.", TutorialCue.DrawFailureFirst),
            new TutorialStep("덱이 비는 순간 바로 피해를 받는 것은 아니네. 빈 덱에서 카드를 뽑으려 할 때 발생하는 것이지.", TutorialCue.DrawFailureTiming),
            new TutorialStep("이제 마지막 공격을 해보세. 연습을 빨리 마칠 수 있도록 상대 마스터의 체력을 낮춰두었네. 실제 시작 체력은 333이라는 점을 기억하게.", TutorialCue.VictorySetup),
            new TutorialStep("스켈레톤아처로 상대 마스터를 공격해보게!", TutorialCue.VictoryAttack, TutorialAction.FinishPracticeBattle),
            new TutorialStep("잘했네! 상대 마스터를 쓰러뜨렸군.", TutorialCue.PracticeVictory),
            new TutorialStep("실제 골드 던전이라면 승리 보상을 받겠지만, 이번 연습은 계정의 골드나 전적을 바꾸지 않네.", TutorialCue.PracticeVictoryIsolation),
            new TutorialStep("전투를 마쳤으니 준비를 더 단단히 하는 방법도 알아보세. 먼저 상점이라네.", TutorialCue.ShopIntro),
            new TutorialStep("광고를 끝까지 보고 보상이 확인되면 게임 티켓 하나를 얻을 수 있네. 이번 설명에서는 실제 광고를 재생하지 않겠네.", TutorialCue.ShopAds),
            new TutorialStep("골드로 게임 티켓을 구매할 수도 있지. 새 런에 필요한 티켓이 부족하면 이곳을 찾아오게.", TutorialCue.ShopTickets),
            new TutorialStep("카드팩은 골드로 구매할 수 있네. 팩 하나에서 무작위 카드 열 장을 얻지.", TutorialCue.ShopPacks),
            new TutorialStep("같은 카드가 여러 장 나올 수도 있네. 얻은 카드는 보유 카드에 추가되고, 나중에 카드 업그레이드에 사용할 수 있네. 지금 사용하는 런 덱에 자동으로 들어가는 것이 아니라네.", TutorialCue.ShopDuplicates),
            new TutorialStep("이곳에서 쓰는 골드는 계정에 모아둔 골드라네. 전투 화면의 골드와는 별개라는 점, 기억하고 있겠지?", TutorialCue.ShopAccountGold),
            new TutorialStep("이제 ‘보유 카드’를 살펴보세. 여기에서는 가지고 있는 카드의 수와 강화 레벨을 확인할 수 있네.", TutorialCue.CollectionIntro),
            new TutorialStep("강화할 수 있는 카드도 있고, 강화할 수 없는 카드도 있지. 원하는 카드를 누르면 상세 창이 열리네. 고블린 카드를 눌러보게.", TutorialCue.CollectionEligibility, TutorialAction.SelectUpgradeCard),
            new TutorialStep("상세 창에서는 현재 능력치와 효과, 보유 수량, 다음 강화에 필요한 카드와 골드를 확인할 수 있네.", TutorialCue.CollectionDetail),
            new TutorialStep("카드 그림을 눌러보게. 그림 뒤에 강화 정보를 적어두었네.", TutorialCue.UpgradeTableFlip, TutorialAction.ShowUpgradeTable),
            new TutorialStep("이 표는 강화할 때마다 레벨별로 늘어나는 능력치를 보여주네. 예를 들어 Lv.0에서 Lv.1이 될 때는 Hp+1, Lv.2에서 Lv.3이 될 때는 ATK+1이 오르지.", TutorialCue.UpgradeTableExplanation),
            new TutorialStep("모든 카드가 같은 방식으로 강해지는 것은 아니네. 공격력이나 체력, 마법의 효과 수치 등 무엇이 늘어나는지 확인하게.", TutorialCue.UpgradeVariations),
            new TutorialStep("같은 영역을 다시 누르면 카드 이미지로 돌아오네.", TutorialCue.UpgradeTableReturn, TutorialAction.RestoreCardArtwork),
            new TutorialStep("강화에는 해당 카드와 골드가 함께 필요하네. 이번에는 고블린을 레벨 0에서 레벨 1로 강화해보세.", TutorialCue.UpgradePracticeIntro),
            new TutorialStep("필요한 것은 고블린 카드 3장과 3골드라네. 모두 연습용으로 준비해두었지.", TutorialCue.UpgradePracticeCost, TutorialAction.UpgradePracticeCard),
            new TutorialStep("강화에 쓴 카드와 골드가 줄고, 고블린의 체력이 1 늘었군!", TutorialCue.UpgradePracticeResult),
            new TutorialStep("더 높은 레벨로 강화할수록 필요한 수량도 달라지네. 최대 강화 레벨은 13이고, 매번 상세 창에서 다음 비용과 증가량을 확인하면 되네.", TutorialCue.UpgradeMaximum),
            new TutorialStep("실제 게임에서 강화하면 그 카드의 강화 정보가 계정에 남네. 방금 한 연습 강화는 실제 카드에 적용되지 않으니 안심하게.", TutorialCue.UpgradeAccountIsolation),
            new TutorialStep("이제 덱을 만들고, 자원을 모으고, 마스터를 지키며 싸우는 법을 배웠네.", TutorialCue.TutorialConclusion),
            new TutorialStep("강한 카드 한 장만 바라보지 말게. 자원과 배치, 공격할 순서까지 잘 생각해야 승리를 얻을 수 있지.", TutorialCue.TutorialStrategy),
            new TutorialStep("그럼, 자네의 도전을 응원하겠네. 골드도 많이 모으고 말이야!", TutorialCue.TutorialFarewell),
        };

        // Offers are authored for the lesson. No random source, live draft service or account data is used.
        private static readonly IReadOnlyList<string>[] PracticeOffers =
        {
            Array.AsReadOnly(new[] { "BlueDragon", "RedDragon", "Nuclear" }),
            Array.AsReadOnly(new[] { "Goblin", "ManaPond", "firebolt" }),
            Array.AsReadOnly(new[] { "SkeletonArcher", "ManaCannon", "GaebangBranch" }),
            Array.AsReadOnly(new[] { "OrcBerserker", "PlasmaShield", "ManaShield" }),
            Array.AsReadOnly(new[] { "ElfLongbowScout", "PowerPlant", "A-301" }),
            Array.AsReadOnly(new[] { "Goblin", "ManaPond", "firebolt" }),
        };
        private static readonly string[] AutomaticPool =
        {
            "Goblin", "ManaPond", "firebolt", "SkeletonArcher", "ManaCannon", "GaebangBranch",
            "OrcBerserker", "PlasmaShield", "ManaShield", "ElfLongbowScout", "PowerPlant", "A-301", "Gwangma"
        };
        private static readonly IReadOnlyList<string> EmptyCards = Array.AsReadOnly(Array.Empty<string>());
        private DraftDeckState _deck = new DraftDeckState();
        private IReadOnlyList<string> _draftCards = EmptyCards;

        public int StepIndex { get; private set; }
        public int StepCount => Steps.Length;
        public int Chapter => StepIndex < FirstChapterStep ? 0 : StepIndex < SecondChapterStep ? 1 :
            StepIndex < ThirdChapterStep ? 2 : StepIndex < FourthChapterStep ? 3 : StepIndex < FifthChapterStep ? 4 : StepIndex < SixthChapterStep ? 5 : 6;
        public TutorialStep CurrentStep => Steps[StepIndex];
        public TutorialUpgradePractice UpgradePractice { get; private set; } = new TutorialUpgradePractice();
        public int Revision { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsExited { get; private set; }
        public int PracticeTickets { get; private set; } = 3;
        public bool TutorialRunStarted { get; private set; }
        public int ManualPickCount { get; private set; }
        public bool IsDraftReady => _deck.IsComplete;
        public bool PracticeBattleStarted { get; private set; }
        public int PracticeGoldDungeonRemaining { get; private set; } = 3;
        public bool IsLegendaryOpeningOffer => TutorialRunStarted && ManualPickCount == 0 && !IsDraftReady;
        public IReadOnlyList<string> DraftCardIds => _draftCards;
        public IReadOnlyList<string> CurrentOfferCardIds => TutorialRunStarted && !IsDraftReady
            ? PracticeOffers[Math.Min(ManualPickCount, ManualDraftPicks)] : EmptyCards;
        public bool WaitingForAction => !IsComplete && !IsExited && CurrentStep.RequiredAction != TutorialAction.None;

        public bool TryAdvance()
        {
            if (IsComplete || IsExited || WaitingForAction || StepIndex >= Steps.Length - 1) return false;
            StepIndex++;
            if (StepIndex == Steps.Length - 1) IsComplete = true;
            return true;
        }

        // Preview actions are submitted by presentation only after a successful hold AND release.
        // Opening/canceling a preview and short clicks must not submit those actions.
        public bool PerformAction(TutorialAction action, int cardIndex = -1)
        {
            if (!WaitingForAction || action == TutorialAction.None || action != CurrentStep.RequiredAction) return false;
            if (action == TutorialAction.UpgradePracticeCard &&
                (UpgradePractice.Level != 1 || UpgradePractice.Copies != 0 || UpgradePractice.Gold != 0)) return false;
            switch (action)
            {
                case TutorialAction.StartRun:
                    if (PracticeTickets < 3 || TutorialRunStarted) return false;
                    BeginPracticeRun();
                    StepIndex = SecondChapterStep;
                    return true;
                case TutorialAction.PreviewOffer:
                    if (cardIndex < 0 || cardIndex >= CurrentOfferCardIds.Count) return false;
                    StepIndex++;
                    return true;
                case TutorialAction.PreviewDeck:
                    if (cardIndex < 0 || cardIndex >= _draftCards.Count) return false;
                    StepIndex++;
                    return true;
                case TutorialAction.PickCard:
                    if (cardIndex < 0 || cardIndex >= CurrentOfferCardIds.Count) return false;
                    var cardId = CurrentOfferCardIds[cardIndex];
                    var rarity = PracticeRarity(cardId);
                    if (!_deck.CanAddCard(cardId, rarity)) return false;
                    _deck.AddCard(cardId, rarity);
                    ManualPickCount++;
                    RefreshDraftSnapshot();
                    if (CurrentStep.Cue == TutorialCue.OfferPick || ManualPickCount == ManualDraftPicks) StepIndex++;
                    return true;
                case TutorialAction.CompleteDeck:
                    if (ManualPickCount != ManualDraftPicks || _deck.Count != ManualDraftPicks) return false;
                    CompletePracticeDeck();
                    StepIndex++;
                    return true;
                case TutorialAction.StartGoldDungeon:
                    if (!IsDraftReady || PracticeBattleStarted || PracticeGoldDungeonRemaining <= 0) return false;
                    PracticeGoldDungeonRemaining--;
                    PracticeBattleStarted = true;
                    StepIndex++;
                    return true;
                case TutorialAction.SelectMulliganCard:
                case TutorialAction.ConfirmMulligan:
                case TutorialAction.ExpandHand:
                case TutorialAction.SelectGoblin:
                case TutorialAction.SummonGoblin:
                case TutorialAction.SelectManaPond:
                case TutorialAction.SummonManaPond:
                case TutorialAction.EndTurn:
                case TutorialAction.AttemptBlockedAttack:
                case TutorialAction.SummonArcherAndEndTurn:
                case TutorialAction.RangedMasterAttack:
                case TutorialAction.MeleeCounterattack:
                case TutorialAction.EndTurnForGolem:
                case TutorialAction.AttackGolem:
                case TutorialAction.CastPracticeFireball:
                case TutorialAction.PreviewOccupant:
                case TutorialAction.MovePracticeUnit:
                case TutorialAction.SwapPracticeUnits:
                case TutorialAction.AttemptBuildingMove:
                case TutorialAction.FinishPracticeBattle:
                case TutorialAction.SelectUpgradeCard:
                case TutorialAction.ShowUpgradeTable:
                case TutorialAction.RestoreCardArtwork:
                case TutorialAction.UpgradePracticeCard:
                    StepIndex++;
                    return true;
                default: return false;
            }
        }

        public void Restart()
        {
            Revision++;
            UpgradePractice = new TutorialUpgradePractice();
            StepIndex = 0; PracticeTickets = 3; TutorialRunStarted = false; IsComplete = false; IsExited = false;
            ManualPickCount = 0; _deck = new DraftDeckState(); _draftCards = EmptyCards;
            PracticeBattleStarted = false; PracticeGoldDungeonRemaining = 3;
        }

        public bool SelectChapter(int chapter)
        {
            if (chapter < 1 || chapter > 6) return false;
            Restart();
            if (chapter == 6) { StepIndex = SixthChapterStep; return true; }
            if (chapter == 1) StepIndex = FirstChapterStep;
            else
            {
                BeginPracticeRun();
                StepIndex = SecondChapterStep;
                if (chapter >= 3)
                {
                    // Direct entry always uses the same five authored choices.
                    for (var i = 0; i < ManualDraftPicks; i++)
                    {
                        var id = PracticeOffers[i][0];
                        _deck.AddCard(id, PracticeRarity(id));
                    }
                    ManualPickCount = ManualDraftPicks;
                    CompletePracticeDeck();
                    StepIndex = ThirdChapterStep;
                    if (chapter >= 4)
                    { PracticeBattleStarted = true; PracticeGoldDungeonRemaining = 2; StepIndex = chapter == 4 ? FourthChapterStep : FifthChapterStep; }
                }
            }
            return true;
        }

        public bool CancelMulliganSelection()
        {
            if (IsExited || CurrentStep.RequiredAction != TutorialAction.ConfirmMulligan) return false;
            StepIndex--;
            return true;
        }

        public void Skip() { IsExited = true; }

        private void CompletePracticeDeck()
        {
            // Preserve the player's choices and skip cards at the shared copy cap.
            while (!_deck.IsComplete)
                foreach (var id in AutomaticPool)
                {
                    if (_deck.IsComplete) break;
                    var rarity = PracticeRarity(id);
                    if (_deck.CanAddCard(id, rarity)) _deck.AddCard(id, rarity);
                }
            RefreshDraftSnapshot();
        }

        private void BeginPracticeRun() { PracticeTickets -= 3; TutorialRunStarted = true; }
        private void RefreshDraftSnapshot()
        {
            var cards = new string[_deck.Count];
            for (var i = 0; i < cards.Length; i++) cards[i] = _deck.CardIds[i];
            _draftCards = Array.AsReadOnly(cards);
        }
        private static CardRarity PracticeRarity(string cardId)
        {
            switch (cardId)
            {
                case "BlueDragon": case "RedDragon": case "Nuclear": return CardRarity.Legendary;
                case "ManaPond": return CardRarity.Rare;
                case "ManaCannon": case "OrcBerserker": case "ElfLongbowScout": return CardRarity.Uncommon;
                case "Gwangma": return CardRarity.Unique;
                default: return CardRarity.Common;
            }
        }
    }
}
