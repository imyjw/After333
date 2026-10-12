# After333 카드 상세 명세

최초 작성 기준일: 2026-09-07 / 강화 규칙·공통 설명 갱신: 2026-09-12 / n연타 반격 규칙 갱신: 2026-09-13 / 종족 설정·뉴클리어·마법 증폭진 추가 갱신: 2026-09-21 / 뉴클리어 효과·강화 및 지진·메테오·오크 광전사·오크 족장·스켈레톤아처·엘프 장로 신규 카드 갱신: 2026-09-30 / 개방 방주·마나 대포·파이어스피어·플라즈마 쉴드·마나쉴드·트랩: 리턴·트랩-미러 필드·트랩 - 반응 장갑·트랩 - 아이언 돔·트랩 - 서먼 트랩 신규 카드 갱신: 2026-10-01

최신 대조 기준: 2026-10-03. 현재 카드 JSON 60종, 네이티브 카탈로그 53종, 강화 코드와 서버 보상 필터를 정적으로 비교했다. 아래 과거 실행 검증 기록은 그 날짜의 결과이며 이번 문서 대조가 Unity 실행·클라이언트 빌드·운영 서버 배포를 뜻하지 않는다.

반응 장갑 추가 실행 검증(2026-10-01): 분리 Unity 6000.3.12f1의 관련 17개 fixture에서 468/468 통과, 네이티브 등록 50→51 및 반복 등록·기존 자산 보존 확인. 복사 실제 서버 빌드 오류 0·기존 경고 95, `BattleSession` 12그룹 통과. 증거는 `TempBuild/ExplosiveReactiveArmour_20261001/`에 있으며 사용자 앱 빌드·설치나 운영 배포는 수행하지 않았다.

최근 밸런스: 오크 전사·오크 광전사 기본 ATK 20, 마나 대포 기본 ATK 40/HP 10·Lv.13 44/19 (2026-10-01), 블루 드래곤 ATK/HP 40/50, 레드 드래곤 40/40, 생화학폭탄 기본 고정 피해 25. 두 드래곤의 Lv.13은 각각 44/59와 44/49이며, 생화학폭탄의 Lv.13 피해는 38이다.

이 문서는 카드 원본 데이터, 강화 코드, 개별 전투 효과 및 과거 대화 복원 기록을 대조한 현재 설정의 스냅샷이다. 개별 카드 추가와 밸런스 변경은 아래 날짜별 확정 스펙에 반영한다.

## 1. 범위와 읽는 방법

- JSON/domain 카드 정의 60종: 판타지 29, 무림 11, 과학 문명 15, 중립 5. Unity 네이티브 `StarterTenCardCatalog` 자산은 트랩 5종을 포함해 53종으로 구분한다.
- 종류: 유닛 27, 건물 7, 마법 26. 마스터는 덱 카드가 아니므로 60종에서 제외한다.
- 덱 빌딩 On 53종 / Off 7종. 강화 가능 43종 / 불가 17종. `includeInRewards` 적용값 On 42종 / Off 18종과 실제 랜덤 보상 대상 39종을 구분한다(강화 불가 트랩: 리턴의 사용자 지정 예외 포함).
- 미등록 기획 1종: Card Grader. 확인되지 않은 항목은 미확정으로 남긴다.
- 기본 능력치는 Lv.0이다. 현재 HP, 버프·디버프, 부활 누적, 웨어울프 효과, 개방 방주 골드 보너스, 주문력 등 전투 중 값은 별도로 계산된다.
- On/Off는 등장 설정이지 이미지 완성도나 실기기 최종 검증 여부가 아니다.
- 일반 공격 속성과 카드 효과의 피해 속성은 별도로 설정된다. 현재 레드 드래곤은 일반 공격과 턴 종료 효과가 모두 마법 피해다.
- 아래 원문 JSON은 모든 명시된 카드 필드를 보존한다. JSON 미지정 값과 해당 카드 종류에 적용되지 않는 필드는 구분한다.

## 2. 공통 기본값과 적용 규칙

| 항목 | 현재 기준 |
|---|---|
| 자원 | 마나·기·전력·골드. 네 항목 중 생략된 비용은 0 |
| 골드 대체 | 카드 사용 시 부족한 마나·기·전력을 골드로 1:1 대체. 명시된 골드 비용은 별도로 지불 |
| 전력 소모 | 소유자 턴 시작 시 지불하며 골드 대체 가능. 전액 지불 불가 시 방전. 망각·봉인 등 세부 예외는 전투 규칙 참조 |
| 일반 공격 | 유닛은 기본 턴당 1회, 공격당 1타격. ATK 0이면 공격·반격 불가. 속공이 없으면 소환한 턴 공격 불가 |
| n연타 | 카드 이름과 관계없이 일반 공격과 적법한 근접 반격 각각 자신의 유효 n만큼 타격. 각 타격에 방어력을 적용하며 공격자·반격자의 연타 수를 서로 곱하지 않음. 망각 중 1타격, 방전·봉인·스턴·빙결은 반격 불가 |
| 건물 | 현재 등록 건물은 모두 이동·공격 불가. JSON 로더의 기본 Melee/CanMove 값이 건물의 이동·공격을 허용하는 것은 아님 |
| 생략 플래그 | hasRush/hasReplicate/hasBerserker/hasEndure/hasShielder/hasLifeSteal/hasHiding/hasFlying/hasPiercing는 false |
| 생략 상태 수치 | 방어력·유지비·주문력·초기 봉인 횟수는 0, 무적은 None |
| 등장 기본값 | includeInDraft / includeInRewards 생략 시 true |
| 실제 보상 풀 | 유효한 카드 정의 + includeInRewards=true + (강화 가능 또는 사용자 지정 TrapReturn 예외) + Master 제외 |
| 강화 비용 | 목표 Lv.1/2/3/4/5는 동일 카드 3/6/10/20/30장과 같은 수치의 계정 골드, Lv.6~13은 각 33장·33골드. 전투 골드와 계정 골드는 별개. [전체 비용표](meta_rules.md#card-upgrade-costs) |
| 일반 강화 | Lv.3/6/9/13 ATK +1, 나머지 HP +1. Lv.13 총 ATK +4 / HP +9 |
| Lv.13만 공격력 강화 | A-212·케르베로스는 Lv.1~12 HP +1, Lv.13 ATK +1. 총 ATK +1 / HP +12 |
| HP 전용 강화 | 마나의 샘·로봇 공장·개방 분타·객잔·상단·발전소·원자력 발전소는 레벨마다 HP +1 |
| 마왕·용사 강화 | Lv.3/6/9 ATK +1, Lv.13 ATK +3, 나머지 HP +1. Lv.13 총 ATK +6 / HP +9 |
| 피해 마법 강화 | 파이어볼·파이어월·시한폭탄·생화학폭탄·뉴클리어·지진·메테오·파이어스피어는 레벨마다 피해 +1(뉴클리어는 즉시 피해만 증가, 지진 건물 피해는 증가된 기본 피해의 3배) |
| 방어 버프 마법 | 플라즈마 쉴드·마나쉴드는 강화 불가. 물리·마법 방어력 부여량은 각각 +10 고정이며 랜덤 카드 보상·카드팩에서 제외 |
| 레벨 범위 예외 | 유닛/건물 능력치 계산은 13 초과를 13으로 제한. 마법 피해 계산은 0 미만 또는 13 초과 레벨의 보너스를 0으로 처리 |
| 트랩 | TrapSpell의 설치 비용 0과 별도 trapActivationCost를 구분. 조건·비용 충족 때 1회 소모하며 동일 소유자/CardId 중복 설치 불가 |
| 효과 문구와 실제 기능 | effectText/specialEffectText는 설명 데이터. 하드코딩된 효과 수치·처리 순서는 전투 코드와 함께 수정해야 함 |
| 종족 | 유닛 전용 복수 목록, 없음 허용. 망각에도 유지. [전체 지정값](unit_races.md) |
| 유효 상태 | 방전·망각·봉인·스턴·빙결 및 효과별 상호작용은 battle_rules.md와 개별 효과 코드를 따름. 스턴·빙결 체계가 있어도 기존 카드에 자동으로 부여되지는 않음 |

## 3. 카드 찾아보기

| 문명 | 카드 | 종류 | 희귀도 | 덱 빌딩 | 강화 |
|---|---|---|---|---|---|
| 판타지 | [블루 드래곤 (BlueDragon)](#card-bluedragon) | 유닛 | 레전더리 | On | 가능 |
| 판타지 | [케르베로스 (Cerberus)](#card-cerberus) | 유닛 | 레어 | On | 가능 |
| 판타지 | [엘프 장궁수 (ElfLongbowScout)](#card-elflongbowscout) | 유닛 | 언커먼 | On | 가능 |
| 판타지 | [엘프 장로 (ElfElder)](#card-elf-elder) | 유닛 | 레어 | On | 가능 |
| 판타지 | [마법 증폭진 (MagicAmplificationCircle)](#card-magicamplificationcircle) | 마법 / 개별 효과 | 커먼 | Off | 불가 |
| 판타지 | [마나쉴드 (ManaShield)](#card-manashield) | 마법 / 방어 버프 | 커먼 | Off | 불가 |
| 판타지 | [메테오 (Meteor)](#card-meteor) | 마법 / 개별 효과 | 유니크 | On | 가능 |
| 판타지 | [파이어스피어 (FireSpear)](#card-firespear) | 마법 / 개별 효과 | 커먼 | On | 가능 |
| 판타지 | [파이어볼 (firebolt)](#card-firebolt) | 마법 / 직접 피해 | 커먼 | On | 가능 |
| 판타지 | [파이어월 (Firewall)](#card-firewall) | 마법 / 개별 효과 | 언커먼 | Off | 가능 |
| 판타지 | [고블린 (Goblin)](#card-goblin) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [스켈레톤 (Skeleton)](#card-skeleton) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [스켈레톤아처 (SkeletonArcher)](#card-skeletonarcher) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [좀비 (Zombie)](#card-zombie) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [오크 전사 (OrcWarrior)](#card-orcwarrior) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [오크 광전사 (OrcBerserker)](#card-orcberserker) | 유닛 | 언커먼 | On | 가능 |
| 판타지 | [오크 족장 (OrcChieftain)](#card-orcchieftain) | 유닛 | 레어 | On | 가능 |
| 판타지 | [웨어울프 (Werewolf)](#card-werewolf) | 유닛 | 레어 | On | 가능 |
| 판타지 | [마왕 (DemonKing)](#card-demonking) | 유닛 | 레전더리 | Off | 가능 |
| 판타지 | [용사 (Hero)](#card-hero) | 유닛 | 유니크 | Off | 가능 |
| 판타지 | [골렘 (Golem)](#card-golem) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [마나 대포 (ManaCannon)](#card-manacannon) | 유닛 | 언커먼 | On | 가능 |
| 판타지 | [마나의 샘 (ManaPond)](#card-manapond) | 건물 | 레어 | On | 가능 |
| 판타지 | [마나 위버 (ManaWeaver)](#card-manaweaver) | 유닛 | 언커먼 | On | 가능 |
| 판타지 | [마정석 (ManaStone)](#card-manastone) | 마법 / 개별 효과 | 커먼 | On | 불가 |
| 판타지 | [마정석 꾸러미 (ManaStoneBundle)](#card-manastonebundle) | 마법 / 개별 효과 | 언커먼 | On | 불가 |
| 판타지 | [레드 드래곤 (RedDragon)](#card-reddragon) | 유닛 | 레전더리 | On | 가능 |
| 판타지 | [뱀파이어 (Vampire)](#card-vampire) | 유닛 | 언커먼 | On | 가능 |
| 판타지 | [트랩-미러 필드 (TrapMirrorField)](#card-trapmirrorfield) | 마법 / 트랩 | 레어 | On | 불가 |
| 무림 | [천라지망 (CheonraJimang)](#card-cheonrajimang) | 마법 / 개별 효과 | 레어 | On | 불가 |
| 무림 | [대환단 (Daehwandan)](#card-daehwandan) | 마법 / 개별 효과 | 레전더리 | On | 가능 |
| 무림 | [영약: 만년설삼 (TenThousandYearSnowGinseng)](#card-tenthousandyearsnowginseng) | 마법 / 지속 자원 | 유니크 | On | 불가 |
| 무림 | [고독 (Gu)](#card-gu) | 마법 / 개별 효과 | 유니크 | On | 불가 |
| 무림 | [환술 (HuanShu)](#card-huanshu) | 마법 / 개별 효과 | 언커먼 | On | 불가 |
| 무림 | [개방 분타 (GaebangBranch)](#card-gaebangbranch) | 건물 | 커먼 | On | 가능 |
| 무림 | [상단 (MerchantCaravan)](#card-merchantcaravan) | 건물 | 언커먼 | On | 가능 |
| 무림 | [객잔 (Inn)](#card-inn) | 건물 | 커먼 | Off | 가능 |
| 무림 | [개방 방주 (GaebangChief)](#card-gaebangchief) | 유닛 | 유니크 | On | 가능 |
| 무림 | [광마 (Gwangma)](#card-gwangma) | 유닛 | 유니크 | On | 가능 |
| 무림 | [소림 1대 제자 (Shaolin_1st_Disciple)](#card-shaolin_1st_disciple) | 유닛 | 커먼 | On | 가능 |
| 과학 문명 | [트랩 - 반응 장갑 (ExplosiveReactiveArmour)](#card-explosivereactivearmour) | 마법 / 트랩 | 레어 | On | 불가 |
| 과학 문명 | [트랩 - 아이언 돔 (IronDome)](#card-irondome) | 마법 / 트랩 | 레어 | On | 불가 |
| 과학 문명 | [플라즈마 쉴드 (PlasmaShield)](#card-plasmashield) | 마법 / 방어 버프 | 커먼 | Off | 불가 |
| 과학 문명 | [뉴클리어 (Nuclear)](#card-nuclear) | 마법 / 개별 효과 | 레전더리 | On | 가능 |
| 과학 문명 | [A-111 (A-111)](#card-a-111) | 유닛 | 커먼 | On | 가능 |
| 과학 문명 | [A-212 (A-212)](#card-a-212) | 유닛 | 레어 | On | 가능 |
| 과학 문명 | [A-301 (A-301)](#card-a-301) | 유닛 | 커먼 | On | 가능 |
| 과학 문명 | [로봇 합체 (RobotFusion)](#card-robotfusion) | 마법 / 개별 효과 | 커먼 | Off | 불가 |
| 과학 문명 | [로봇 공장 (RobotFactory)](#card-robotfactory) | 건물 | 레어 | Off | 가능 |
| 과학 문명 | [발전소 (PowerPlant)](#card-powerplant) | 건물 | 커먼 | On | 가능 |
| 과학 문명 | [원자력 발전소 (NuclearPowerPlant)](#card-nuclearpowerplant) | 건물 | 레어 | On | 가능 |
| 과학 문명 | [시한폭탄 (TimedBomb)](#card-timedbomb) | 마법 / 개별 효과 | 커먼 | On | 가능 |
| 과학 문명 | [생화학폭탄 (BiochemicalBomb)](#card-biochemicalbomb) | 마법 / 개별 효과 | 언커먼 | On | 가능 |
| 과학 문명 | [초소형 발전기 (Microreactor)](#card-microreactor) | 마법 / 지속 자원 | 언커먼 | On | 불가 |
| 과학 문명 | [보조배터리 (PowerBank)](#card-powerbank) | 마법 / 개별 효과 | 커먼 | On | 불가 |
| 중립 | [트랩 - 서먼 트랩 (SummonTrap)](#card-summontrap) | 마법 / 트랩 | 커먼 | On | 가능 |
| 중립 | [트랩: 리턴 (TrapReturn)](#card-trapreturn) | 마법 / 트랩 | 레어 | On | 불가 (보상 예외) |
| 중립 | [지진 (Earthquake)](#card-earthquake) | 마법 / 개별 효과 | 언커먼 | On | 가능 |
| 중립 | [금광 채굴꾼 (GoldMiner)](#card-goldminer) | 유닛 | 커먼 | On | 가능 |
| 중립 | [방패병 (Shieldbearer)](#card-shieldbearer) | 유닛 | 커먼 | On | 가능 |

## 4. 등록 카드 상세

<a id="card-a-212"></a>
### A-212 (신규)

| 항목 | 값 |
|---|---|
| 이름 / CardId | A-212 / A-212 |
| 종족 | 로봇 |
| 종류 / 문명 / 희귀도 | 유닛 / 과학 문명 / 레어 |
| 비용 | 전력 4 (마나 0, 기 0, 골드 0); 부족한 전력은 골드로 대체 |
| 차지 타일 / 이동 | 1×1 / 가능 |
| 공격 유형 / 데미지 속성 | 근거리 / 물리 |
| 기본 ATK / HP | 6 / 22 |
| 물리 / 마법 방어력 | 0 / 0 |
| 특수효과 | 4연타, 전력 소모:2 |
| 일반 효과 | 없음 |
| 덱 빌딩 / 랜덤 보상 | On / On |
| 강화 | 최대 Lv.13. Lv.1~12마다 HP +1, Lv.13에는 ATK +1 |
| Lv.12 / Lv.13 ATK·HP | 6/34 / 7/34 |
| 소환 턴 공격 | 불가 (속공 없음) |
| 카드 이미지 | Assets/Project333/Resources/Project333/CardArtwork/A-212.png |
| 애니메이션 | Assets/Unit/A-212: Idle, Run, Attack, BeAttacked, Death. 방전은 Idle 대체 |

4연타는 일반 공격 또는 적법한 근접 반격 한 번에 4번 타격하는 효과이며, 매 타격마다 방어력을 적용한다.
로봇 공장의 생성 후보와 로봇 합체 대상으로 포함된다. 전력 소모:2는 기존 유지비 순서와 골드 대체 규칙을 따른다.
유지비를 낼 수 없으면 방전된다. 기존 봉인·망각·방전의 효과 억제 규칙을 그대로 따른다.
이번 추가로 기존 AI 고정 덱 10개를 자동 수정하지는 않는다.

**원본 카드 데이터**

```json
{
  "id": "A-212",
  "displayName": "A-212",
  "definitionType": "Unit",
  "races": ["Robot"],
  "rarity": "Rare",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 4
  },
  "specialEffectText": "4연타\n전력 소모:2",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 6,
  "health": 22,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "isScience": true,
  "sciencePowerUpkeep": 2,

  "hitsPerAttack": 4
}
```

<a id="card-bluedragon"></a>

### 01. 블루 드래곤 (BlueDragon)

| 항목 | 내용 |
|---|---|
| 이름 | 블루 드래곤 |
| 종족 | 드래곤 |
| cardId | `BlueDragon` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레전더리 (Legendary) |
| 사용 비용 | 마나 9 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 40 / 50 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 마법 (Magic) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 있음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 44 / HP 59 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 종료 시 내 타일들 위의 유닛들의 체력 +33 회복 |
| 특수효과 문구 원문 | 비행 |
| 효과 요약 | 비행. 내 턴 종료 시 내 전장의 건물을 제외한 생존 아군 유닛과 마스터의 체력을 33 회복한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/BlueDragon.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

회복량 33은 EndTurnService의 상수다. 대상은 건물을 제외한 생존 아군 유닛과 마스터이며 봉인 대상은 제외한다. 카드 강화로 회복량이 증가하지 않는다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "BlueDragon",
  "displayName": "블루 드래곤",
  "definitionType": "Unit",
  "races": ["Dragon"],
  "rarity": "Legendary",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 9
  },
  "effectText": "내 턴 종료 시 내 타일들 위의 유닛들의 체력 +33 회복",
  "specialEffectText": "비행",
  "attackType": "Ranged",
  "damageType": "Magic",
  "attack": 40,
  "health": 50,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasFlying": true
}
```

<a id="card-cerberus"></a>

### 02. 케르베로스 (Cerberus)

| 항목 | 내용 |
|---|---|
| 이름 | 케르베로스 |
| 종족 | 야수 |
| cardId | `Cerberus` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 3 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 9 / 66 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 3회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.1~12 HP +1, Lv.13 ATK +1 |
| Lv.13 결과 | ATK 10 / HP 78 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | Triple Attack |
| 효과 요약 | 일반 공격과 적법한 근접 반격 한 번에 각각 3회 타격한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Cerberus.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Cerberus",
  "displayName": "케르베로스",
  "definitionType": "Unit",
  "races": ["Beast"],
  "rarity": "Rare",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 3,
    "gold": 3
  },
  "specialEffectText": "Triple Attack",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 9,
  "health": 66,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hitsPerAttack": 3
}
```

<a id="card-elflongbowscout"></a>

### 03. 엘프 장궁수 (ElfLongbowScout)

| 항목 | 내용 |
|---|---|
| 이름 | 엘프 장궁수 |
| 종족 | 엘프 |
| cardId | `ElfLongbowScout` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 2 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 10 / 20 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 있음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 14 / HP 29 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 관통 |
| 효과 요약 | 관통 |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/ElfLongbowScout.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

전열 공격 시 같은 열 후열, 후열 공격 시 같은 열 전열에 동일 타격 ATK·물리 속성으로 관통 피해를 준다. 관통 추가 피해로 별도 반격은 발생하지 않고 쉴더 피해 전환은 적용한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "ElfLongbowScout",
  "displayName": "엘프 장궁수",
  "definitionType": "Unit",
  "races": ["Elf"],
  "rarity": "Uncommon",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 2
  },
  "specialEffectText": "관통",
  "attackType": "Ranged",
  "damageType": "Physical",
  "attack": 10,
  "health": 20,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasPiercing": true
}
```

<a id="card-firebolt"></a>

### 04. 파이어볼 (firebolt)

| 항목 | 내용 |
|---|---|
| 이름 | 파이어볼 |
| cardId | `firebolt` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 마법 / 직접 피해 / `DamageSpell` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 1 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 효과 피해 +1 |
| Lv.13 결과 | 효과 피해 23 (매 발동/대상 기준, 주문력·방어력 등 미반영) |
| 효과 문구 원문 | 대상 하나에게 마법 피해 10 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 선택한 합법적 대상 하나에게 마법 피해 10. 자기편과 상대편 대상 지정이 가능하다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 10 / 마법 (Magic) |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Firebolt.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

자기편과 상대편의 합법적인 1타일 대상을 지정할 수 있다. 피해는 기본 10 + 카드 강화 레벨이며, 전투 중 주문력이 있으면 마법 피해에 추가된다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "firebolt",
  "displayName": "파이어볼",
  "definitionType": "DamageSpell",
  "rarity": "Common",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "None",
  "cost": {
    "mana": 1
  },
  "effectText": "대상 하나에게 마법 피해 10",
  "damageType": "Magic",
  "damage": 10
}
```

<a id="card-firewall"></a>

### 05. 파이어월 (Firewall)

| 항목 | 내용 |
|---|---|
| 이름 | 파이어월 |
| cardId | `Firewall` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | Off |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (includeInRewards=false) |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 효과 피해 +1 |
| Lv.13 결과 | 효과 피해 46 (매 발동/대상 기준, 주문력·방어력 등 미반영) |
| 효과 문구 원문 | 매 턴 시작 시, 선택한 5X1 타일에 33 마법 데미지를 입힌다. |
| 특수효과 문구 원문 | 2턴 지속 |
| 효과 요약 | 자기편 또는 상대편의 선택한 5x1 한 줄에 매 턴 시작마다 마법 피해 33. 총 2회 발동한다. 피해 발동 시점의 현재 주문력을 적용한다. |
| 개별 효과 ID | `firewall` |
| 피해 마법의 기본 피해 / 속성 | 33 / 마법 (Magic) |
| triggerCount 필드 | 2 (의미는 아래 효과 설명 참조) |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

한쪽 보드의 전열 또는 후열 5×1을 선택한다. 사용 후 다음 전역 턴 시작부터 총 2회 발동한다. 사용 시점의 강화 피해는 저장하고 주문력은 매 피해 발동 시 현재 값으로 계산한다. 여러 장은 독립적으로 중첩한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Firewall",
  "displayName": "파이어월",
  "definitionType": "ScriptedSpell",
  "rarity": "Uncommon",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "None",
  "cost": {
    "mana": 3
  },
  "effectText": "매 턴 시작 시, 선택한 5X1 타일에 33 마법 데미지를 입힌다.",
  "specialEffectText": "2턴 지속",
  "damageType": "Magic",
  "damage": 33,
  "effectId": "firewall",
  "triggerCount": 2
}
```

<a id="card-goblin"></a>

### 06. 고블린 (Goblin)

| 항목 | 내용 |
|---|---|
| 이름 | 고블린 |
| 종족 | 고블린 |
| cardId | `Goblin` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 1 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 10 / 10 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 14 / HP 19 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 별도 효과 없음. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Goblin.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Goblin",
  "displayName": "고블린",
  "definitionType": "Unit",
  "races": ["Goblin"],
  "rarity": "Common",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 1
  },
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 10,
  "health": 10,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true
}
```

<a id="card-skeleton"></a>

### 07. 스켈레톤 (Skeleton)

| 항목 | 내용 |
|---|---|
| 이름 | 스켈레톤 |
| 종족 | 언데드 |
| cardId | `Skeleton` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 1 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 10 / 10 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 14 / HP 19 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 별도 효과 없음. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Skeleton.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Skeleton",
  "displayName": "스켈레톤",
  "definitionType": "Unit",
  "races": ["Undead"],
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 1
  },
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 10,
  "health": 10,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true
}
```

<a id="card-zombie"></a>

### 08. 좀비 (Zombie)

| 항목 | 내용 |
|---|---|
| 이름 | 좀비 |
| 종족 | 언데드 |
| cardId | `Zombie` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 1 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 15 / 1 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 19 / HP 10 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 별도 효과 없음. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Zombie.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Zombie",
  "displayName": "좀비",
  "definitionType": "Unit",
  "races": ["Undead"],
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 1
  },
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 15,
  "health": 1,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true
}
```

<a id="card-orcwarrior"></a>

### 09. 오크 전사 (OrcWarrior)

| 항목 | 내용 |
|---|---|
| 이름 | 오크 전사 |
| 종족 | 오크 |
| cardId | `OrcWarrior` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 20 / 40 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 24 / HP 49 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 별도 효과 없음. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/OrcWarrior.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "OrcWarrior",
  "displayName": "오크 전사",
  "definitionType": "Unit",
  "races": ["Orc"],
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 3
  },
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 20,
  "health": 40,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true
}
```

<a id="card-werewolf"></a>

### 10. 웨어울프 (Werewolf)

| 항목 | 내용 |
|---|---|
| 이름 | 웨어울프 |
| 종족 | 야수 |
| cardId | `Werewolf` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 10 / 30 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 있음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 14 / HP 39 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 필드의 다른 살아 있는 웨어울프 하나당 ATK +10 |
| 특수효과 문구 원문 | 복제 |
| 효과 요약 | 복제. 살아 있고 효과가 활성화된 동안 같은 필드의 다른 살아 있는 아군 웨어울프마다 ATK +10. 셀 때 방전·망각은 포함하고 봉인은 제외한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Werewolf.png` |

**판정 및 구분할 사항**

효과를 받는 자신이 방전·망각·봉인이면 효과가 억제된다. 다른 웨어울프를 셀 때는 살아 있는 아군만 세며 봉인은 제외하고 방전·망각은 포함한다. 카드 강화와 전투 중 효과에 따른 추가 ATK는 구분한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Werewolf",
  "displayName": "웨어울프",
  "definitionType": "Unit",
  "races": ["Beast"],
  "rarity": "Rare",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 3
  },
  "effectText": "내 필드의 다른 살아 있는 웨어울프 하나당 ATK +10",
  "specialEffectText": "복제",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 10,
  "health": 30,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasReplicate": true
}
```

<a id="card-demonking"></a>

### 11. 마왕 (DemonKing)

| 항목 | 내용 |
|---|---|
| 이름 | 마왕 |
| 종족 | 없음 |
| cardId | `DemonKing` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레전더리 (Legendary) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 3 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 33 / 33 |
| 기본 물리 / 마법 방어력 | 3 / 3 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 마법 (Magic) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | Off |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (includeInRewards=false) |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9 ATK +1, Lv.13 ATK +3, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 39 / HP 42 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 사망 시 그 타일에서 봉인된다. 사망 당시 턴 플레이어의 다음 3번째 턴 시작에 ATK/HP +33을 얻고 완전 회복하여 부활한다. 이 효과는 반복해서 발동한다. |
| 특수효과 문구 원문 | 마왕의 귀환 |
| 효과 요약 | 마왕의 귀환. 사망 시 그 타일에서 봉인되고, 사망 당시 턴 플레이어의 다음 세 번째 턴 시작에 누적 ATK/HP +33을 얻어 완전 회복 부활한다. 방전·망각 중 사망하면 영구 제거된다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

봉인 카운트는 마왕 소유자가 아니라 사망 당시 턴 플레이어의 이후 턴 시작을 센다. 예: 상대 턴에 죽으면 이후 상대 턴 시작 3회를 기다린다. 부활 시 누적 ATK/최대 HP +33과 완전 회복을 적용한다. 부활한 턴에는 일반적인 소환 후 공격 제한이 다시 적용된다. 초기 sealboundOwnerTurnStarts=0은 사망 후 특수 봉인과 별개다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "DemonKing",
  "displayName": "마왕",
  "definitionType": "Unit",
  "races": [],
  "rarity": "Legendary",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 3,
    "gold": 3
  },
  "effectText": "사망 시 그 타일에서 봉인된다. 사망 당시 턴 플레이어의 다음 3번째 턴 시작에 ATK/HP +33을 얻고 완전 회복하여 부활한다. 이 효과는 반복해서 발동한다.",
  "specialEffectText": "마왕의 귀환",
  "attackType": "Melee",
  "damageType": "Magic",
  "attack": 33,
  "health": 33,
  "physicalDefense": 3,
  "magicDefense": 3,
  "canMove": true
}
```

<a id="card-hero"></a>

### 12. 용사 (Hero)

| 항목 | 내용 |
|---|---|
| 이름 | 용사 |
| 종족 | 인간 |
| cardId | `Hero` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 유니크 (Unique) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 33 / 33 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 고정 (Fixed) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 가능: 속공 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 있음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | 없음 |
| 덱 빌딩 등장 | Off |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (includeInRewards=false) |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9 ATK +1, Lv.13 ATK +3, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 39 / HP 42 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 상대방 소환물을 공격해 파괴할 때마다 공격력 또는 체력+33 |
| 특수효과 문구 원문 | 속공 |
| 효과 요약 | 일반공격으로 상대 소환물을 파괴할 때마다 ATK 또는 최대/현재 HP +33. 각각 50% 확률. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

속공으로 소환한 턴부터 공격한다. 내 일반공격으로 상대 유닛·건물을 파괴한 경우만 횟수마다 50% 확률로 ATK 또는 최대/현재 HP +33을 얻는다. 반격·마스터·아군·파괴 효과의 연쇄 사망은 제외하고, 동시 반격으로 사망한 용사는 부활하지 않는다. 기존 무적과 턴 종료 성장은 없다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Hero",
  "displayName": "용사",
  "definitionType": "Unit",
  "races": [
    "Human"
  ],
  "rarity": "Unique",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 3
  },
  "effectText": "상대방 소환물을 공격해 파괴할 때마다 공격력 또는 체력+33",
  "specialEffectText": "속공",
  "hasRush": true,
  "attackType": "Melee",
  "damageType": "Fixed",
  "attack": 33,
  "health": 33,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true
}
```

<a id="card-golem"></a>

### 13. 골렘 (Golem)

| 항목 | 내용 |
|---|---|
| 이름 | 골렘 |
| 종족 | 없음 |
| cardId | `Golem` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 2 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 20 / 30 |
| 기본 물리 / 마법 방어력 | 3 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 24 / HP 39 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 별도 효과 없음. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Golem.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Golem",
  "displayName": "골렘",
  "definitionType": "Unit",
  "races": [],
  "rarity": "Common",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 2
  },
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 20,
  "health": 30,
  "physicalDefense": 3,
  "magicDefense": 0,
  "canMove": true
}
```

<a id="card-manapond"></a>

### 14. 마나의 샘 (ManaPond)

| 항목 | 내용 |
|---|---|
| 이름 | 마나의 샘 |
| cardId | `ManaPond` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 건물 / `Building` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 6 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 20 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 (canAttack=false) |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 3 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 20 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.1~13 모두 HP +1 |
| Lv.13 결과 | ATK 0 / HP 33 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 턴 시작 시 마나 +3 |
| 특수효과 문구 원문 | 주문력 +20 |
| 효과 요약 | 소유자 턴 시작에 마나 +3. 활성 상태에서 아군 주문력 +20을 제공한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/ManaPond.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

2026-09-12 사용자 확정: 모든 강화 레벨에서 HP만 +1이며 공격력은 증가하지 않는다. 공격 불가 설정과 마나 생산량은 유지한다.

2026-09-13 사용자 확정: 비용을 마나 5로 올리고 주문력 +20을 추가했다. 주문력은 소환 직후부터 제공하며, 피해 발생 시점의 전체 아군 주문력에 합산한다. 기존 HP 전용 강화 규칙은 유지하고 주문력은 모든 레벨에서 20이다.

2026-10-05 사용자 변경: 소환 비용은 마나 6이다. 마나 생산 +3, 주문력 +20과 HP 전용 강화 규칙은 유지한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "ManaPond",
  "displayName": "마나의 샘",
  "definitionType": "Building",
  "rarity": "Rare",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 6
  },
  "effectText": "턴 시작 시 마나 +3",
  "specialEffectText": "주문력 +20",
  "spellPower": 20,
  "damageType": "None",
  "attack": 0,
  "health": 20,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canAttack": false,
  "turnStartResourceGain": {
    "mana": 3
  }
}
```

<a id="card-manaweaver"></a>

### 15. 마나 위버 (ManaWeaver)

| 항목 | 내용 |
|---|---|
| 이름 | 마나 위버 |
| 종족 | 인간 |
| cardId | `ManaWeaver` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 2 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 1 / 10 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 마법 (Magic) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 1 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 5 / HP 19 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 턴 시작 시 마나 +1 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 소유자 턴 시작에 마나 +1. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/ManaWeaver.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "ManaWeaver",
  "displayName": "마나 위버",
  "definitionType": "Unit",
  "races": ["Human"],
  "rarity": "Uncommon",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 2
  },
  "effectText": "턴 시작 시 마나 +1",
  "attackType": "Ranged",
  "damageType": "Magic",
  "attack": 1,
  "health": 10,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "turnStartResourceGain": {
    "mana": 1
  }
}
```

<a id="card-manastone"></a>

### 16. 마정석 (ManaStone)

| 항목 | 내용 |
|---|---|
| 이름 | 마정석 |
| cardId | `ManaStone` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 2 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 사용 시 마나 +3 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 사용 즉시 마나 +3. |
| 개별 효과 ID | `mana_stone` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/ManaStone.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "ManaStone",
  "displayName": "마정석",
  "definitionType": "ScriptedSpell",
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": false,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "None",
  "cost": {
    "gold": 2
  },
  "effectText": "사용 시 마나 +3",
  "damageType": "None",
  "damage": 0,
  "effectId": "mana_stone"
}
```

<a id="card-manastonebundle"></a>

### 17. 마정석 꾸러미 (ManaStoneBundle)

| 항목 | 내용 |
|---|---|
| 이름 | 마정석 꾸러미 |
| cardId | `ManaStoneBundle` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 5 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 사용 시 마나 +9 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 사용 즉시 마나 +9. |
| 개별 효과 ID | `mana_stone_bundle` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/ManaStoneBundle.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "ManaStoneBundle",
  "displayName": "마정석 꾸러미",
  "definitionType": "ScriptedSpell",
  "rarity": "Uncommon",
  "includeInDraft": true,
  "includeInRewards": false,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "None",
  "cost": {
    "gold": 5
  },
  "effectText": "사용 시 마나 +9",
  "damageType": "None",
  "damage": 0,
  "effectId": "mana_stone_bundle"
}
```

<a id="card-reddragon"></a>

### 18. 레드 드래곤 (RedDragon)

| 항목 | 내용 |
|---|---|
| 이름 | 레드 드래곤 |
| 종족 | 드래곤 |
| cardId | `RedDragon` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레전더리 (Legendary) |
| 사용 비용 | 마나 10 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 40 / 40 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 마법 (Magic) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 있음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 44 / HP 49 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 종료 시 상대방 타일 전체에 20 마법 피해 |
| 특수효과 문구 원문 | 비행 |
| 효과 요약 | 비행. 내 턴 종료 시 상대 전장의 봉인되지 않은 모든 소환물과 마스터에게 마법 피해 33. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/RedDragon.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

일반 공격과 턴 종료 효과는 모두 마법 피해다. 기본 효과 피해 33은 EndTurnService의 상수로 처리하며 일반 공격과 턴 종료 효과 모두 현재 주문력만큼 피해가 증가한다. 카드 강화로 이 효과 피해가 증가하지 않는다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "RedDragon",
  "displayName": "레드 드래곤",
  "definitionType": "Unit",
  "races": ["Dragon"],
  "rarity": "Legendary",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 10
  },
  "effectText": "내 턴 종료 시 상대방 타일 전체에 20 마법 피해",
  "specialEffectText": "비행",
  "attackType": "Melee",
  "damageType": "Magic",
  "attack": 40,
  "health": 40,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasFlying": true
}
```

<a id="card-vampire"></a>

### 19. 뱀파이어 (Vampire)

| 항목 | 내용 |
|---|---|
| 이름 | 뱀파이어 |
| 종족 | 뱀파이어 |
| cardId | `Vampire` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 5 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 15 / 50 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 있음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 19 / HP 59 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 흡혈: 공격 시 입힌 피해만큼 체력 회복 |
| 효과 요약 | 흡혈. 공격 또는 반격 후 살아 있으면 방어 적용 뒤 적 HP를 실제로 감소시킨 만큼 회복한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Vampire.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

흡혈은 방어 적용 후 실제로 감소한 적 HP만큼만 회복하고, 공격 또는 반격 후 자신이 살아 있을 때 발동한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Vampire",
  "displayName": "뱀파이어",
  "definitionType": "Unit",
  "races": ["Vampire"],
  "rarity": "Uncommon",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 5
  },
  "effectText": "",
  "specialEffectText": "흡혈: 공격 시 입힌 피해만큼 체력 회복",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 15,
  "health": 50,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasLifeSteal": true
}
```

<a id="card-cheonrajimang"></a>

### 20. 천라지망 (CheonraJimang)

| 항목 | 내용 |
|---|---|
| 이름 | 천라지망 |
| cardId | `CheonraJimang` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 무림 (Murim) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 0 / 기 5 / 전력 0 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 유닛을 하나 선택하고, 내 다음 턴 시작 시 그 유닛 파괴 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 유닛 하나를 선택하고 내 다음 턴 시작에 파괴한다. |
| 개별 효과 ID | `cheonra_jimang` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/CheonraJimang.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "CheonraJimang",
  "displayName": "천라지망",
  "definitionType": "ScriptedSpell",
  "rarity": "Rare",
  "affiliation": "Murim",
  "chargeTileFootprint": "None",
  "cost": {
    "qi": 5
  },
  "effectText": "유닛을 하나 선택하고, 내 다음 턴 시작 시 그 유닛 파괴",
  "damageType": "None",
  "effectId": "cheonra_jimang"
}
```

<a id="card-daehwandan"></a>

### 21. 대환단 (Daehwandan)

| 항목 | 내용 |
|---|---|
| 이름 | 대환단 |
| cardId | `Daehwandan` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 무림 (Murim) |
| 희귀도 | 레전더리 (Legendary) |
| 사용 비용 | 마나 0 / 기 5 / 전력 0 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 (강화 가능·보상 플래그 On) |
| 강화 가능 여부 | 가능 |
| 레벨별 강화 규칙 | Lv.1–13 매 레벨 효과수치 +1. 공격력 증가량 = 30 + 레벨 |
| Lv.13 결과 | 효과수치 43, 내 마스터 공격력 +43 |
| 효과 문구 원문 | 사용 시 내 마스터의 공격력을 증가시킨다. |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 사용 즉시 내 마스터 공격력 +효과수치. Lv.0 30, Lv.13 43. 기 생산 없음. 중첩 가능. |
| 효과수치 (Lv.0) | 30 |
| 개별 효과 ID | `daehwandan` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Daehwandan.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Daehwandan",
  "displayName": "대환단",
  "definitionType": "ScriptedSpell",
  "rarity": "Legendary",
  "affiliation": "Murim",
  "chargeTileFootprint": "None",
  "cost": {
    "qi": 5
  },
  "effectText": "사용 시 내 마스터의 공격력을 증가시킨다.",
  "effectAmount": 30,
  "damage": 0,
  "damageType": "None",
  "effectId": "daehwandan"
}
```

**강화 레벨별 공격력 증가량**

| 레벨 | 효과수치 / 마스터 공격력 증가량 |
|---|---|
| 0 | +30 |
| 1 | +31 |
| 2 | +32 |
| 3 | +33 |
| 4 | +34 |
| 5 | +35 |
| 6 | +36 |
| 7 | +37 |
| 8 | +38 |
| 9 | +39 |
| 10 | +40 |
| 11 | +41 |
| 12 | +42 |
| 13 | +43 |

비용 기5는 그대로다. 주문력·마법 피해 증폭을 더하지 않고 사용자의 마스터에 전투 공격력 버프를 부여한다. 이후 턴 기 생산은 등록하지 않는다.

<a id="card-tenthousandyearsnowginseng"></a>

### 22. 영약: 만년설삼 (TenThousandYearSnowGinseng)

| 항목 | 내용 |
|---|---|
| 이름 | 영약: 만년설삼 |
| cardId | `TenThousandYearSnowGinseng` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 마법 / 지속 자원 / `PersistentResourceSpell` |
| 문명 | 무림 (Murim) |
| 희귀도 | 유니크 (Unique) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 4 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 3 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 사용 시 다음 3번의 내 턴 시작마다 기 +3 |
| 특수효과 문구 원문 | 지속 마법: 다음 3번의 내 턴 시작 후 종료됩니다. |
| 효과 요약 | 다음 3번의 내 턴 시작마다 기 +3. |
| 개별 효과 ID | `ten_thousand_year_snow_ginseng` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 3 |
| 지속 종료 문구 | 다음 3번의 내 턴 시작 후 종료됩니다. |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/TenThousandYearSnowGinseng.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "TenThousandYearSnowGinseng",
  "displayName": "영약: 만년설삼",
  "definitionType": "PersistentResourceSpell",
  "rarity": "Unique",
  "includeInDraft": true,
  "includeInRewards": false,
  "affiliation": "Murim",
  "chargeTileFootprint": "None",
  "cost": {
    "gold": 4
  },
  "effectText": "사용 시 다음 3번의 내 턴 시작마다 기 +3",
  "specialEffectText": "지속 마법: 다음 3번의 내 턴 시작 후 종료됩니다.",
  "damage": 0,
  "damageType": "None",
  "effectId": "ten_thousand_year_snow_ginseng",
  "turnStartResourceGain": {
    "qi": 3
  },
  "ownerTurnStartsRemaining": 3,
  "endConditionText": "다음 3번의 내 턴 시작 후 종료됩니다."
}
```

<a id="card-gu"></a>

### 23. 고독 (Gu)

| 항목 | 내용 |
|---|---|
| 이름 | 고독 |
| cardId | `Gu` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 무림 (Murim) |
| 희귀도 | 유니크 (Unique) |
| 사용 비용 | 마나 0 / 기 10 / 전력 0 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 상대방 유닛 하나를 복종시켜 현재 상태를 보존한 채 내 첫 번째 빈 타일로 가져온다. |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 상대 유닛 하나의 ATK, HP, 효과와 상태를 보존해 첫 번째 빈 아군 타일로 영구 이전한다. 이전 직후에는 일반적으로 공격할 수 없고 속공이 활성화되어 있으면 공격할 수 있다. |
| 개별 효과 ID | `gu` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Gu.png` |

**판정 및 구분할 사항**

마스터·건물이 아닌 적 유닛 하나를 대상으로 한다. 첫 배치 가능 아군 타일을 (0,0),(0,1),(1,0),(1,1)…(4,1) 순으로 찾는다. ATK/HP·효과·버프·디버프를 보존하며, 공격은 활성 속공이 없는 한 즉시 할 수 없다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Gu",
  "displayName": "고독",
  "definitionType": "ScriptedSpell",
  "rarity": "Unique",
  "includeInDraft": true,
  "includeInRewards": false,
  "affiliation": "Murim",
  "chargeTileFootprint": "None",
  "cost": {
    "qi": 10
  },
  "effectText": "상대방 유닛 하나를 복종시켜 현재 상태를 보존한 채 내 첫 번째 빈 타일로 가져온다.",
  "damageType": "None",
  "damage": 0,
  "effectId": "gu"
}
```

<a id="card-huanshu"></a>

### 24. 환술 (HuanShu)

| 항목 | 내용 |
|---|---|
| 이름 | 환술 |
| cardId | `HuanShu` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 무림 (Murim) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 0 / 기 3 / 전력 0 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 상대방 유닛 하나에게 환술을 영구적으로 부여합니다. 그 유닛이 일반 공격할 때마다 공격 대상이 무작위로 변경됩니다. |
| 특수효과 문구 원문 | 환술: 일반 공격 대상이 무작위로 변경됩니다. |
| 효과 요약 | 상대 유닛 하나에게 환술을 영구적으로 부여한다. 그 유닛이 일반 공격을 선언할 때마다 공격자 자신을 제외한 합법적 소환물·건물·마스터 중 서버가 무작위 대상을 정한다. |
| 개별 효과 ID | `huan_shu` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/HuanShu.png` |

**판정 및 구분할 사항**

기간 제한이 없는 영구 환술이다. 공격자 자신을 제외하고 양쪽 전장의 합법적인 유닛·건물·마스터 중 무작위 일반 공격 대상을 서버가 결정한다. 기존 3턴 기획은 현재 적용하지 않는다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "HuanShu",
  "displayName": "환술",
  "definitionType": "ScriptedSpell",
  "rarity": "Uncommon",
  "includeInDraft": true,
  "includeInRewards": false,
  "affiliation": "Murim",
  "chargeTileFootprint": "None",
  "cost": {
    "qi": 3
  },
  "effectText": "상대방 유닛 하나에게 환술을 영구적으로 부여합니다. 그 유닛이 일반 공격할 때마다 공격 대상이 무작위로 변경됩니다.",
  "specialEffectText": "환술: 일반 공격 대상이 무작위로 변경됩니다.",
  "damageType": "None",
  "damage": 0,
  "effectId": "huan_shu"
}
```

<a id="card-gaebangbranch"></a>

### 25. 개방 분타 (GaebangBranch)

| 항목 | 내용 |
|---|---|
| 이름 | 개방 분타 |
| cardId | `GaebangBranch` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 건물 / `Building` |
| 문명 | 무림 (Murim) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 2 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 20 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 (canAttack=false) |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 HP +1 |
| Lv.13 결과 | ATK 0 / HP 33 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 종료 시 내 골드가 0이라면 카드 2장 드로우 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 내 턴 종료 시 내 골드가 0이면 카드 2장을 뽑는다. 소환한 턴에도 발동하고 여러 장이면 타일 순서대로 각각 발동한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/GaebangBranch.png` |

**판정 및 구분할 사항**

소환한 턴 종료에도 발동한다. 여러 건물은 타일 순서대로 각각 2장을 드로우한다. 손패 초과 카드는 소멸하며 빈 덱의 드로우 실패는 매 시도마다 덱 고갈 고정 피해를 발생시킨다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "GaebangBranch",
  "displayName": "개방 분타",
  "definitionType": "Building",
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "Murim",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "gold": 2
  },
  "effectText": "내 턴 종료 시 내 골드가 0이라면 카드 2장 드로우",
  "damageType": "None",
  "attack": 0,
  "health": 20,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canAttack": false
}
```

<a id="card-merchantcaravan"></a>

### 26. 상단 (MerchantCaravan)

| 항목 | 내용 |
|---|---|
| 이름 | 상단 |
| cardId | `MerchantCaravan` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 건물 / `Building` |
| 문명 | 무림 (Murim) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 4 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 20 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 (canAttack=false) |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 3; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 HP +1 |
| Lv.13 결과 | ATK 0 / HP 33 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 시작 시 골드 +3 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 소유자 턴 시작에 골드 +3. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/MerchantCaravan.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "MerchantCaravan",
  "displayName": "상단",
  "definitionType": "Building",
  "rarity": "Uncommon",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "Murim",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "gold": 4
  },
  "effectText": "내 턴 시작 시 골드 +3",
  "damageType": "None",
  "attack": 0,
  "health": 20,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canAttack": false,
  "turnStartResourceGain": {
    "gold": 3
  }
}
```

<a id="card-inn"></a>

### 27. 객잔 (Inn)

| 항목 | 내용 |
|---|---|
| 이름 | 객잔 |
| cardId | `Inn` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 건물 / `Building` |
| 문명 | 무림 (Murim) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 2 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 20 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 (canAttack=false) |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 1; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | Off |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (includeInRewards=false) |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 HP +1 |
| Lv.13 결과 | ATK 0 / HP 33 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 시작 시 골드 +1 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 소유자 턴 시작에 골드 +1. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Inn",
  "displayName": "객잔",
  "definitionType": "Building",
  "rarity": "Common",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "Murim",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "qi": 2
  },
  "effectText": "내 턴 시작 시 골드 +1",
  "damageType": "None",
  "attack": 0,
  "health": 20,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canAttack": false,
  "turnStartResourceGain": {
    "gold": 1
  }
}
```

<a id="card-gwangma"></a>

### 28. 광마 (Gwangma)

| 항목 | 내용 |
|---|---|
| 이름 | 광마 |
| 종족 | 인간 |
| cardId | `Gwangma` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 무림 (Murim) |
| 희귀도 | 유니크 (Unique) |
| 사용 비용 | 마나 0 / 기 8 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 30 / 60 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 2회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 있음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 34 / HP 69 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | Berserker, Double Attack |
| 효과 요약 | 버서커: 잃은 HP 1당 ATK +1. 일반 공격과 적법한 근접 반격 한 번에 각각 2회 타격한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Gwangma.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Gwangma",
  "displayName": "광마",
  "definitionType": "Unit",
  "races": ["Human"],
  "rarity": "Unique",
  "affiliation": "Murim",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "qi": 8
  },
  "specialEffectText": "Berserker, Double Attack",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 30,
  "health": 60,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hitsPerAttack": 2,
  "hasBerserker": true
}
```

<a id="card-shaolin_1st_disciple"></a>

### 29. 소림 1대 제자 (Shaolin_1st_Disciple)

| 항목 | 내용 |
|---|---|
| 이름 | 소림 1대 제자 |
| 종족 | 인간 |
| cardId | `Shaolin_1st_Disciple` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 무림 (Murim) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 2 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 13 / 33 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 있음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 17 / HP 42 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | Endure |
| 효과 요약 | 불굴: 처음 받는 치명적인 피해를 버티고 HP 1로 생존한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Shaolin_1st_Disciple.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

불굴은 일반 공격·반격만이 아니라 치명적인 피해에 적용하며 최초 1회 HP 1로 생존한다. 피해가 아닌 직접 파괴와 구분한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Shaolin_1st_Disciple",
  "displayName": "소림 1대 제자",
  "definitionType": "Unit",
  "races": ["Human"],
  "rarity": "Common",
  "affiliation": "Murim",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "qi": 2
  },
  "specialEffectText": "Endure",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 13,
  "health": 33,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasEndure": true
}
```

<a id="card-a-111"></a>

### 30. A-111 (A-111)

| 항목 | 내용 |
|---|---|
| 이름 | A-111 |
| 종족 | 로봇 |
| cardId | `A-111` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 1 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 13 / 13 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 가능: 속공/소환 턴 공격 허용 |
| 전력 소모 | 1 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 있음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 17 / HP 22 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 전력 소모:1<br>속공 |
| 효과 요약 | 전력 소모:1, 속공 |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/A-111.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "A-111",
  "displayName": "A-111",
  "definitionType": "Unit",
  "races": ["Robot"],
  "rarity": "Common",
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 1
  },
  "specialEffectText": "전력 소모:1\n속공",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 13,
  "health": 13,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "isScience": true,
  "sciencePowerUpkeep": 1,

  "hasRush": true
}
```

<a id="card-a-301"></a>

### 31. A-301 (A-301)

| 항목 | 내용 |
|---|---|
| 이름 | A-301 |
| 종족 | 로봇 |
| cardId | `A-301` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 2 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 33 / 3 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 1 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 있음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 있음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 37 / HP 12 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 비행<br>전력 소모:1<br>복제 |
| 효과 요약 | 비행, 전력 소모:1, 복제 |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/A-301.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "A-301",
  "displayName": "A-301",
  "definitionType": "Unit",
  "races": ["Robot"],
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 2
  },
  "specialEffectText": "비행\n전력 소모:1\n복제",
  "attackType": "Ranged",
  "damageType": "Physical",
  "attack": 33,
  "health": 3,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "isScience": true,
  "sciencePowerUpkeep": 1,
  "hasFlying": true,

  "hasReplicate": true
}
```

<a id="card-robotfusion"></a>

### 32. 로봇 합체 (RobotFusion)

| 항목 | 내용 |
|---|---|
| 이름 | 로봇 합체 |
| cardId | `RobotFusion` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 3 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | Off |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 내 필드의 살아 있는 로봇 유닛을 2기 이상 선택해 합체한다. |
| 특수효과 문구 원문 | 선택한 로봇 중 전력 소모가 가장 높은 로봇이 남고, 나머지 로봇의 현재 ATK/HP를 합산해 강화 |
| 효과 요약 | 살아 있는 아군 로봇 유닛을 2기 이상 순서대로 선택한다. 전력 소모가 가장 높은 로봇이 남고, 동률이면 먼저 선택한 로봇이 남는다. 나머지 로봇의 현재 ATK/HP를 합산해 생존 로봇에게 부여하고 재료 로봇은 처치가 아닌 합체 제거로 없앤다. |
| 개별 효과 ID | `robot_fusion` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

합체 재료는 내 살아 있는 로봇 유닛 최소 2기이며, 같은 cardId도 허용한다. 전력 소모가 가장 높은 로봇이 생존하고 동률이면 먼저 선택한 로봇이 생존한다. 재료의 현재 ATK/HP만 합산하며 종족·효과·공격 횟수·데미지 유형·유지비·상태는 이전하지 않는다. 망각된 로봇도 종족이 유지되어 합체 재료가 될 수 있다. 봉인된 로봇은 재료에서 제외한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "RobotFusion",
  "displayName": "로봇 합체",
  "definitionType": "ScriptedSpell",
  "rarity": "Common",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "None",
  "cost": {
    "power": 3
  },
  "effectText": "내 필드의 살아 있는 로봇 유닛을 2기 이상 선택해 합체한다.",
  "specialEffectText": "선택한 로봇 중 전력 소모가 가장 높은 로봇이 남고, 나머지 로봇의 현재 ATK/HP를 합산해 강화",
  "damageType": "None",
  "effectId": "robot_fusion"
}
```

<a id="card-robotfactory"></a>

### 33. 로봇 공장 (RobotFactory)

| 항목 | 내용 |
|---|---|
| 이름 | 로봇 공장 |
| cardId | `RobotFactory` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 건물 / `Building` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 0 / 기 0 / 전력 1 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 50 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 (canAttack=false) |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 1 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | Off |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (includeInRewards=false) |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 HP +1 |
| Lv.13 결과 | ATK 0 / HP 63 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 시작 시 덱 빌딩에 포함되는 로봇 유닛 중 하나를 무작위로 손패에 추가 |
| 특수효과 문구 원문 | 전력 소모:1 |
| 효과 요약 | 전력 소모:1. 소유자 턴 시작에 `로봇 + 드래프트 On` 유닛 중 하나를 같은 확률로 손패에 생성한다. 현재 후보는 A-111, A-212, A-301이다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

공장마다 내 턴 시작 유지비 처리 후 1회 생성한다. 후보는 races에 Robot이 포함되고 includeInDraft=true인 유닛이며 현재 A-111, A-212, A-301이다. 후보별 동일 확률, 공장 처리 순서는 (0,0),(0,1),(1,0)…(4,1)이다. 손패 초과 생성 카드는 소멸하고 덱 고갈 피해는 발생하지 않는다. 상대에게 생성 카드의 정체는 숨긴다. 건물인 공장 자체에는 종족 항목이 없다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "RobotFactory",
  "displayName": "로봇 공장",
  "definitionType": "Building",
  "rarity": "Rare",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 1
  },
  "effectText": "내 턴 시작 시 덱 빌딩에 포함되는 로봇 유닛 중 하나를 무작위로 손패에 추가",
  "specialEffectText": "전력 소모:1",
  "damageType": "None",
  "attack": 0,
  "health": 50,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canAttack": false,
  "isScience": true,
  "sciencePowerUpkeep": 1
}
```

<a id="card-powerplant"></a>

### 34. 발전소 (PowerPlant)

| 항목 | 내용 |
|---|---|
| 이름 | 발전소 |
| cardId | `PowerPlant` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 건물 / `Building` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 1 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 30 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 (canAttack=false) |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 HP +1 |
| Lv.13 결과 | ATK 0 / HP 43 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 액티브: 1턴에 1번, 골드를 1 소모해서 전력을 2 얻는다. |
| 효과 요약 | 자신의 행동 턴에 타일을 클릭하고 액티브 버튼을 눌러 사용한다. 소환한 턴부터 발전소마다 턴당 1회, 골드 1 → 전력 2. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/PowerPlant.png` |

**판정 및 구분할 사항**

자동 교환은 하지 않는다. 소환한 턴부터 자신의 Main 단계에서 골드 1을 전부 지불할 수 있을 때만 전력 2를 얻는다. 발전소마다 턴당 한 번이며 망각·방전·봉인 중에는 사용할 수 없다. 재접속해도 사용 기록은 유지된다. 이 지불은 sciencePowerUpkeep가 아니라 별도 건물 효과이므로 전력 소모와 구분한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "PowerPlant",
  "displayName": "발전소",
  "definitionType": "Building",
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 1
  },
  "effectText": "",
  "specialEffectText": "액티브: 1턴에 1번, 골드를 1 소모해서 전력을 2 얻는다.",
  "damageType": "None",
  "attack": 0,
  "health": 30,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canAttack": false,
  "isScience": true,
  "sciencePowerUpkeep": 0
}
```

<a id="card-nuclearpowerplant"></a>

### 35. 원자력 발전소 (NuclearPowerPlant)

| 항목 | 내용 |
|---|---|
| 이름 | 원자력 발전소 |
| cardId | `NuclearPowerPlant` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 건물 / `Building` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 0 / 기 0 / 전력 5 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 30 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 (canAttack=false) |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 3 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 HP +1 |
| Lv.13 결과 | ATK 0 / HP 43 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 시작 시 전력 +3. 파괴될 때 양쪽 전장 전체에 30 물리 데미지. |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 소유자 턴 시작에 전력 +3. 파괴되면 양쪽 전장의 봉인되지 않은 모든 소환물과 마스터에게 물리 피해 30을 주며 다른 원자력 발전소도 연쇄 폭발할 수 있다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/NuclearPowerPlant.png` |

**판정 및 구분할 사항**

30 물리 폭발 피해는 damage 필드가 아니라 NuclearPowerPlantRules.DestructionDamage에서 정한다. 건물 강화는 HP만 올리고 생산량·폭발 피해는 올리지 않는다. 봉인 대상은 폭발 피해를 받지 않는다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "NuclearPowerPlant",
  "displayName": "원자력 발전소",
  "definitionType": "Building",
  "rarity": "Rare",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 5
  },
  "effectText": "내 턴 시작 시 전력 +3. 파괴될 때 양쪽 전장 전체에 30 물리 데미지.",
  "damageType": "None",
  "attack": 0,
  "health": 30,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canAttack": false,
  "isScience": true,
  "sciencePowerUpkeep": 0,
  "turnStartResourceGain": {
    "power": 3
  }
}
```

<a id="card-timedbomb"></a>

### 36. 시한폭탄 (TimedBomb)

| 항목 | 내용 |
|---|---|
| 이름 | 시한폭탄 |
| cardId | `TimedBomb` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 3 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 효과 피해 +1 |
| Lv.13 결과 | 효과 피해 46 (매 발동/대상 기준, 주문력·방어력 등 미반영) |
| 효과 문구 원문 | 사용 후 세 번째 턴 시작 시 상대방 타일 전체에 33 물리 데미지 |
| 특수효과 문구 원문 | 3턴 후 발동 |
| 효과 요약 | 사용 후 세 번째 턴 시작에 상대 전장 전체에 물리 피해 33을 준다. |
| 개별 효과 ID | `timed_bomb` |
| 피해 마법의 기본 피해 / 속성 | 33 / 물리 (Physical) |
| triggerCount 필드 | 3 (의미는 아래 효과 설명 참조) |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/TimedBomb.png` |

**판정 및 구분할 사항**

사용 후 상대 턴 시작을 1회, 내 턴 시작을 2회, 다시 상대 턴 시작을 3회로 세고 세 번째에 한 번 폭발한다. triggerCount=3은 피해 3회가 아니라 발동 대기 횟수다. 물리 피해이므로 주문력은 적용하지 않는다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "TimedBomb",
  "displayName": "시한폭탄",
  "definitionType": "ScriptedSpell",
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "None",
  "cost": {
    "power": 3
  },
  "effectText": "사용 후 세 번째 턴 시작 시 상대방 타일 전체에 33 물리 데미지",
  "specialEffectText": "3턴 후 발동",
  "damageType": "Physical",
  "damage": 33,
  "effectId": "timed_bomb",
  "triggerCount": 3
}
```

<a id="card-biochemicalbomb"></a>

### 37. 생화학폭탄 (BiochemicalBomb)

| 항목 | 내용 |
|---|---|
| 이름 | 생화학폭탄 |
| cardId | `BiochemicalBomb` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 0 / 기 0 / 전력 4 / 골드 1 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 효과 피해 +1 |
| Lv.13 결과 | 고정 피해 38 (매 발동/대상 기준, 주문력·방어력 미적용; 방전·피해 방지 판정 전) |
| 효과 문구 원문 | 상대방의 선택한 4X2 타일에 다음 4번의 전역 턴 시작마다 25 고정 데미지를 입힌다. |
| 특수효과 문구 원문 | 4턴 지속 |
| 효과 요약 | 상대 전장의 왼쪽 또는 오른쪽 4x2 범위를 고정한다. 다음 전역 턴 시작부터 4회, 범위의 현재 소환물과 마스터에게 고정 피해 25를 준다. |
| 개별 효과 ID | `biochemical_bomb` |
| 피해 마법의 기본 피해 / 속성 | 25 / 고정 (Fixed) |
| triggerCount 필드 | 4 (의미는 아래 효과 설명 참조) |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/BiochemicalBomb.png` |

**판정 및 구분할 사항**

상대 보드의 0~3열 또는 1~4열, 두 줄 전체를 선택한다. 사용 후 상대 턴 시작→내 턴 시작→상대 턴 시작→내 턴 시작, 총 4회 발동한다. 범위가 고정되므로 나중에 그 범위에 들어온 소환물도 피해 대상이다. 고정 피해이므로 물리·마법 방어력을 무시하고 주문력은 적용하지 않는다. 방전의 받는 피해 3배와 무적·불굴·봉인 판정은 유지한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "BiochemicalBomb",
  "displayName": "생화학폭탄",
  "definitionType": "ScriptedSpell",
  "rarity": "Uncommon",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "None",
  "cost": {
    "power": 4,
    "gold": 1
  },
  "effectText": "상대방의 선택한 4X2 타일에 다음 4번의 전역 턴 시작마다 25 고정 데미지를 입힌다.",
  "specialEffectText": "4턴 지속",
  "damageType": "Fixed",
  "damage": 25,
  "effectId": "biochemical_bomb",
  "triggerCount": 4
}
```

<a id="card-microreactor"></a>

### 38. 초소형 발전기 (Microreactor)

| 항목 | 내용 |
|---|---|
| 이름 | 초소형 발전기 |
| cardId | `Microreactor` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 마법 / 지속 자원 / `PersistentResourceSpell` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 0 / 기 0 / 전력 1 / 골드 0 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 1 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 다음 3번의 자기 턴 시작마다 전력 +1 |
| 특수효과 문구 원문 | Persistent Spell: Expires after next three turns. |
| 효과 요약 | 다음 3번의 소유자 턴 시작마다 전력 +1. |
| 개별 효과 ID | `Microreactor_Effect` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 3 |
| 지속 종료 문구 | Expires after next three turns. |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Microreactor.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Microreactor",
  "displayName": "초소형 발전기",
  "definitionType": "PersistentResourceSpell",
  "rarity": "Uncommon",
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "None",
  "cost": {
    "power": 1
  },
  "effectText": "다음 3번의 자기 턴 시작마다 전력 +1",
  "specialEffectText": "Persistent Spell: Expires after next three turns.",
  "damageType": "None",
  "effectId": "Microreactor_Effect",
  "turnStartResourceGain": {
    "power": 1
  },
  "ownerTurnStartsRemaining": 3,
  "endConditionText": "Expires after next three turns."
}
```

<a id="card-powerbank"></a>

### 39. 보조배터리 (PowerBank)

| 항목 | 내용 |
|---|---|
| 이름 | 보조배터리 |
| cardId | `PowerBank` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 마법 / 개별 효과 / `ScriptedSpell` |
| 문명 | 과학 문명 (ScienceCivilization) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 3 |
| 차지 타일 | 없음 / None |
| 기본 ATK / HP | 해당 없음: 마법 카드 |
| 기본 물리 / 마법 방어력 | 해당 없음 |
| 일반 공격 유형 | 일반 공격 없음 |
| 일반 공격 데미지 속성 | 해당 없음: 일반 공격 불가 |
| 일반 공격 가능 여부 | 불가 |
| 이동 가능 여부 | 불가 / 해당 없음 |
| 기본 턴당 공격 횟수 | 해당 없음 |
| 일반 공격 1회당 타격 수 | 해당 없음 |
| 소환한 턴 공격 | 해당 없음: 공격 불가 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 사용 시 전력 +6 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 사용 즉시 전력 +6. |
| 개별 효과 ID | `power_bank` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/PowerBank.png` |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "PowerBank",
  "displayName": "보조배터리",
  "definitionType": "ScriptedSpell",
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": false,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "None",
  "cost": {
    "gold": 3
  },
  "effectText": "사용 시 전력 +6",
  "damageType": "None",
  "damage": 0,
  "effectId": "power_bank"
}
```

<a id="card-goldminer"></a>

### 40. 금광 채굴꾼 (GoldMiner)

| 항목 | 내용 |
|---|---|
| 이름 | 금광 채굴꾼 |
| 종족 | 인간 |
| cardId | `GoldMiner` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 중립 (Neutral) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 2 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 3 / 15 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 1; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 7 / HP 24 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 턴 시작 시 골드 +1 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 소유자 턴 시작에 골드 +1. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/GoldMiner.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "GoldMiner",
  "displayName": "금광 채굴꾼",
  "definitionType": "Unit",
  "races": ["Human"],
  "rarity": "Common",
  "affiliation": "Neutral",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "gold": 2
  },
  "effectText": "턴 시작 시 골드 +1",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 3,
  "health": 15,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "turnStartResourceGain": {
    "gold": 1
  }
}
```

<a id="card-shieldbearer"></a>

### 41. 방패병 (Shieldbearer)

| 항목 | 내용 |
|---|---|
| 이름 | 방패병 |
| 종족 | 인간 |
| cardId | `Shieldbearer` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 중립 (Neutral) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 0 / 기 0 / 전력 0 / 골드 2 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 0 / 33 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 현재 ATK 0으로 공격 불가. ATK 증가 시 일반 유닛 규칙 적용 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 있음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 4 / HP 42 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 쉴더 |
| 효과 요약 | 쉴더: 바로 뒤 아군이 받는 일반 공격과 단일 대상 마법 피해를 대신 받으며 초과 피해는 원래 대상에게 전달한다. 관통으로 뒤 아군에게 발생하는 추가 피해도 정상적인 쉴더 판정을 거친다. ATK 0이므로 현재 일반 공격과 반격은 할 수 없다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/Shieldbearer.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

ATK=0이어서 현재 일반 공격·반격은 불가능하지만 이동 가능 유닛이다. 후열에 대한 관통 추가 피해도 쉴더가 대신 받으며, 매 피해마다 쉴더 방어력→남은 HP 초과분→보호 대상 방어력 순으로 처리한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Shieldbearer",
  "displayName": "방패병",
  "definitionType": "Unit",
  "races": ["Human"],
  "rarity": "Common",
  "affiliation": "Neutral",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "gold": 2
  },
  "specialEffectText": "쉴더",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 0,
  "health": 33,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasShielder": true
}
```


<a id="card-nuclear"></a>
### 43. 뉴클리어 (Nuclear)

| 항목 | 설정 |
|---|---|
| 종류 / 희귀도 / 문명 | ScriptedSpell / Legendary / ScienceCivilization |
| 비용 / 차지 타일 | 전력 10 / None |
| 효과 | (사용 즉시) 상대방 전장 전체에 고정 데미지 100을 입힌다. 사용 당시 상대 전장에 있는 유닛(마스터 포함)에 영구 낙진 +1중첩. 양쪽 턴 시작마다 중첩당 고정 피해 10. |
| 즉시 피해 대상·판정 | 적 마스터·유닛·건물. 광역 고정 피해이므로 방어력·주문력·쉴더 미적용; 무적·불굴·봉인·방전 및 사망 연쇄는 기존 피해 규칙 적용 |
| 낙진 | 사용 당시 상대 전장에 있는 유닛(마스터 포함)에 영구 부여. 건물은 중첩 부여와 낙진 피해에서 제외. 양쪽 턴 시작마다 중첩당 고정 피해 10. 망각으로 제거 불가, 고독으로 가져와도 유지 |
| 강화 | 가능. Lv.1–13 매 레벨 즉시 효과수치 +1; Lv.0 100 / Lv.1 101 / Lv.3 103 / Lv.13 113. 낙진 중첩·중첩당 피해는 불변 |
| 덱 빌딩 / 랜덤 보상 | 모두 포함 (On / On), 첫 레전더리 선택 및 기존 보상 확률 적용 |
| 능력치 / 종족 | 마법이므로 해당 없음 |
| 이미지 | 사용자 제작 `Assets/Project333/Resources/Project333/CardArtwork/Nuclear.png`, 원래 PNG/메타데이터 보존 |

카드 자산: `Assets/Project333/ScriptableObjects/StarterTen/Cards/Nuclear.asset`, `StarterTenCardCatalog`에 등록. 카드 이미지·출현 설정만 변경하며 즉시 피해·강화·영구 낙진 규칙은 유지한다.

세부 판정·전투 상태 보존: [뉴클리어와 낙진](nuclear_fallout.md).

원본 JSON:

```json
{
    "id":  "Nuclear",
    "displayName":  "뉴클리어",
    "definitionType":  "ScriptedSpell",
    "rarity":  "Legendary",
    "affiliation":  "ScienceCivilization",
    "chargeTileFootprint":  "None",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "cost":  {
                 "power":  10
             },
    "effectId":  "nuclear",
    "damage":  100,
    "effectText":  "(사용 즉시) 상대방 전장 전체에 고정 데미지 100을 입힌다. 사용 당시 상대 전장에 있는 유닛(마스터 포함)에 영구 낙진 +1중첩. 양쪽 턴 시작마다 중첩당 고정 피해 10.",
    "specialEffectText":  "낙진: 양쪽 턴 시작마다 중첩당 고정 피해 10을 받는다.",
    "damageType":  "Fixed"
}
```


<a id="card-magicamplificationcircle"></a>
### 44. 마법 증폭진 (MagicAmplificationCircle)

| 항목 | 설정 |
|---|---|
| 종류 / 문명 / 희귀도 | ScriptedSpell / Fantasy / Common |
| 비용 / 차지 타일 | 마나 1 / None |
| 효과 | 내 턴 종료 시까지 주문력+10을 얻는다. |
| 중첩 / 만료 | 시전마다 +10, 소유자의 턴 종료 효과 처리 후 만료 |
| 강화 / 덱 빌딩 / 랜덤 보상 | 불가 / 제외 / 제외 |
| 능력치 / 종족 | 마법이므로 해당 없음 |
| 이미지 | 별도 일러스트 미등록 |

[상세 판정과 검증](magic_amplification_circle.md).

원본 JSON:

```json
{
  "id": "MagicAmplificationCircle",
  "displayName": "마법 증폭진",
  "definitionType": "ScriptedSpell",
  "rarity": "Common",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "None",
  "includeInDraft": false,
  "includeInRewards": false,
  "cost": {
    "mana": 1
  },
  "effectId": "magic_amplification_circle",
  "effectText": "내 턴 종료 시까지 주문력+10을 얻는다."
}
```

<a id="card-earthquake"></a>
### 45. 지진 (Earthquake)

| 항목 | 설정 |
|---|---|
| 종류 / 희귀도 / 문명 | ScriptedSpell / Uncommon / Neutral |
| 비용 / 차지 타일 | 골드 3 / None |
| 효과 문구 원문 | 상대 전장 전체에 물리 데미지 10을 입힌다. 건물에는 3배의 데미지를 입힌다. |
| 효과·대상 | 대상 지정 없이 즉시 상대 전장의 현재 유닛·건물·마스터에게 각 1회 물리 피해. 아군은 직접 피해 제외; 빈 적 타일에도 균열 연출 표시 |
| 피해 판정 | 유닛·마스터 10+강화 레벨. 건물은 3×(10+강화 레벨)을 물리 방어 전에 계산. 주문력·쉴더·은신 회피·비행 감산 미적용; 봉인·무적·불굴·방전·사망 연쇄는 기존 규칙 적용 |
| 강화 | 가능. Lv.1-13 매 레벨 기본 물리 피해 +1. 골드 비용과 건물 3배 계수는 불변 |
| Lv.0 / Lv.1 / Lv.3 / Lv.13 | 유닛·마스터 10/11/13/23, 건물 30/33/39/69(방어·상태 적용 전) |
| 사용 / 지속 | 내 행동 턴에 성공한 사용에서 비용·정확한 손패 카드 1장을 소비하고 버림패로 이동. 지속 효과 생성 없음 |
| 덱 빌딩 / 랜덤 보상 | On / On, 기존 언커먼 제시·보상 규칙 적용 |
| 능력치 / 종족 | 마법이므로 해당 없음 |
| 이미지 | 사용자 제작 `Assets/Project333/Resources/Project333/CardArtwork/Earthquake.png` 및 `.meta` 보존 |
| 카드 자산 | `Assets/Project333/ScriptableObjects/StarterTen/Cards/Earthquake.asset`, `StarterTenCardCatalog` 등록 |
| 보드 연출·음원 | 적 10타일 균열, 1.15초 연출·0.85초 작은 화면 흔들림, 조용한 1.08초 rumble/gain 0.30. [편집·제작 자료](earthquake_presentation.md) |

Lv.13에서 물리 방어 4인 건물은 방어 후 65 피해를 받는다. 건물 계수를 방어 적용 후에 곱하지 않는다. 지진의 모든 피해를 먼저 적용하고 사망 제거·연쇄 폭발·마스터 결과를 처리한다.

원본 JSON:

```json
{
    "id":  "Earthquake",
    "displayName":  "지진",
    "definitionType":  "ScriptedSpell",
    "rarity":  "Uncommon",
    "affiliation":  "Neutral",
    "chargeTileFootprint":  "None",
    "cost":  {
                 "gold":  3
             },
    "effectText":  "상대 전장 전체에 물리 데미지 10을 입힌다. 건물에는 3배의 데미지를 입힌다.",
    "specialEffectText":  "",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "effectId":  "earthquake",
    "damage":  10,
    "damageType":  "Physical"
}
```


<a id="card-meteor"></a>
### 46. 메테오 (Meteor)

| 항목 | 설정 |
|---|---|
| 종류 / 희귀도 / 문명 | ScriptedSpell / Unique / Fantasy; 2026-09-30 사용자 판타지 소속 및 후속 유니크 변경 확정 |
| 비용 / 차지 타일 | 마나 7 / None; 부족한 마나는 기존 골드 1:1 대체 규칙 적용 |
| 효과 문구 원문 | 상대 전장 전체에 마법 데미지 40을 입힌다. |
| 효과·대상 | 대상 지정 없이 즉시 상대 전장의 현재 유닛·건물·마스터에게 각 1회 마법 피해. 아군 직접 피해 제외, 지속 효과 없음 |
| 피해 판정 | 40+강화 레벨에 현재 아군 주문력을 대상마다 한 번 더한 뒤 마법 방어·보호 적용. 건물 배수·쉴더 대리피격·은신 회피·비행 감산 없음. 봉인·무적·불굴·방전·부활·사망 연쇄는 기존 규칙 적용 |
| 강화 | 가능. Lv.1-13 매 레벨 기본 마법 피해 +1. 마나 비용 고정 |
| Lv.0 / Lv.1 / Lv.3 / Lv.13 | 기본 피해 40/41/43/53; 주문력·방어·보호 적용 전 |
| 사용 / 처리 순서 | 내 행동 턴에 성공한 사용에서 비용·정확한 손패 카드 1장을 소비하고 버림패로 이동. 모든 직접 피해 패킷 적용 후 사망 제거·부활·연쇄 폭발·마스터 결과 처리 |
| 덱 빌딩 / 랜덤 보상 | On / On, 기존 유니크 제시·보상 규칙 적용 |
| 능력치 / 종족 | 마법이므로 해당 없음 |
| 이미지 | 사용자 제작 `Assets/Project333/Resources/Project333/CardArtwork/Meteor.png` 바이트 보존; 기존에 없던 metadata는 네이티브 Unity API 생성 |
| 카드 자산 | `Assets/Project333/ScriptableObjects/StarterTen/Cards/Meteor.asset`, `StarterTenCardCatalog` 등록 |
| 보드 연출·음원 | 적 전장 중심으로 거대 운석 하강 0.65초→폭발/tail 1.15초, 전체 1.80초. Incoming 0.65초/Impact 1초 quiet WAV 각각 gain 0.30. [편집·제작 자료](meteor_presentation.md) |

Lv.13과 주문력 20일 때 각 대상의 incoming 피해는 73이다. 마법 방어 4라면 다른 보호 적용 전 69이며 건물도 같은 계산을 사용한다. 화면 연출은 서버/공유 판정을 재계산하지 않는다.

원본 JSON:

```json
{
    "id":  "Meteor",
    "displayName":  "메테오",
    "definitionType":  "ScriptedSpell",
    "rarity":  "Unique",
    "affiliation":  "Fantasy",
    "chargeTileFootprint":  "None",
    "cost":  {
                 "mana":  7
             },
    "effectText":  "상대 전장 전체에 마법 데미지 40을 입힌다.",
    "specialEffectText":  "",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "effectId":  "meteor",
    "damage":  40,
    "damageType":  "Magic"
}
```

<a id="card-orcberserker"></a>
### 47. 오크 광전사 (OrcBerserker)

| 항목 | 내용 |
|---|---|
| 이름 | 오크 광전사 |
| 종족 | 오크 |
| cardId | `OrcBerserker` |
| 현재 상태 | 사용자 규칙 확정 및 JSON/네이티브 카드 등록. 실행 검증·실제 기기 검수는 별도 결과로 기록 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 5 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 20 / 50 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 있음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 24 / HP 59 (버서커 등 전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | Berserker |
| 효과 요약 | 별도 카드 효과 없음. 버서커로 잃은 HP 1당 현재 ATK +1; 회복하면 감소하고 방전·망각·봉인 중에는 억제 |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/OrcBerserker.png` |
| 카드 자산 | `Assets/Project333/ScriptableObjects/StarterTen/Cards/OrcBerserker.asset`, `StarterTenCardCatalog` 등록 |
| 보드 Animator | 기존 `Assets/Unit/Orc/OrcWarrior.controller` 공유. 기존 오크 전사 애니메이션은 수정하지 않음 |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
    "id":  "OrcBerserker",
    "displayName":  "오크 광전사",
    "definitionType":  "Unit",
    "races":  [
                  "Orc"
              ],
    "rarity":  "Uncommon",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "affiliation":  "Fantasy",
    "chargeTileFootprint":  "OneByOne",
    "cost":  {
                 "mana":  5
             },
    "effectText":  "",
    "specialEffectText":  "Berserker",
    "attackType":  "Melee",
    "damageType":  "Physical",
    "attack":  20,
    "health":  50,
    "physicalDefense":  0,
    "magicDefense":  0,
    "canMove":  true,
    "hasBerserker":  true
}
```

Lv.13에서 최대 HP 59, 현재 HP 39라면 손실 HP 20으로 버서커 적용 ATK는 44다. 회복하거나 효과가 억제되면 공용 버서커 규칙으로 다시 계산하며 공격력을 영구 누적하지 않는다.

| 강화 레벨 | 기본 ATK | 기본 HP |
|---|---:|---:|
| 0 | 20 | 50 |
| 1 | 20 | 51 |
| 2 | 20 | 52 |
| 3 | 21 | 52 |
| 4 | 21 | 53 |
| 5 | 21 | 54 |
| 6 | 22 | 54 |
| 7 | 22 | 55 |
| 8 | 22 | 56 |
| 9 | 23 | 56 |
| 10 | 23 | 57 |
| 11 | 23 | 58 |
| 12 | 23 | 59 |
| 13 | 24 | 59 |

<a id="card-orcchieftain"></a>
### 48. 오크 족장 (OrcChieftain)

| 항목 | 내용 |
|---|---|
| 이름 | 오크 족장 |
| 종족 | 오크 |
| cardId | `OrcChieftain` |
| 현재 상태 | 사용자 규칙과 JSON/네이티브 카드 등록 확인. 실행 검증·실제 기기 검수는 별도 결과로 기록 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 7 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 50 / 70 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | 기본 ATK 54 / HP 79. 자기 오라 1개 적용 시 64 / 89, 다른 전투 효과 미반영 |
| 효과 문구 원문 | 내 전장의 현재·이후 오크 유닛(자신 포함)에게 공격력 +10, 최대·현재 체력 +10. 이 오크 족장이 전장을 떠나면 해당 버프가 사라집니다. 중첩 가능. |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 내 전장의 현재·이후 오크 유닛과 자신에게 족장별 ATK/최대·현재 HP +10. 여러 족장 중첩, 해당 족장 퇴장 때 각 −10, HP≤0 연쇄 사망 가능 |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지·보드 연결 | 현재 사용자 CardArtwork/OrcChieftain.png를 CardArtworkLibrary가 우선 사용한다. 전용 PNG가 없을 때만 기존 CardArtwork/OrcWarrior.png alias를 사용한다. 네이티브 카드의 보드 Animator는 전용 Assets/Unit/OrcChieftian/OrcChieftian.controller에 연결되어 있다(Chieftian은 실제 폴더·파일명). 오크 광전사의 OrcWarrior.controller 공유와 구분한다 |
| 네이티브 카드 경로 | Assets/Project333/ScriptableObjects/StarterTen/Cards/OrcChieftain.asset. StarterTenCardCatalog에 1회 등록 |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
    "id":  "OrcChieftain",
    "displayName":  "오크 족장",
    "definitionType":  "Unit",
    "races":  [
                  "Orc"
              ],
    "rarity":  "Rare",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "affiliation":  "Fantasy",
    "chargeTileFootprint":  "OneByOne",
    "cost":  {
                 "mana":  7
             },
    "effectText":  "내 전장의 현재·이후 오크 유닛(자신 포함)에게 공격력 +10, 최대·현재 체력 +10. 이 오크 족장이 전장을 떠나면 해당 버프가 사라집니다. 중첩 가능.",
    "specialEffectText":  "",
    "attackType":  "Melee",
    "damageType":  "Physical",
    "attack":  50,
    "health":  70,
    "physicalDefense":  0,
    "magicDefense":  0,
    "canMove":  true
}
```

오라는 카드/강화 기본 능력치와 분리한다. Lv.13 기본 54/79에 자기 오라 1개를 더하면 64/89다. 한 source의 HP 기여를 제거할 때 최대 HP와 현재 HP를 각각 10씩 내리므로, 예를 들어 오라 적용 중 현재 HP 8인 수신자는 해당 source 퇴장 시 HP −2가 되어 기존 사망 처리 대상으로 이어진다.

방전·망각·봉인 중인 족장은 오라를 내보내지 않는다. 수신자 방전은 기존 버프 수신과 동일하고, 수신자 봉인은 새 오라 부여를 막는다. 수신자 망각 적용은 기존 ATK/최대 HP baseline reset와 현재 HP clamp를 그대로 사용하며 오라 source별 통상 현재 HP −10 제거와 구분한다. 지워진 기존 source는 tombstone으로 기억해 즉시 다시 부여하지 않고, 새 족장 source는 망각 이후에도 일반 버프로 부여할 수 있다. 이미 지워진 source가 나중에 퇴장해도 이 기여를 중복 차감하지 않는다.

봉인된 수신자는 새 source 버프를 받지 않는다. 이미 받은 기여는 해당 source가 유효한 동안 유지하지만 source 파괴·퇴장·효과 억제 시에는 봉인 중이라도 그 기여의 ATK/최대·현재 HP 10을 즉시 회수한다. 봉인 해제 후에는 현재 유효한 source 중 아직 받지 않은 기여를 정상 적용하며 망각의 기존 source tombstone 규칙도 유지한다. 족장 자신이 봉인되면 자신의 source 기여를 전부 회수하고, 다른 유효한 족장에서 이미 받은 기여는 유지한다.

| 강화 레벨 | 기본 ATK | 기본 HP | 자기오라 ATK | 자기오라 HP |
|---|---:|---:|---:|---:|
| 0 | 50 | 70 | 60 | 80 |
| 1 | 50 | 71 | 60 | 81 |
| 2 | 50 | 72 | 60 | 82 |
| 3 | 51 | 72 | 61 | 82 |
| 4 | 51 | 73 | 61 | 83 |
| 5 | 51 | 74 | 61 | 84 |
| 6 | 52 | 74 | 62 | 84 |
| 7 | 52 | 75 | 62 | 85 |
| 8 | 52 | 76 | 62 | 86 |
| 9 | 53 | 76 | 63 | 86 |
| 10 | 53 | 77 | 63 | 87 |
| 11 | 53 | 78 | 63 | 88 |
| 12 | 53 | 79 | 63 | 89 |
| 13 | 54 | 79 | 64 | 89 |

<a id="card-skeletonarcher"></a>

### 49. 스켈레톤아처 (SkeletonArcher)

| 항목 | 내용 |
|---|---|
| 이름 | 스켈레톤아처 |
| 종족 | 언데드 |
| cardId | `SkeletonArcher` |
| 현재 상태 | 사용자 규칙과 JSON/네이티브 카드 등록 확인. 실행 검증·실제 기기 검수는 별도 결과로 기록 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 1 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 10 / 3 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 14 / HP 12 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 별도 효과 없음. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지·보드 연결 | 전용 SkeletonArcher.png가 있으면 우선 사용하고, 없으면 CardArtworkLibrary에서 기존 CardArtwork/Skeleton.png alias를 사용한다. 기존 Assets/Unit/Skeleton/Idle.png Idle_0 및 Skeleton.controller 공유; 새 그림·애니메이션·음원 없음 |
| 네이티브 카드 경로 | Assets/Project333/ScriptableObjects/StarterTen/Cards/SkeletonArcher.asset. StarterTenCardCatalog에 1회 등록 |

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
    "id":  "SkeletonArcher",
    "displayName":  "스켈레톤아처",
    "definitionType":  "Unit",
    "races":  [
                  "Undead"
              ],
    "rarity":  "Common",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "affiliation":  "Fantasy",
    "chargeTileFootprint":  "OneByOne",
    "cost":  {
                 "mana":  1
             },
    "effectText":  "",
    "specialEffectText":  "",
    "attackType":  "Ranged",
    "damageType":  "Physical",
    "attack":  10,
    "health":  3,
    "physicalDefense":  0,
    "magicDefense":  0,
    "canMove":  true
}
```

원거리 공격은 기존 대상 지정·피해·반격 규칙을 적용하며, 별도 관통·주문력·소환 효과를 추가하지 않는다. 강화는 공격력과 HP만 증가하고 마나 비용·방어력·언데드 종족·기본 특수효과 없음은 유지한다.

| 강화 레벨 | ATK | HP |
|---|---:|---:|
| 0 | 10 | 3 |
| 1 | 10 | 4 |
| 2 | 10 | 5 |
| 3 | 11 | 5 |
| 4 | 11 | 6 |
| 5 | 11 | 7 |
| 6 | 12 | 7 |
| 7 | 12 | 8 |
| 8 | 12 | 9 |
| 9 | 13 | 9 |
| 10 | 13 | 10 |
| 11 | 13 | 11 |
| 12 | 13 | 12 |
| 13 | 14 | 12 |

<a id="card-elf-elder"></a>

### 50. 엘프 장로 (ElfElder)

| 항목 | 내용 |
|---|---|
| 이름 | 엘프 장로 |
| 종족 | 엘프 |
| cardId | `ElfElder` |
| 현재 상태 | 사용자 규칙과 JSON/네이티브 카드 등록 확인. 실행 검증·실제 기기 검수는 별도 결과로 기록 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 5 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 30 / 25 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 |
| 소환한 턴 공격 | 가능: 활성 속공이 소환 후 공격 제한을 해제하며 ATK·상태·공격 횟수·대상 조건은 유지 |
| 전력 소모 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 속공: 있음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 있음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 34 / HP 34 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | 관통<br>속공 (JSON 줄바꿈으로 구분) |
| 효과 요약 | 카드 효과 없음. 관통과 속공의 기존 규칙을 적용 |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지·보드 연결 | 현재 전용 CardArtwork/ElfElder.png와 Assets/Unit/ElfElder/Idle.png·ElfElder.controller를 사용한다. 사용자가 교체한 그림·애니메이션 및 GUID는 보존한다. 전용 카드 그림이 없을 때만 ElfLongbowScout.png를 대체 사용한다. |
| 네이티브 카드 경로 | Assets/Project333/ScriptableObjects/StarterTen/Cards/ElfElder.asset. StarterTenCardCatalog에 1회 등록 |

**판정 및 구분할 사항**

전열 공격 시 같은 열 후열, 후열 공격 시 같은 열 전열에 동일 타격 ATK·물리 속성으로 관통 피해를 준다. 관통 추가 피해로 별도 반격은 발생하지 않고 쉴더 피해 전환은 적용한다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
    "id":  "ElfElder",
    "displayName":  "엘프 장로",
    "definitionType":  "Unit",
    "races":  [
                  "Elf"
              ],
    "rarity":  "Rare",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "affiliation":  "Fantasy",
    "chargeTileFootprint":  "OneByOne",
    "cost":  {
                 "mana":  5
             },
    "effectText":  "",
    "specialEffectText":  "관통\n속공",
    "attackType":  "Ranged",
    "damageType":  "Physical",
    "attack":  30,
    "health":  25,
    "physicalDefense":  0,
    "magicDefense":  0,
    "canMove":  true,
    "hasPiercing":  true,
    "hasRush":  true
}
```

CardId는 사용자 변경(2026-10-09)에 따라 공백 없는 `ElfElder`로 통일한다. 데이터·서버·리소스·네이티브 자산은 같은 ID를 사용한다. 기존 DB 저장 ID는 마이그레이션 0024로 변환한다. 속공은 소환한 턴의 공격 제한만 없애며 공격 횟수를 늘리지 않는다. 관통·방전·망각·봉인·쉴더 및 원거리 반격 여부는 기존 공용 판정을 적용한다.

강화는 ATK와 HP만 증가한다. 마나 비용·방어력·엘프 종족·관통·속공은 강화로 변하지 않는다.

| 강화 레벨 | ATK | HP |
|---|---:|---:|
| 0 | 30 | 25 |
| 1 | 30 | 26 |
| 2 | 30 | 27 |
| 3 | 31 | 27 |
| 4 | 31 | 28 |
| 5 | 31 | 29 |
| 6 | 32 | 29 |
| 7 | 32 | 30 |
| 8 | 32 | 31 |
| 9 | 33 | 31 |
| 10 | 33 | 32 |
| 11 | 33 | 33 |
| 12 | 33 | 34 |
| 13 | 34 | 34 |

<a id="card-gaebangchief"></a>
### 51. 개방 방주 (GaebangChief)

| 항목 | 내용 |
|---|---|
| 이름 | 개방 방주 |
| 종족 | 인간 (Human) |
| cardId | `GaebangChief` |
| 현재 상태 | 사용자 카드·효과·강화 규칙 확정. 실행 검증과 실제 기기 검수 결과는 별도로 기록 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 / 희귀도 | 무림 (Murim) / 유니크 (Unique) |
| 사용 비용 | 마나 0 / 기 7 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 10 / 60 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 / 데미지 속성 | 근거리 (Melee) / 물리 (Physical) |
| 일반 공격 가능 여부 / 이동 가능 여부 | 가능 / 가능. 턴·상태·대상 등 일반 조건을 적용 |
| 기본 턴당 공격 횟수 / 일반 공격 1회당 타격 수 | 1회 / 1회 |
| 소환한 턴 공격 | 불가. 기본 속공 없음 |
| 전력 소모 / isScience | 0 / false |
| 기본 내 턴 시작 자원 생산 | 마나 0 / 기 0 / 전력 0 / 골드 0 |
| 초기 특수효과 플래그 | 속공·복제·버서커·불굴·쉴더·흡혈·은신·비행·관통 모두 없음 |
| 초기 주문력 / 소환 직후 봉인 / 무적 | 0 / 0 / None |
| 덱 빌딩 / 랜덤 보상 설정 / 실제 랜덤 보상 | On / On / 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 기본 ATK +1, 나머지 Lv.1–13 HP +1 |
| Lv.13 카드 표시 수치 | 기본 ATK 14 / HP 69. 현재 골드에 따른 전투 ATK는 별도 계산 |
| 효과 문구 원문 | 현재 소유한 골드 1당 공격력 +10. 골드가 변하면 즉시 반영되며 골드를 소모하지 않습니다. |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 살아 있고 효과가 활성화된 동안 현재 소유자의 전투 골드 ×10을 현재 ATK에 더함. 골드 미소모 |
| 개별 효과 ID | 없음. CardId 기반 동적 ATK 규칙이며 고유 특수효과 플래그를 추가하지 않음 |
| 카드 이미지 | `Assets/Project333/Resources/Project333/CardArtwork/GaebangChief.png`. 사용자 제공 원본 전체를 그대로 복사하며 기존 카드 그림은 보존 |
| 네이티브 카드 경로 | `Assets/Project333/ScriptableObjects/StarterTen/Cards/GaebangChief.asset`; StarterTenCardCatalog에 1회 등록 |
| 보드 Animator | `Assets/Unit/GaebangChief/GaebangChief.controller`. Idle·Attack·Run·Death 각 8컷 제작, 컷마다 512×512px와 하단 약 16px 접지 여백. 여섯 상태 연결은 제작·임포트 기록에서 확인 |
| 공격음 | `Project333/Units/GaebangChief/Audio/CoinPunch`. 잔잔한 주먹 바람·은은한 금화 소리 0.44초, mono PCM16/44.1kHz, 초기 gain 0.55. 피해 소리는 기존 공통 UnitHit 사용 |

**판정 및 구분할 사항**

현재 전투 ATK는 저장된 `BaseAttack`과 현재 소유자의 전투 골드 ×10 보너스를 구분해 계산한다. 카드·강화 화면에는 골드 보너스를 넣지 않는다. Lv.0은 기본 10/60, Lv.13은 기본 14/69이며 골드 3을 보유한 활성 유닛의 전투 ATK는 각각 40과 44다. 계정 Resource Gold는 이 효과의 자원이 아니다.

골드 획득·소비·비용 지불 직후 현재 잔액을 즉시 읽으며 효과 자체는 골드를 소모하지 않는다. 기 등 부족한 비용을 골드로 대신 지불하면 실제 지불을 끝낸 뒤 남은 골드로 보너스를 계산한다. 고독으로 소유자가 바뀌면 새 현재 소유자의 골드를 즉시 사용하며 이전 소유자의 골드 보너스를 저장·이전하지 않는다.

사망·방전·망각·봉인 중에는 이 동적 골드 보너스가 0이다. 인간 종족은 유지한다. 일반 공격과 적법한 근접 반격의 ATK에 같은 계산을 사용하며, 기존 공격 행동에서 ATK를 확정하는 시점과 피해·반격·쉴더 규칙은 유지한다. 전투 뷰·재접속은 기본 ATK와 표시용 현재 ATK를 구분하여 보너스를 중복 저장하거나 재적용하지 않는다.

**원본 카드 데이터: 설정된 모든 필드**

```json
    {
      "id": "GaebangChief",
      "displayName": "개방 방주",
      "definitionType": "Unit",
      "rarity": "Unique",
      "affiliation": "Murim",
      "chargeTileFootprint": "OneByOne",
      "cost": { "qi": 7 },
      "effectText": "현재 소유한 골드 1당 공격력 +10. 골드가 변하면 즉시 반영되며 골드를 소모하지 않습니다.",
      "specialEffectText": "",
      "includeInDraft": true,
      "includeInRewards": true,
      "attackType": "Melee",
      "damageType": "Physical",
      "attack": 10,
      "health": 60,
      "physicalDefense": 0,
      "magicDefense": 0,
      "canMove": true,
      "races": ["Human"],
      "isScience": false,
      "sciencePowerUpkeep": 0
    }
```

| 강화 레벨 | 기본 ATK | 기본 HP | 골드 3 보유 시 전투 ATK |
|---|---:|---:|---:|
| 0 | 10 | 60 | 40 |
| 1 | 10 | 61 | 40 |
| 2 | 10 | 62 | 40 |
| 3 | 11 | 62 | 41 |
| 4 | 11 | 63 | 41 |
| 5 | 11 | 64 | 41 |
| 6 | 12 | 64 | 42 |
| 7 | 12 | 65 | 42 |
| 8 | 12 | 66 | 42 |
| 9 | 13 | 66 | 43 |
| 10 | 13 | 67 | 43 |
| 11 | 13 | 68 | 43 |
| 12 | 13 | 69 | 43 |
| 13 | 14 | 69 | 44 |

강화로 기 비용·방어·이동·인간 종족·골드당 ATK +10 계수는 바뀌지 않는다.

<a id="card-manacannon"></a>
### 52. 마나 대포 (ManaCannon)

| 항목 | 내용 |
|---|---|
| 이름 | 마나 대포 |
| 종족 | 없음 / `races: []` |
| cardId | `ManaCannon` |
| 현재 상태 | 사용자 카드·효과·강화 규칙 확정. 네이티브 등록과 실행·기기 검증 결과는 각각 구분 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 / 희귀도 | 판타지 (Fantasy) / 언커먼 (Uncommon) |
| 사용 비용 | 마나 2 / 기 0 / 전력 0 / 골드 0 |
| 공격 행동 비용 | 적법한 일반 공격 1회마다 마나 1. 부족한 마나 1은 골드 1로 대체 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 40 / 10 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 / 데미지 속성 | 원거리 (Ranged) / 마법 (Magic) |
| 일반 공격 가능 여부 / 이동 가능 여부 | 가능 / 가능. 턴·상태·대상·공격 비용 조건을 모두 적용 |
| 기본 턴당 공격 횟수 / 일반 공격 1회당 타격 수 | 1회 / 1회 |
| 소환한 턴 공격 | 불가. 기본 속공 없음 |
| 전력 소모 / isScience | 0 / false |
| 기본 내 턴 시작 자원 생산 | 마나 0 / 기 0 / 전력 0 / 골드 0 |
| 초기 특수효과 플래그 | 속공·복제·버서커·불굴·쉴더·흡혈·은신·비행·관통 모두 없음 |
| 초기 주문력 / 소환 직후 봉인 / 무적 | 0 / 0 / None |
| 덱 빌딩 / 랜덤 보상 설정 / 실제 랜덤 보상 | On / On / 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 기본 ATK +1, 나머지 Lv.1–13 HP +1 |
| Lv.13 카드 표시 수치 | 기본 ATK 44 / HP 19 |
| 효과 문구 원문 | 공격할 때마다 마나 1 소모. 부족한 마나는 골드로 대신 지불할 수 있습니다. |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 현재 소유자의 자원에서 공격 행동당 한 번 지불. 검증 실패 시 미차감, 망각 중에는 비용 효과 억제 |
| 개별 효과 ID | 없음. CardId 기반 공격 비용 규칙이며 고유 특수효과 플래그를 추가하지 않음 |
| 카드 이미지 경로 | `Assets/Project333/Resources/Project333/CardArtwork/ManaCannon.png` |
| 네이티브 카드 경로 | `Assets/Project333/ScriptableObjects/StarterTen/Cards/ManaCannon.asset`. 카탈로그 등록 확인. 트랩: 리턴·트랩-미러 필드를 포함한 현재 카탈로그 53종. 실행 검증 결과는 별도 기록 |
| 보드 Animator 경로 | `Assets/Unit/ManaCannon/ManaCannon.controller`. PNG → 클립 → 컨트롤러 → 카드의 기존 연결을 사용하며 실제 분할·모션·임포트 결과는 별도 제작 기록에서 확인 |
| 공격·마나 폭발음 | `Project333/Units/ManaCannon/Audio/ManaShot`(0.400초)·`Project333/Units/ManaCannon/Audio/ManaExplosion`(0.350초). 발사 0.300초/Attack 10fps frame 3에 맞춘 quiet mono PCM16/44.1kHz, 제안 gain 0.45/0.40. 기존 공통 UnitHit는 유지 |

**판정 및 구분할 사항**

소환 비용 마나 2와 공격 행동 비용 마나 1은 별개다. 일반 공격의 턴·공격자·남은 공격 횟수·상태·선언 대상·환술 후 확정 대상 검증을 모두 마친 뒤 현재 소유자의 자원에서 공격 행동당 한 번 차감한다. 마나를 우선 사용하며 마나가 없으면 골드 1로 대신 지불한다. 마나·골드 모두 부족하거나 공격이 유효하지 않으면 자원과 남은 공격 횟수를 소비하지 않는다.

환술 대상 변경·쉴더 피해 이전·피해 대상 수는 이미 지불한 공격 비용을 다시 부과하지 않는다. 보호·무적·방어로 실제 피해가 0이 되더라도 유효한 공격 행동은 비용을 지불한다. 별도로 공격을 한 번 더 선언하면 그 행동의 비용은 다시 확인한다. 기본 원거리 유닛이므로 일반 반격은 하지 않는다.

망각은 이 카드의 공격 비용 효과를 억제한다. 나머지 일반 공격 가능 조건을 만족한다면 마나·골드 지불 없이 공격할 수 있으며 원거리·마법·이동·강화 기준 수치와 종족 없음은 유지한다. 방전·봉인·소환 턴 공격 제한은 기존 규칙을 따른다. 고독으로 이전되면 새 현재 소유자의 자원을 사용한다. 카드 공격 비용은 계정 Resource Gold가 아닌 전투 자원이다.

원거리 마법 피해는 기존 현재 주문력·마법 방어·쉴더·보호 규칙을 적용한다. 새 폭발 VFX와 효과음은 공격 피해·지불·타겟 판정에 영향을 주지 않는다. 망각·고독·재접속·AI 등 실행 검증 완료 여부는 실제 실행 기록에서 따로 확인한다.

**원본 카드 데이터: 설정된 모든 필드**

```json
    {
      "id": "ManaCannon",
      "displayName": "마나 대포",
      "definitionType": "Unit",
      "races": [],
      "rarity": "Uncommon",
      "affiliation": "Fantasy",
      "includeInDraft": true,
      "includeInRewards": true,
      "chargeTileFootprint": "OneByOne",
      "cost": { "mana": 2 },
      "effectText": "공격할 때마다 마나 1 소모. 부족한 마나는 골드로 대신 지불할 수 있습니다.",
      "specialEffectText": "",
      "attackType": "Ranged",
      "damageType": "Magic",
      "attack": 40,
      "health": 10,
      "physicalDefense": 0,
      "magicDefense": 0,
      "canMove": true
    }
```

| 강화 레벨 | 기본 ATK | 기본 HP | 일반 공격 행동 비용 |
|---|---:|---:|---|
| 0 | 40 | 10 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 1 | 40 | 11 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 2 | 40 | 12 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 3 | 41 | 12 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 4 | 41 | 13 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 5 | 41 | 14 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 6 | 42 | 14 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 7 | 42 | 15 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 8 | 42 | 16 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 9 | 43 | 16 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 10 | 43 | 17 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 11 | 43 | 18 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 12 | 43 | 19 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |
| 13 | 44 | 19 | 마나 1 (부족 시 골드 1), 망각 중 미부과 |

강화로 소환 마나 2·공격 마나 1·골드 1:1 대체·방어·이동·종족 없음은 바뀌지 않는다.

<a id="card-firespear"></a>

### 53. 파이어스피어 (FireSpear)

| 항목 | 설정 |
|---|---|
| 이름 / CardId | 파이어스피어 / `FireSpear` |
| 종류 / 효과 ID | 즉시 마법 / `ScriptedSpell`, `fire_spear` |
| 소속 / 희귀도 | 판타지 / 커먼 |
| 비용 | 마나 3. 부족한 마나는 골드로 1:1 대체 |
| 사용 대상 | 살아 있는 아군 또는 적 유닛·건물·마스터 |
| 적용 범위 | 선택한 대상 전장의 같은 열 앞·뒤 2칸 |
| 직접 선택 불가 | 빈 칸·봉인된 대상·은신한 적. 은신한 아군은 선택 가능 |
| 피해 / 발동 | 기본 마법 피해 15 / 즉시 각 소환물에 1회 |
| 주문력 / 방어 | 시전자 현재 주문력을 한 번 읽어 각 대상에 추가 후 마법 방어 적용 |
| 특수효과 / 차지 타일 | 없음 / None. 유닛의 hasPiercing와 별도인 마법 범위 규칙 |
| 덱 빌딩 / 랜덤 보상 | 등장 / 등장 |
| 강화 | Lv.1–13 매 레벨 피해 +1, Lv.13 기본 피해 28 |
| 카드 그림 | `Assets/Project333/Resources/Project333/CardArtwork/FireSpear.png` |
| 네이티브 카드 | `Assets/Project333/ScriptableObjects/StarterTen/Cards/FireSpear.asset` |
| 관통 VFX | `Project333/SpellEffects/FireSpear/FlameSpear`, 512px 8컷 |

같은 열 반대 칸은 별도로 지정하지 않는 광역 피해다. 은신·쉴더 이전·비행 반감은 적용하지 않고 봉인·무적·불굴·방전·피해 후 파괴 및 부활은 기존 규칙을 따른다. 다른 열과 반대편 전장에는 직접 피해를 주지 않는다. 비용과 손패 카드는 한 번만 소비한다. UI는 게이트웨이가 확정한 결과를 표시한다.

| 레벨 | 주문력·방어 적용 전 기본 피해 |
|---:|---:|
| 0 | 15 |
| 1 | 16 |
| 2 | 17 |
| 3 | 18 |
| 4 | 19 |
| 5 | 20 |
| 6 | 21 |
| 7 | 22 |
| 8 | 23 |
| 9 | 24 |
| 10 | 25 |
| 11 | 26 |
| 12 | 27 |
| 13 | 28 |

원본 데이터:

```json
{
    "id":  "FireSpear",
    "displayName":  "파이어스피어",
    "definitionType":  "ScriptedSpell",
    "rarity":  "Common",
    "affiliation":  "Fantasy",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "chargeTileFootprint":  "None",
    "cost":  {
                 "mana":  3
             },
    "effectText":  "선택한 대상이 속한 전장의 같은 열 소환물 모두에게 마법 데미지 15를 입힌다.",
    "specialEffectText":  "",
    "effectId":  "fire_spear",
    "damage":  15,
    "damageType":  "Magic"
}
```

<a id="card-plasmashield"></a>
### 54. 플라즈마 쉴드 (PlasmaShield)

| 항목 | 현재 값 |
|---|---|
| 이름 / CardId | 플라즈마 쉴드 / `PlasmaShield` |
| 종류 / 소속 / 희귀도 | 마법 ScriptedSpell / 과학문명 / 커먼 |
| 비용 | 전력 1. 부족한 전력은 기존 규칙대로 골드 1로 대체 가능 |
| 효과 | 선택한 유닛(마스터 제외) 또는 건물의 물리 방어력 +10 |
| 사용 대상 / 범위 | 살아 있는 내·상대 유닛(마스터 제외) 또는 건물 / 단일 대상 |
| 발동 / 지속 / 중첩 | 즉시 / 턴·횟수 제한 없는 능력치 버프 / 중첩 가능 |
| 피해 / 주문력 | 피해 없음. 주문력·마법 증폭진·방어에 따른 증가량 변화 없음 |
| 특수효과 / 종족 | 없음 / 마법은 종족 항목 없음 |
| 강화 | 불가능. 부여 물리 방어력 +10 고정 |
| 덱 빌딩 / 랜덤 보상 | On / Off |
| 그래픽 | 대상에게 투명한 푸른 육각형 보호막 형성·빛남·소멸을 한 번 재생. 버프가 남아 있어도 보호막은 계속 표시하지 않음 |
| 카드 그림 | `Assets/Project333/Resources/Project333/CardArtwork/PlasmaShield.png`. 사용자 첫 첨부 그림 전체 비율 유지 |
| VFX / 네이티브 카드 | `Project333/SpellEffects/PlasmaShield/Formation` / `Assets/Project333/ScriptableObjects/StarterTen/Cards/PlasmaShield.asset` |

빈 칸·사망·봉인·마스터·은신한 적은 직접 지정할 수 없다. 은신한 아군은 지정할 수 있고 버프만으로 은신은 해제되지 않는다. 방전 중에도 증가량을 저장하지만 방전의 실효 방어력 0 규칙은 유지되며 해제 뒤 원래 방어력과 합산된다. 망각은 이미 적용된 버프를 지우고, 망각 이후 새 버프는 정상 적용된다. 고독으로 소유권이 이전되어도 증가량과 출처를 유지한다. HP·공격력·마법 방어력·이동·액티브는 바꾸지 않는다. 시전 실패 시 비용·손패·버프를 소비/변경하지 않는다.

강화 불가 카드이므로 시전 1회 부여 물리 방어력은 +10으로 고정한다. 기존 저장 레벨이 있어도 새 시전의 부여량은 증가하지 않는다.

원본 데이터:

```json
{
    "id":  "PlasmaShield",
    "displayName":  "플라즈마 쉴드",
    "definitionType":  "ScriptedSpell",
    "rarity":  "Common",
    "affiliation":  "ScienceCivilization",
    "includeInDraft":  true,
    "includeInRewards":  false,
    "chargeTileFootprint":  "None",
    "cost":  {
                 "power":  1
             },
    "effectText":  "선택한 유닛(마스터 제외) 또는 건물의 물리 방어력을 영구적으로 10 증가시킨다. 중첩 가능.",
    "specialEffectText":  "",
    "effectId":  "plasma_shield",
    "effectAmount":  10,
    "damage":  0,
    "damageType":  "None"
}
```


<a id="card-manashield"></a>
### 55. 마나쉴드 (ManaShield)

| 항목 | 현재 값 |
|---|---|
| 이름 / CardId | 마나쉴드 / `ManaShield` |
| 종류 / 소속 / 희귀도 | 마법 ScriptedSpell / 판타지 / 커먼 |
| 비용 | 마나 1. 부족한 마나는 기존 규칙대로 골드 1로 대체 가능 |
| 효과 | 선택한 유닛(마스터 제외) 또는 건물의 마법 방어력 +10 |
| 사용 대상 / 범위 | 살아 있는 내·상대 유닛(마스터 제외) 또는 건물 / 단일 대상 |
| 발동 / 지속 / 중첩 | 즉시 / 턴·횟수 제한 없는 능력치 버프 / 중첩 가능 |
| 피해 / 주문력 | 피해 없음(`damage: 0`, `damageType: "None"`). 주문력·마법 증폭진·피해 방지는 부여량을 바꾸지 않음 |
| 효과 수치 | `effectAmount: 10`. 피해 필드와 별개로 마법 방어력 부여량을 저장 |
| 특수효과 / 종족 | 없음 / 마법은 종족 항목 없음 |
| 강화 | 불가능. 부여 마법 방어력 +10 고정 |
| 덱 빌딩 / 랜덤 보상 | On / Off |
| 그래픽 | 육각형 무늬 없이 매끄럽고 투명한 푸른 마나 구체를 대상 주변에 한 번 형성·빛남·소멸시킨 뒤 숨김. 약 0.817초, 화면 Image 최대 알파 0.28. 원본 알파와 구체의 가로·세로 비율 유지 |
| 카드 그림 | `Assets/Project333/Resources/Project333/CardArtwork/ManaShield.png` |
| VFX / 네이티브 카드 | `Project333/SpellEffects/ManaShield/Formation` / `Assets/Project333/ScriptableObjects/StarterTen/Cards/ManaShield.asset` |

빈 칸·사망·봉인·마스터·은신한 적은 직접 지정할 수 없다. 은신한 아군은 지정할 수 있고 버프만으로 은신은 해제되지 않는다. 마법 방어력만 증가하며 HP·공격력·물리 방어력·이동·액티브를 바꾸지 않는다. 플라즈마 쉴드의 물리 방어력 증가와 독립적으로 중첩된다. 예를 들어 플라즈마 쉴드 +10과 마나쉴드 +10을 적용하면 각각 물리 방어력 +10, 마법 방어력 +10이다.

기존 `CombatStatBuffState`에 `ManaShield` 출처와 실제 `MagicDefenseBonus`를 저장한다. 방전 중에도 증가량을 저장하지만 방전의 실효 방어력 0 규칙은 유지되며 해제 뒤 원래 마법 방어력과 합산된다. 망각은 이미 적용된 버프를 지우고, 망각 이후 새 버프는 정상 적용된다. 고독으로 소유권이 이전되거나 AI 복사·온라인 스냅샷을 복원해도 증가량과 출처를 유지한다. 기존 사망·부활 정리 규칙을 따른다. 시전 실패 시 비용·손패·버프를 소비/변경하지 않는다.

보호 구체는 성공한 시전의 연출이고 영구 버프가 남아 있어도 계속 표시하지 않는다. 상태 복원은 구체를 다시 재생하지 않는다. 피해·피격 애니메이션·효과음은 발생시키지 않는다. [연출과 자산](mana_shield_presentation.md).

강화 불가 카드이므로 시전 1회 부여 마법 방어력은 +10으로 고정한다. 기존 저장 레벨이 있어도 새 시전의 부여량은 증가하지 않는다.

원본 데이터:

```json
{
  "id": "ManaShield",
  "displayName": "마나쉴드",
  "definitionType": "ScriptedSpell",
  "rarity": "Common",
  "affiliation": "Fantasy",
  "includeInDraft": true,
  "includeInRewards": false,
  "chargeTileFootprint": "None",
  "cost": {
    "mana": 1
  },
  "effectText": "선택한 유닛(마스터 제외) 또는 건물의 마법 방어력을 영구적으로 10 증가시킨다. 중첩 가능.",
  "specialEffectText": "",
  "effectId": "mana_shield",
  "effectAmount": 10,
  "damage": 0,
  "damageType": "None"
}
```

<a id="card-trapreturn"></a>
### 56. 트랩: 리턴 (TrapReturn)

| 항목 | 현재 값 |
|---|---|
| 이름 / CardId | 트랩: 리턴 / `TrapReturn` |
| 종류 / 소속 / 희귀도 | 마법 TrapSpell / 중립 / 레어 |
| 설치 비용 / 발동 비용 | 설치 무료 / 발동 때 전투 골드 4. 계정 Resource Gold와 별개 |
| 효과 / 특수효과 | 일반 효과 문구 없음 / 트랩 |
| 특수효과 문구 | 트랩: 골드-4. 상대방 유닛이 내 마스터 공격 시, 그 유닛을 패로 되돌린다.(마스터 제외) |
| 설치 입력 | 내 Main 행동 턴, 살아 있는 내 마스터로 드래그 또는 카드 선택 후 마스터 클릭. 실제 설치 명령은 효과 대상 좌표 없음 |
| 발동 조건 / 효과 | `EnemyUnitAttacksMaster` / `ReturnAttackerToHand` |
| 대상 | 상대의 적법한 일반 공격자 중 일반 유닛. 마스터·건물 공격자는 제외 |
| 발동 시점 | 환술 최종 대상·공격 비용 처리 후, 쉴더·HP 피해·연타·관통·반격 전 |
| 피해 속성 | `damage: 0`, `damageType: "Magic"` 메타데이터. 실제 피해·주문력·방어력 계산 없음 |
| 지속 / 소모 / 중복 | 조건·비용 충족 때 1회 소모해 버림. 같은 소유자의 동일 CardId는 동시에 하나만 설치 |
| 강화 / Lv.1–13 | 강화 불가 / 증가량 없음. 설치·발동 비용과 반환 효과 불변 |
| 덱 빌딩 / 랜덤 보상 | On / On. 강화 불가 보상 제외 정책의 사용자 확정 예외 |
| 종족 / 차지 타일 | 마법은 종족 항목 없음 / None, 필드 타일 미점유 |
| 설치 중 공개 | 소유자에게만 상세, 상대에게 개수만 공개. 마스터 금색 ?; 2개 이상 Xn |
| 중앙 카드 공개 | 상대 설치는 숨김 그림 + “트랩”, 실제 발동은 양쪽에 진짜 카드. 비차단 FIFO 3초/대기열2초 |
| 그림 / 네이티브 카드 | `Project333/CardArtwork/TrapReturn` / `Assets/Project333/ScriptableObjects/StarterTen/TrapReturn.asset` |
| 숨김 그림 | 사용자 물음표 원본을 유지한 `Project333/CardArtwork/TrapHidden`. 마스터에는 작은 원본 축소 대신 읽기 쉬운 금색 ? 글자 사용 |

발동 골드가 4 미만이어도 무료로 설치할 수 있다. 설치 순서대로 검사하며 조건이 맞지 않거나 발동 비용을 낼 수 없는 트랩은 유지한다. 처음으로 발동 가능한 트랩 하나만 비용을 내고 소모한다. 중복 설치 거절은 손패·자원을 변경하지 않는다.

발동하면 공격자를 현재 소유자의 손패에 같은 CardId의 새 일반 카드로 되돌리고 공격 행동 전체를 취소한다. 새 런타임 ID를 부여하고 전장 HP·버프·상태·임시 복제 할인을 보존하지 않는다. 다음 소환은 현재 카드/소유자의 기본·강화 규칙을 사용한다. 손패가 가득 차면 카드를 추가하지 않고 소실시키지만 트랩 소모·골드 지불·전장 제거·공격 취소는 그대로 처리한다.

반환 자체는 사망/파괴가 아니므로 처치 보상·사망 효과·마왕 부활·죽음 모션을 발생시키지 않는다. 원래 공격의 피해·연타·관통·반격·흡혈·피격 효과음도 발생시키지 않는다. 퇴장에 따른 기존 오라 정리 등은 유지한다. 마나 대포 등 적법한 공격 행동의 비용은 리턴 판정 전에 내며, 공격 검증 실패는 트랩 발동을 만들지 않는다.

상대 설치 이벤트에는 비밀 카드 ID·레벨·런타임 ID·포착한 비용·조건·효과가 없다. 발동 이벤트만 실제 카드와 반환 대상을 공개한다. 재접속은 자신의 트랩 상세와 상대 개수만 복원하고 과거 중앙 연출을 재생하지 않는다. [전투 규칙](battle_rules.md#trap-return).

원본 데이터:

```json
{
  "id": "TrapReturn",
  "displayName": "트랩: 리턴",
  "definitionType": "TrapSpell",
  "rarity": "Rare",
  "affiliation": "Neutral",
  "chargeTileFootprint": "None",
  "includeInDraft": true,
  "includeInRewards": true,
  "cost": { "mana": 0, "qi": 0, "power": 0, "gold": 0 },
  "effectText": "",
  "specialEffectText": "트랩: 골드-4. 상대방 유닛이 내 마스터 공격 시, 그 유닛을 패로 되돌린다.(마스터 제외)",
  "damage": 0,
  "damageType": "Magic",
  "hasTrap": true,
  "trapTrigger": "EnemyUnitAttacksMaster",
  "trapEffect": "ReturnAttackerToHand",
  "trapActivationCost": { "mana": 0, "qi": 0, "power": 0, "gold": 4 }
}
```

<a id="card-trapmirrorfield"></a>

### 57. 트랩-미러 필드 (TrapMirrorField)

| 항목 | 내용 |
|---|---|
| 이름 / CardId | 트랩-미러 필드 / `TrapMirrorField` |
| 종류 / 소속 / 희귀도 | 마법 `TrapSpell` / 판타지 `Fantasy` / 레어 `Rare` |
| 설치 / 발동 비용 | 설치 무료 / 발동 마나 2, 부족한 마나는 골드로 1:1 대체 |
| 덱 빌딩 / 랜덤 보상 | On / Off |
| 강화 | 불가. Lv.1–13 증가량 없음, 공격자 현재 ATK를 사용 |
| 설치 입력 | 살아 있는 내 마스터. 설치는 효과 대상 선택이 아님 |
| 발동 대상 | 상대 유닛·마스터가 내 유닛·마스터를 공격할 때. 건물 제외 |
| 효과 | 공격 전체 취소 후 공격자의 현재 ATK를 기본값으로 본인에게 Magic 피해. 내 현재 주문력 및 대상의 마법 방어·피해 보호 적용 |
| 수명 / 중복 | 조건과 발동 비용 충족 시 한 번 소모해 버림. 기본 만료 없음, 같은 소유자의 동일 CardId 동시 설치 불가 |
| 특수효과 | 트랩. 설치 순서·비용 부족 시 유지·상대 비공개·성공 발동 시 실제 이미지 공개 |
| 그림 | `Assets/Project333/Resources/Project333/CardArtwork/TrapMirrorField.png`. 투명한 1254×1254 거울·반사 광선 일러스트, native FullRect |
| 네이티브 카드 | `Assets/Project333/ScriptableObjects/StarterTen/TrapMirrorField.asset`. 현재 53종 카탈로그에 1회 등록 |
| 판정 상세 | [전투 규칙](battle_rules.md#trapmirrorfield). 반사는 명시된 공격자 본인에게 직접 적용하며 다른 단일 마법 규칙은 유지 |

**원본 카드 데이터: 설정된 모든 필드**

```json
{
    "id":  "TrapMirrorField",
    "displayName":  "트랩-미러 필드",
    "definitionType":  "TrapSpell",
    "rarity":  "Rare",
    "affiliation":  "Fantasy",
    "chargeTileFootprint":  "None",
    "includeInDraft":  true,
    "includeInRewards":  false,
    "cost":  {
                 "mana":  0,
                 "qi":  0,
                 "power":  0,
                 "gold":  0
             },
    "effectText":  "",
    "specialEffectText":  "트랩: 마나-2. 상대방 유닛이 내 유닛을 공격 시, 공격을 취소하고 그 유닛에게 해당 유닛의 공격력만큼 마법 데미지를 입힌다.",
    "damage":  0,
    "damageType":  "Magic",
    "hasTrap":  true,
    "trapTrigger":  "EnemyUnitAttacksUnit",
    "trapEffect":  "ReflectAttackerAttackAsMagic",
    "trapActivationCost":  {
                               "mana":  2,
                               "qi":  0,
                               "power":  0,
                               "gold":  0
                           }
}
```

<a id="card-explosivereactivearmour"></a>

### 58. 트랩 - 반응 장갑 (ExplosiveReactiveArmour)

| 항목 | 내용 |
|---|---|
| 이름 / CardId | 트랩 - 반응 장갑 / `ExplosiveReactiveArmour` |
| 종류 / 소속 / 희귀도 | 마법 `TrapSpell` / 과학문명 `ScienceCivilization` / 레어 `Rare` |
| 설치 / 발동 비용 | 설치 무료 / 발동 전력 4. 전력을 먼저 쓰고 부족분은 골드로 1:1 대체 |
| 덱 빌딩 / 랜덤 보상 | On / Off |
| 강화 | 불가. Lv.1–13 증가량 및 효과 수치 없음 |
| 설치 입력 | 살아 있는 내 마스터. 설치 시 발동 비용이 없어도 가능 |
| 발동 조건 | 상대 일반 근거리 유닛(`AttackType.Melee`)이 내 마스터를 적법하게 근접 공격할 때. 원거리 유닛·마스터·건물 공격자 제외, 환술의 실제 최종 대상 기준. 거리·피해 속성이 아닌 공격 방식으로 판정 |
| 효과 | 피해·쉴더·연타·관통·반격 전에 승인된 공격을 취소하고 공격자를 직접 파괴. 피해 방어·무적·불굴로 막지 않으며 정상 사망·오라 연쇄·마왕 부활·승패 처리 적용 |
| 공격 비용 | 승인된 공격 1회와 이미 낸 명시 공격 비용은 반환하지 않음 |
| 수명 / 중복 | 조건·비용 충족 시 1회 소모, 기본 만료 없음. 비용 부족 시 유지하며 다음 설치 트랩 검사. 같은 소유자의 동일 CardId 동시 설치 불가 |
| 표시 | 설치 시 상대에게 물음표 공통 카드·마스터 ?/Xn만 공개. 성공 발동 시 양쪽 화면 중앙에 실제 카드 이미지 표시. 배리어·폭발과 실제 공격자 사망 연출 표시 |
| 카드 그림 | `Assets/Project333/Resources/Project333/CardArtwork/ExplosiveReactiveArmour.png`. 1254×1254 투명 그림: 금색 팔각형 에너지 배리어에 탄환이 닿으며 배리어 쪽으로 폭발 |
| 네이티브 카드 | `Assets/Project333/ScriptableObjects/StarterTen/ExplosiveReactiveArmour.asset`. 53종 카탈로그에 1회 등록, FullRect·Point 그림 연결 |
| 판정 상세 | [전투 규칙](battle_rules.md#explosivereactivearmour). 별도 피해 패킷·가짜 피해 수치·피격음 없음. 위 전력4·근접 전용 정의는 신규 설치에 적용하며, 수정 전 설치·복원된 트랩은 설치 당시 포착한 조건·비용을 유지 |

**원본 카드 데이터: 설정된 모든 필드**

```json
{
    "id":  "ExplosiveReactiveArmour",
    "displayName":  "트랩 - 반응 장갑",
    "definitionType":  "TrapSpell",
    "rarity":  "Rare",
    "affiliation":  "ScienceCivilization",
    "chargeTileFootprint":  "None",
    "includeInDraft":  true,
    "includeInRewards":  false,
    "cost":  {
                 "mana":  0,
                 "qi":  0,
                 "power":  0,
                 "gold":  0
             },
    "effectText":  "",
    "specialEffectText":  "트랩: 전력-4. 상대방 유닛이 내 마스터를 근접 공격 시, 그 공격을 취소하고 그 유닛을 파괴한다.(마스터 제외)",
    "damage":  0,
    "damageType":  "None",
    "hasTrap":  true,
    "trapTrigger":  "EnemyMeleeUnitAttacksMaster",
    "trapEffect":  "DestroyAttacker",
    "trapActivationCost":  {
                               "mana":  0,
                               "qi":  0,
                               "power":  4,
                               "gold":  0
                           }
}
```

<a id="card-irondome"></a>

### 59. 트랩 - 아이언 돔 (IronDome)

| 항목 | 내용 |
|---|---|
| 이름 / CardId | 트랩 - 아이언 돔 / `IronDome` |
| 종류 / 소속 / 희귀도 | 마법 `TrapSpell` / 과학문명 `ScienceCivilization` / 레어 `Rare` |
| 특수효과 문구 | 트랩: 전력-4. 상대방 원거리 유닛이 내 마스터 공격 시, 그 공격을 취소하고 그 유닛을 파괴한다.(마스터 제외) |
| 설치 / 발동 비용 | 설치 무료 / 발동 전력 4. 전력을 먼저 쓰고 부족분은 전투 골드로 1:1 대체 |
| 덱 빌딩 / 랜덤 보상 | On / Off |
| 강화 | 불가. Lv.1–13 증가량 및 효과 수치 없음 |
| 설치 입력 | 살아 있는 내 마스터. 발동 비용이 없어도 설치 가능 |
| 발동 조건 | 상대 일반 원거리 유닛(`AttackType.Ranged`)의 적법한 일반 공격에서 실제 최종 대상이 내 실제 마스터일 때. 근거리 유닛·마스터·건물 공격자 제외, 환술의 실제 최종 대상 기준. 타일 거리·비행·피해 속성으로 판정하지 않음 |
| 효과 | 피해·쉴더·연타·관통·반격 전에 승인된 공격 전체 취소 및 공격자 직접 파괴. 방어·무적·불굴로 막지 않으며 공통 사망·오라 연쇄·유언·부활·승패 처리 적용 |
| 공격 비용 | 승인된 공격 1회와 이미 낸 명시 공격 비용은 반환하지 않음 |
| 수명 / 중복 | 조건·비용 충족 시 1회 소모, 기본 만료 없음. 비용 부족 시 유지하며 다음 설치 트랩 검사. 같은 소유자의 동일 CardId 동시 설치 불가 |
| 표시 | 설치 시 상대에게 공통 물음표 카드와 마스터 ?/Xn만 공개. 성공 발동 때 실제 카드 공개 후 설치자 마스터에서 공격자까지 0.5초 직선 미사일 발사. 공격자 위치에서 시한폭탄 폭발 16컷을 재사용하며 폭발 최대 확장 시 실제 Death 재생. 부활 대기는 영구 사망으로 표시하지 않음. 추가 음원 없음. [연출 안내](iron_dome.md) |
| 카드 그림 | `Assets/Project333/Resources/Project333/CardArtwork/IronDome.png`. 날아오는 미사일을 요격 미사일이 맞혀 공중에서 폭파하는 장면 |
| 네이티브 카드 | `Assets/Project333/ScriptableObjects/StarterTen/IronDome.asset`. 53종 카탈로그에 1회 등록, FullRect·Point 그림 연결 |
| 타입화된 조건 / 효과 | `EnemyRangedUnitAttacksMaster`(4) / `DestroyAttacker`(3), 실제 피해 수치 없음 (`damage: 0`, `damageType: "None"`) |
| 판정 상세 | [전투 규칙](battle_rules.md#irondome), [카드·그림·등록 안내](iron_dome.md). 설치 당시 조건·비용·효과를 보존하며 상대 정체를 모의 실행에서 읽지 않음 |

**원본 카드 데이터: 설정된 모든 필드**

```json
{
  "id": "IronDome",
  "displayName": "트랩 - 아이언 돔",
  "definitionType": "TrapSpell",
  "rarity": "Rare",
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "None",
  "includeInDraft": true,
  "includeInRewards": false,
  "cost": { "mana": 0, "qi": 0, "power": 0, "gold": 0 },
  "effectText": "",
  "specialEffectText": "트랩: 전력-4. 상대방 원거리 유닛이 내 마스터 공격 시, 그 공격을 취소하고 그 유닛을 파괴한다.(마스터 제외)",
  "damage": 0,
  "damageType": "None",
  "hasTrap": true,
  "trapTrigger": "EnemyRangedUnitAttacksMaster",
  "trapEffect": "DestroyAttacker",
  "trapActivationCost": { "mana": 0, "qi": 0, "power": 4, "gold": 0 }
}
```

<a id="card-summontrap"></a>

### 60. 트랩 - 서먼 트랩 (SummonTrap)

| 항목 | 내용 |
|---|---|
| 이름 / CardId | 트랩 - 서먼 트랩 / `SummonTrap` |
| 종류 / 소속 / 희귀도 | 마법 `TrapSpell` / 중립 `Neutral` / 커먼 `Common` |
| 특수효과 문구 | 트랩: 골드-1. 상대방이 유닛을 소환하면 그 유닛에게 물리 데미지 20을 입힌다. |
| 설치 / 발동 비용 | 설치 무료 / 성공 발동 시 전투 골드 1. 계정 Resource Gold와 별개 |
| 덱 빌딩 / 랜덤 보상 | On / On |
| 강화 | 가능. Lv.1–13 매 레벨 물리 피해 +1; Lv.0 20, Lv.13 33. 설치·발동 비용은 불변 |
| 설치 입력 | 살아 있는 내 마스터. 발동 골드가 부족해도 설치 가능 |
| 발동 조건 | 상대 일반 유닛의 성공한 손패 소환(`PlayUnitCard`). 마스터·건물 제외. 실패한 소환, 이동·고독·패 반환·부활은 조건이 아님 |
| 발동 순서 | 손패·소환 비용·정상 배치·강화·속공·오라·낙진·무적 등 기존 소환 초기화 완료 뒤 판정. 이후 해당 소환물에 직접 피해를 주며 정상 소환을 취소하거나 이미 낸 비용/손패를 복구하지 않음 |
| 효과 | 설치 때 포착한 `20 + 레벨`의 직접 Physical 피해. 물리 방어·무적·봉인 보호·불굴·방전 3배·실제 HP 감소 시 은신 해제·정상 사망/오라 철회/연쇄/마왕 부활 처리 적용 |
| 피해 예외 | 주문력·마법 방어·비행 반감·일반공격 치명타·연타·흡혈·쉴더 이전 없음. 최종 피해 0이어도 비용 지불·1회 소모·실제 카드 공개는 발생 |
| 수명 / 중복 | 기본 만료 없음. 골드가 부족하면 유지하고 다음 설치 트랩을 검사. 한 소환에 조건·비용을 충족하는 최초 트랩 한 장만 발동. 같은 소유자의 동일 CardId 동시 설치 불가 |
| 비공개 / 복원 | 설치 시 상대에게 공통 물음표 카드와 마스터 ?/Xn만 공개. 소유자에게만 CardId·조건·비용·포착 피해·레벨·순서를 복원하며, 실제 발동 때 양쪽에 진짜 카드 공개. 재접속은 과거 연출을 재생하지 않음 |
| 카드 그림 / 네이티브 카드 | `Assets/Project333/Resources/Project333/CardArtwork/SummonTrap.png` / `Assets/Project333/ScriptableObjects/StarterTen/SummonTrap.asset`. 네이티브 카탈로그에 1회 등록 |
| 타입화된 조건 / 효과 | `EnemyUnitSummoned`(5) / `DamageSummonedUnitPhysical`(4), `damage: 20`, `damageType: "Physical"`. 설치 상태 `ArmedTrapState.EffectAmount`에 강화 반영 피해 저장 |
| 상세 규칙 | [공통 트랩/개별 판정](battle_rules.md#summontrap), [특수효과](special_effects.md#summontrap) |

**강화 레벨별 증가량**

| 레벨 | 해당 레벨의 증가 | 누적 물리 피해 |
|---|---|---:|
| Lv.0 | 기본값 | 20 |
| Lv.1 | 피해 +1 | 21 |
| Lv.2 | 피해 +1 | 22 |
| Lv.3 | 피해 +1 | 23 |
| Lv.4 | 피해 +1 | 24 |
| Lv.5 | 피해 +1 | 25 |
| Lv.6 | 피해 +1 | 26 |
| Lv.7 | 피해 +1 | 27 |
| Lv.8 | 피해 +1 | 28 |
| Lv.9 | 피해 +1 | 29 |
| Lv.10 | 피해 +1 | 30 |
| Lv.11 | 피해 +1 | 31 |
| Lv.12 | 피해 +1 | 32 |
| Lv.13 | 피해 +1 | 33 |

**원본 카드 데이터: 설정된 모든 필드**

```json
{
    "id":  "SummonTrap",
    "displayName":  "트랩 - 서먼 트랩",
    "definitionType":  "TrapSpell",
    "rarity":  "Common",
    "affiliation":  "Neutral",
    "chargeTileFootprint":  "None",
    "includeInDraft":  true,
    "includeInRewards":  true,
    "cost":  {
                 "mana":  0,
                 "qi":  0,
                 "power":  0,
                 "gold":  0
             },
    "effectText":  "",
    "specialEffectText":  "트랩: 골드-1. 상대방이 유닛을 소환하면 그 유닛에게 물리 데미지 20을 입힌다.",
    "damage":  20,
    "damageType":  "Physical",
    "hasTrap":  true,
    "trapTrigger":  "EnemyUnitSummoned",
    "trapEffect":  "DamageSummonedUnitPhysical",
    "trapActivationCost":  {
                               "mana":  0,
                               "qi":  0,
                               "power":  0,
                               "gold":  1
                           }
}
```

## 5. 미등록 기획 카드: Card Grader

| 항목 | 확인된 내용 |
|---|---|
| 현재 상태 | 과거 대화 복원 기록에 기획만 존재. 현재 cards.json 및 해당 이름의 카드 구현 없음 |
| 이름 | Card Grader. 한국어 정식 이름 미확정 |
| cardId | 미확정. 이름을 근거로 임의로 CardGrader라고 확정하지 않음 |
| 종류 / 문명 / 희귀도 | 모두 미확정 |
| 비용 | 마나·기·전력·골드 모두 미확정 |
| ATK / HP | 미확정 |
| 물리 / 마법 방어력 | 미확정 |
| 차지 타일 | 미확정 |
| 일반 공격 유형 / 데미지 속성 | 미확정 |
| 이동 / 공격 횟수 / 타격 수 / 소환 턴 공격 | 미확정 |
| 특수효과 / 유지비 / 자원 생산 | 미확정 |
| 확인된 효과 | 소환 시 최대 손패 +1 |
| 발동 시점 | 소환 시로 기획 |
| 지속 시간 / 퇴장 시 원복 여부 / 중첩 | 미확정 |
| 덱 빌딩 등장 | 현재 미등록이므로 등장하지 않음. 향후 On/Off 정책 미확정 |
| 랜덤 보상 등장 | 현재 미등록이므로 등장하지 않음. 향후 On/Off 정책 미확정 |
| 강화 가능 여부 / 성장 규칙 | 미확정 |
| 이미지 / 보드 연출 준비 | 확인된 자료 없음 |
| 근거 | conversation_recovery_before_2026-07-17_0939_ko.md의 손패 시스템 작업 이력 |

이 기획은 원문 대화 녹취가 아니라 복원 문서에서 확인했다. 누락된 과거 대화 전체를 확보했다고 단정하지 않으며, 비어 있는 항목을 임의로 결정하지 않는다. 원자력 발전소·생화학폭탄·마왕·용사 등은 현재 구현되어 있으므로 기획 전용 목록에 중복 기재하지 않는다.

## 6. 확인 및 관리 시 주의

- 이 문서는 읽기용 명세다. 여기만 수정해도 실제 카드가 변경되지는 않는다.
- 실제 수정은 cards.json에서 시작하며, 개별 효과가 코드 상수를 사용하면 해당 Rules/Service도 함께 바꿔야 한다.
- 카드 이미지가 있더라도 제작 완료나 보드 Animator 연결 완료를 뜻하지 않는다. 그래픽에 그려진 문구가 현재 데이터와 다를 수도 있다.
- 카드 이미지 파일명이 `Firebolt.png`여도 원본 cardId는 `firebolt`다. 기존 ID의 대소문자와 철자를 함부로 변경하지 않는다.
- 마나의 샘은 사용자 확정에 따라 Lv.1~13 모두 HP +1이다. 케르베로스는 Lv.13에만 ATK +1, Lv.1~12에는 HP +1이다.
- 천라지망·초소형 발전기의 보상 플래그 기본값은 On이지만 강화 불가이므로 최종 보상 풀은 Off다. 대환단은 강화 가능으로 변경되어 최종 보상 풀 On이다. 트랩: 리턴만 사용자 확정 예외로 강화 불가 상태에서도 보상 On이다.
- 주문력은 마나의 샘(+20)에 적용되어 있다. 특수효과 제공 수치와 전투 중 아군 합계를 구분한다.
- 상세 상호작용의 최종 기준은 전투 규칙과 구현 코드다. 문구 변경과 기능 변경을 동일하게 취급하지 않는다.

## 7. 확인한 원본

- [카드 원본 JSON](../Assets/Project333/Resources/Project333/Data/cards.json)
- [카드 JSON 기본값과 정의 변환](../Assets/Project333/Runtime/Infrastructure/Data/JsonCardDefinitionDatabase.cs)
- [강화 가능 여부와 비용](../Assets/Project333/Runtime/Infrastructure/Data/CardUpgradeRules.cs)
- [유닛·건물 레벨별 능력치](../Assets/Project333/Runtime/Infrastructure/Data/CardLevelStatRules.cs)
- [마법 피해 강화](../Assets/Project333/Runtime/Infrastructure/Data/CardLevelSpellRules.cs)
- [랜덤 보상 필터](../Server/Project333.PvpServer/Runs/RunStartService.cs)
- [턴 시작 처리](../Assets/Project333/Runtime/Application/Services/TurnStartService.cs)
- [턴 종료 처리](../Assets/Project333/Runtime/Application/Services/EndTurnService.cs)
- [마법 처리](../Assets/Project333/Runtime/Application/Services/SpellService.cs)
- [공격과 쉴더·관통 처리](../Assets/Project333/Runtime/Application/Services/AttackService.cs)
- [사망·부활·폭발 처리](../Assets/Project333/Runtime/Application/Services/OccupantDestructionService.cs)
- [로봇 공장](../Assets/Project333/Runtime/Application/Services/RobotFactoryService.cs)
- [전투 규칙](battle_rules.md)
- [전체 카드 요약](card_catalog.md)
- [카드 제작 절차](card_authoring_checklist.md)
- [과거 대화 복원 기록](conversation_recovery_before_2026-07-17_0939_ko.md)
