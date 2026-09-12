# After333 카드 상세 명세

최초 작성 기준일: 2026-09-07 / 강화 규칙·공통 설명 갱신: 2026-09-12

최근 밸런스: 블루 드래곤 ATK/HP 40/50, 레드 드래곤 40/40, 생화학폭탄 기본 고정 피해 25. 두 드래곤의 Lv.13은 각각 44/59와 44/49이며, 생화학폭탄의 Lv.13 피해는 38이다.

이 문서는 카드 원본 데이터, 강화 코드, 개별 전투 효과 및 과거 대화 복원 기록을 대조한 현재 설정의 스냅샷이다. 카드 밸런스나 게임 코드는 변경하지 않았다.

## 1. 범위와 읽는 방법

- 등록 카드 42종: 판타지 19, 무림 10, 과학 문명 11, 중립 2.
- 종류: 유닛 21, 건물 7, 마법 14. 마스터는 덱 카드가 아니므로 42종에서 제외한다.
- 덱 빌딩 On 36종 / Off 6종. 강화 가능 32종 / 불가 10종. 실제 랜덤 보상 대상 27종.
- 미등록 기획 1종: Card Grader. 확인되지 않은 항목은 미확정으로 남긴다.
- 기본 능력치는 Lv.0이다. 현재 HP, 버프·디버프, 부활 누적, 웨어울프 효과, 주문력 등 전투 중 값은 별도로 계산된다.
- On/Off는 등장 설정이지 이미지 완성도나 실기기 최종 검증 여부가 아니다.
- 일반 공격 속성과 카드 효과의 피해 속성은 별도로 설정된다. 현재 레드 드래곤은 일반 공격과 턴 종료 효과가 모두 마법 피해다.
- 아래 원문 JSON은 모든 명시된 카드 필드를 보존한다. JSON 미지정 값과 해당 카드 종류에 적용되지 않는 필드는 구분한다.

## 2. 공통 기본값과 적용 규칙

| 항목 | 현재 기준 |
|---|---|
| 자원 | 마나·기·전력·골드. 네 항목 중 생략된 비용은 0 |
| 골드 대체 | 카드 사용 시 부족한 마나·기·전력을 골드로 1:1 대체. 명시된 골드 비용은 별도로 지불 |
| 전력 유지비 | 소유자 턴 시작 시 지불하며 골드 대체 가능. 전액 지불 불가 시 방전. 망각·봉인 등 세부 예외는 전투 규칙 참조 |
| 일반 공격 | 유닛은 기본 턴당 1회, 공격당 1타격. ATK 0이면 공격·반격 불가. 속공이 없으면 소환한 턴 공격 불가 |
| 건물 | 현재 등록 건물은 모두 이동·공격 불가. JSON 로더의 기본 Melee/CanMove 값이 건물의 이동·공격을 허용하는 것은 아님 |
| 생략 플래그 | hasRobot/hasRush/hasReplicate/hasBerserker/hasEndure/hasShielder/hasLifeSteal/hasHiding/hasFlying/hasPiercing는 false |
| 생략 상태 수치 | 방어력·유지비·주문력·초기 봉인 횟수는 0, 무적은 None |
| 등장 기본값 | includeInDraft / includeInRewards 생략 시 true |
| 실제 보상 풀 | 유효한 카드 정의 + includeInRewards=true + 강화 가능 + Master 제외 |
| 강화 비용 | 다음 레벨 × 3장의 동일 카드와 같은 수치의 계정 골드. 전투 중 골드와 계정 골드는 별개 |
| 일반 강화 | Lv.3/6/9/13 ATK +1, 나머지 HP +1. Lv.13 총 ATK +4 / HP +9 |
| Lv.13만 공격력 강화 | A-212·케르베로스는 Lv.1~12 HP +1, Lv.13 ATK +1. 총 ATK +1 / HP +12 |
| HP 전용 강화 | 마나의 샘·로봇 공장·개방 분타·객잔·상단·발전소·원자력 발전소는 레벨마다 HP +1 |
| 마왕·용사 강화 | Lv.3/6/9 ATK +1, Lv.13 ATK +3, 나머지 HP +1. Lv.13 총 ATK +6 / HP +9 |
| 피해 마법 강화 | 파이어볼·파이어월·시한폭탄·생화학폭탄만 레벨마다 피해 +1. 나머지 마법은 강화 불가 |
| 레벨 범위 예외 | 유닛/건물 능력치 계산은 13 초과를 13으로 제한. 마법 피해 계산은 0 미만 또는 13 초과 레벨의 보너스를 0으로 처리 |
| 효과 문구와 실제 기능 | effectText/specialEffectText는 설명 데이터. 하드코딩된 효과 수치·처리 순서는 전투 코드와 함께 수정해야 함 |
| 유효 상태 | 방전·망각·봉인 및 효과별 상호작용은 battle_rules.md와 개별 효과 코드를 따름 |

## 3. 카드 찾아보기

| 문명 | 카드 | 종류 | 희귀도 | 덱 빌딩 | 강화 |
|---|---|---|---|---|---|
| 판타지 | [블루 드래곤 (BlueDragon)](#card-bluedragon) | 유닛 | 레전더리 | On | 가능 |
| 판타지 | [케르베로스 (Cerberus)](#card-cerberus) | 유닛 | 레어 | On | 가능 |
| 판타지 | [엘프 장궁수 (ElfLongbowScout)](#card-elflongbowscout) | 유닛 | 언커먼 | On | 가능 |
| 판타지 | [파이어볼 (firebolt)](#card-firebolt) | 마법 / 직접 피해 | 커먼 | On | 가능 |
| 판타지 | [파이어월 (Firewall)](#card-firewall) | 마법 / 개별 효과 | 언커먼 | Off | 가능 |
| 판타지 | [고블린 (Goblin)](#card-goblin) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [스켈레톤 (Skeleton)](#card-skeleton) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [좀비 (Zombie)](#card-zombie) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [오크 전사 (OrcWarrior)](#card-orcwarrior) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [웨어울프 (Werewolf)](#card-werewolf) | 유닛 | 레어 | On | 가능 |
| 판타지 | [마왕 (DemonKing)](#card-demonking) | 유닛 | 레전더리 | Off | 가능 |
| 판타지 | [용사 (Hero)](#card-hero) | 유닛 | 유니크 | Off | 가능 |
| 판타지 | [골렘 (Golem)](#card-golem) | 유닛 | 커먼 | On | 가능 |
| 판타지 | [마나의 샘 (ManaPond)](#card-manapond) | 건물 | 레어 | On | 가능 |
| 판타지 | [마나 위버 (ManaWeaver)](#card-manaweaver) | 유닛 | 언커먼 | On | 가능 |
| 판타지 | [마정석 (ManaStone)](#card-manastone) | 마법 / 개별 효과 | 커먼 | On | 불가 |
| 판타지 | [마정석 꾸러미 (ManaStoneBundle)](#card-manastonebundle) | 마법 / 개별 효과 | 언커먼 | On | 불가 |
| 판타지 | [레드 드래곤 (RedDragon)](#card-reddragon) | 유닛 | 레전더리 | On | 가능 |
| 판타지 | [뱀파이어 (Vampire)](#card-vampire) | 유닛 | 언커먼 | On | 가능 |
| 무림 | [천라지망 (CheonraJimang)](#card-cheonrajimang) | 마법 / 개별 효과 | 레어 | On | 불가 |
| 무림 | [대환단 (Daehwandan)](#card-daehwandan) | 마법 / 개별 효과 | 레전더리 | On | 불가 |
| 무림 | [영약: 만년설삼 (TenThousandYearSnowGinseng)](#card-tenthousandyearsnowginseng) | 마법 / 지속 자원 | 유니크 | On | 불가 |
| 무림 | [고독 (Gu)](#card-gu) | 마법 / 개별 효과 | 유니크 | On | 불가 |
| 무림 | [환술 (HuanShu)](#card-huanshu) | 마법 / 개별 효과 | 언커먼 | On | 불가 |
| 무림 | [개방 분타 (GaebangBranch)](#card-gaebangbranch) | 건물 | 커먼 | On | 가능 |
| 무림 | [상단 (MerchantCaravan)](#card-merchantcaravan) | 건물 | 언커먼 | On | 가능 |
| 무림 | [객잔 (Inn)](#card-inn) | 건물 | 커먼 | Off | 가능 |
| 무림 | [광마 (Gwangma)](#card-gwangma) | 유닛 | 유니크 | On | 가능 |
| 무림 | [소림 1대 제자 (Shaolin_1st_Disciple)](#card-shaolin_1st_disciple) | 유닛 | 커먼 | On | 가능 |
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
| 중립 | [금광 채굴꾼 (GoldMiner)](#card-goldminer) | 유닛 | 커먼 | On | 가능 |
| 중립 | [방패병 (Shieldbearer)](#card-shieldbearer) | 유닛 | 커먼 | On | 가능 |

## 4. 등록 카드 상세

<a id="card-a-212"></a>
### A-212 (신규)

| 항목 | 값 |
|---|---|
| 이름 / CardId | A-212 / A-212 |
| 종류 / 문명 / 희귀도 | 유닛 / 과학 문명 / 레어 |
| 비용 | 전력 4 (마나 0, 기 0, 골드 0); 부족한 전력은 골드로 대체 |
| 차지 타일 / 이동 | 1×1 / 가능 |
| 공격 유형 / 데미지 속성 | 근거리 / 물리 |
| 기본 ATK / HP | 6 / 22 |
| 물리 / 마법 방어력 | 0 / 0 |
| 특수효과 | 로봇, 4연타, 전력 -2 |
| 일반 효과 | 없음 |
| 덱 빌딩 / 랜덤 보상 | On / On |
| 강화 | 최대 Lv.13. Lv.1~12마다 HP +1, Lv.13에는 ATK +1 |
| Lv.12 / Lv.13 ATK·HP | 6/34 / 7/34 |
| 소환 턴 공격 | 불가 (속공 없음) |
| 카드 이미지 | Assets/Project333/Resources/Project333/CardArtwork/A-212.png |
| 애니메이션 | Assets/Unit/A-212: Idle, Run, Attack, BeAttacked, Death. 방전은 Idle 대체 |

4연타는 공격 한 번에 4번 타격하는 효과이며, 매 타격마다 방어력을 적용한다. 반격에는 연타를 적용하지 않는다.
로봇 공장의 생성 후보와 로봇 합체 대상으로 포함된다. 전력 유지비 2는 기존 유지비 순서와 골드 대체 규칙을 따른다.
유지비를 낼 수 없으면 방전된다. 기존 봉인·망각·방전의 효과 억제 규칙을 그대로 따른다.
이번 추가로 기존 AI 고정 덱 10개를 자동 수정하지는 않는다.

**원본 카드 데이터**

```json
{
  "id": "A-212",
  "displayName": "A-212",
  "definitionType": "Unit",
  "rarity": "Rare",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 4
  },
  "specialEffectText": "로봇\n4연타\n전력 -2",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 6,
  "health": 22,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "isScience": true,
  "sciencePowerUpkeep": 2,
  "hasRobot": true,
  "hitsPerAttack": 4
}
```

<a id="card-bluedragon"></a>

### 01. 블루 드래곤 (BlueDragon)

| 항목 | 내용 |
|---|---|
| 이름 | 블루 드래곤 |
| cardId | `BlueDragon` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레전더리 (Legendary) |
| 사용 비용 | 마나 8 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 40 / 50 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 마법 (Magic) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 있음 / 관통: 없음 |
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
  "rarity": "Legendary",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 8
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
| 일반 공격 1회당 타격 수 | 3회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 효과 요약 | 한 번 공격할 때 3회 타격. 반격은 1회다. |
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 있음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 효과 요약 | 자기편 또는 상대편의 선택한 5x1 한 줄에 매 턴 시작마다 마법 피해 33. 총 2회 발동한다. 사용 시점의 주문력을 고정한다. |
| 개별 효과 ID | `firewall` |
| 피해 마법의 기본 피해 / 속성 | 33 / 마법 (Magic) |
| triggerCount 필드 | 2 (의미는 아래 효과 설명 참조) |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

한쪽 보드의 전열 또는 후열 5×1을 선택한다. 사용 후 다음 전역 턴 시작부터 총 2회 발동한다. 사용 시점의 강화 피해와 주문력을 고정하며 여러 장은 독립적으로 중첩한다.

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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| cardId | `OrcWarrior` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 25 / 40 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 29 / HP 49 (전투 중 추가 효과 미반영) |
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
  "attack": 25,
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
| cardId | `Werewolf` |
| 현재 상태 | 카드 이미지 연결 및 덱 빌딩 공개. 실제 기기 연출 검수는 별도 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레어 (Rare) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 20 / 30 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 있음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 24 / HP 39 (전투 중 추가 효과 미반영) |
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
  "attack": 20,
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| cardId | `Hero` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 유니크 (Unique) |
| 사용 비용 | 마나 3 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 3 / 3 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 고정 (Fixed) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | GlobalTurnEnds / 횟수 3 |
| 덱 빌딩 등장 | Off |
| 랜덤 보상 설정값 | Off |
| 실제 랜덤 보상 대상 | 제외 (includeInRewards=false) |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9 ATK +1, Lv.13 ATK +3, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 9 / HP 12 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 소환 시 3번의 턴 종료 동안 무적을 얻는다. 매 턴 종료 시 ATK +13 또는 HP +13 중 하나를 무작위로 얻는다. |
| 특수효과 문구 원문 | 무적, 용사의 성장 |
| 효과 요약 | 소환 시 다음 3번의 전역 턴 종료 동안 무적. 매 턴 종료 시 ATK +13 또는 HP +13 중 하나를 서버가 같은 확률로 영구 부여한다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

invincibleDuration=GlobalTurnEnds이고 invincibleOwnerTurns=3이므로 내 턴과 상대 턴 종료를 모두 센다. 소환한 턴 종료도 첫 번째 감소에 포함한다. 성장의 HP +13은 최대 HP와 현재 HP를 함께 증가시킨다. ATK/HP 선택은 각각 50%다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "Hero",
  "displayName": "용사",
  "definitionType": "Unit",
  "rarity": "Unique",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 3
  },
  "effectText": "소환 시 3번의 턴 종료 동안 무적을 얻는다. 매 턴 종료 시 ATK +13 또는 HP +13 중 하나를 무작위로 얻는다.",
  "specialEffectText": "무적, 용사의 성장",
  "attackType": "Melee",
  "damageType": "Fixed",
  "attack": 3,
  "health": 3,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "invincibleDuration": "GlobalTurnEnds",
  "invincibleOwnerTurns": 3
}
```

<a id="card-golem"></a>

### 13. 골렘 (Golem)

| 항목 | 내용 |
|---|---|
| 이름 | 골렘 |
| cardId | `Golem` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 커먼 (Common) |
| 사용 비용 | 마나 2 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 20 / 30 |
| 기본 물리 / 마법 방어력 | 1 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
  "physicalDefense": 1,
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
| 사용 비용 | 마나 4 / 기 0 / 전력 0 / 골드 0 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 3 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.1~13 모두 HP +1 |
| Lv.13 결과 | ATK 0 / HP 33 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 턴 시작 시 마나 +3 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 소유자 턴 시작에 마나 +3. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/ManaPond.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

2026-09-12 사용자 확정: 모든 강화 레벨에서 HP만 +1이며 공격력은 증가하지 않는다. 공격 불가 설정과 마나 생산량은 유지한다.

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
    "mana": 4
  },
  "effectText": "턴 시작 시 마나 +3",
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
| cardId | `ManaWeaver` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 언커먼 (Uncommon) |
| 사용 비용 | 마나 2 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 5 / 10 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 원거리 (Ranged) |
| 일반 공격 데미지 속성 | 마법 (Magic) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 1 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 9 / HP 19 (전투 중 추가 효과 미반영) |
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
  "rarity": "Uncommon",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 2
  },
  "effectText": "턴 시작 시 마나 +1",
  "attackType": "Ranged",
  "damageType": "Magic",
  "attack": 5,
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| cardId | `RedDragon` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 판타지 (Fantasy) |
| 희귀도 | 레전더리 (Legendary) |
| 사용 비용 | 마나 9 / 기 0 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 40 / 40 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 마법 (Magic) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 있음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 44 / HP 49 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 종료 시 상대방 타일 전체에 33 마법 피해 |
| 특수효과 문구 원문 | 비행 |
| 효과 요약 | 비행. 내 턴 종료 시 상대 전장의 봉인되지 않은 모든 소환물과 마스터에게 마법 피해 33. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/RedDragon.png` (파일 존재만 확인; 완성·연출 검수 판정 아님) |

**판정 및 구분할 사항**

일반 공격과 턴 종료 효과는 모두 마법 피해다. 효과 피해 33은 EndTurnService의 상수로 처리하며, 유닛 효과라서 주문력 증가 대상이 아니다. 카드 강화로 이 효과 피해가 증가하지 않는다.

**원본 카드 데이터: 설정된 모든 필드**

아래 JSON은 작성 시점의 해당 카드 레코드 전체다. 생략 필드는 공통 기본값을 따르며, 문구만 고쳐도 전투 코드의 상수가 자동 변경되는 것은 아니다.

```json
{
  "id": "RedDragon",
  "displayName": "레드 드래곤",
  "definitionType": "Unit",
  "rarity": "Legendary",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "mana": 9
  },
  "effectText": "내 턴 종료 시 상대방 타일 전체에 33 마법 피해",
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 있음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 제외 (강화 불가) |
| 강화 가능 여부 | 불가 |
| 레벨별 강화 규칙 | 강화 불가 |
| Lv.13 결과 | 해당 없음 |
| 효과 문구 원문 | 사용 시 내 마스터의 공격력 +30, 다음 3번의 자기 턴 시작마다 기 +3 |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 사용 즉시 내 마스터 ATK +30. 다음 3번의 내 턴 시작마다 기 +3. |
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
  "effectText": "사용 시 내 마스터의 공격력 +30, 다음 3번의 자기 턴 시작마다 기 +3",
  "damageType": "None",
  "effectId": "daehwandan"
}
```

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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 3 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 3; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 1; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| cardId | `Gwangma` |
| 현재 상태 | 카드 DB 등록 및 기능 구현. 이미지·출시 준비 완료 여부와는 별개 |
| 종류 / 내부 정의 타입 | 유닛 / `Unit` |
| 문명 | 무림 (Murim) |
| 희귀도 | 유니크 (Unique) |
| 사용 비용 | 마나 0 / 기 8 / 전력 0 / 골드 0 |
| 차지 타일 | 1×1 / OneByOne |
| 기본 ATK / HP | 33 / 66 |
| 기본 물리 / 마법 방어력 | 0 / 0 |
| 일반 공격 유형 | 근거리 (Melee) |
| 일반 공격 데미지 속성 | 물리 (Physical) |
| 일반 공격 가능 여부 | 가능. 턴·상태 등 일반 조건을 충족해야 함 |
| 이동 가능 여부 | 가능 |
| 기본 턴당 공격 횟수 | 1회 (공격 가능한 상태 기준) |
| 일반 공격 1회당 타격 수 | 2회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 있음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On (includeInDraft 생략: 기본 true) |
| 랜덤 보상 설정값 | On (includeInRewards 생략: 기본 true) |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | Lv.3/6/9/13 ATK +1, 나머지 레벨 HP +1 |
| Lv.13 결과 | ATK 37 / HP 75 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 없음 / 빈 문자열 |
| 특수효과 문구 원문 | Berserker, Double Attack |
| 효과 요약 | 버서커: 잃은 HP 1당 ATK +1. 한 번 공격할 때 2회 타격하고 반격은 1회다. |
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
  "rarity": "Unique",
  "affiliation": "Murim",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "qi": 8
  },
  "specialEffectText": "Berserker, Double Attack",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 33,
  "health": 66,
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 있음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 가능: 속공/소환 턴 공격 허용 |
| 전력 유지비 | 1 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 있음 / 속공: 있음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 특수효과 문구 원문 | 전력 -1<br>로봇<br>속공 |
| 효과 요약 | 전력 유지비 1, 로봇, 속공 |
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
  "rarity": "Common",
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 1
  },
  "specialEffectText": "전력 -1\n로봇\n속공",
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 13,
  "health": 13,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "isScience": true,
  "sciencePowerUpkeep": 1,
  "hasRobot": true,
  "hasRush": true
}
```

<a id="card-a-301"></a>

### 31. A-301 (A-301)

| 항목 | 내용 |
|---|---|
| 이름 | A-301 |
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 1 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 있음 / 속공: 없음 / 복제: 있음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 있음 / 관통: 없음 |
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
| 특수효과 문구 원문 | 비행<br>로봇<br>전력 -1<br>복제 |
| 효과 요약 | 비행, 로봇, 전력 유지비 1, 복제 |
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
  "rarity": "Common",
  "includeInDraft": true,
  "includeInRewards": true,
  "affiliation": "ScienceCivilization",
  "chargeTileFootprint": "OneByOne",
  "cost": {
    "power": 2
  },
  "specialEffectText": "비행\n로봇\n전력 -1\n복제",
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
  "hasRobot": true,
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 특수효과 문구 원문 | 선택한 로봇 중 전력 유지비가 가장 높은 로봇이 남고, 나머지 로봇의 현재 ATK/HP를 합산해 강화 |
| 효과 요약 | 살아 있는 아군 로봇 유닛을 2기 이상 순서대로 선택한다. 전력 유지비가 가장 높은 로봇이 남고, 동률이면 먼저 선택한 로봇이 남는다. 나머지 로봇의 현재 ATK/HP를 합산해 생존 로봇에게 부여하고 재료 로봇은 처치가 아닌 합체 제거로 없앤다. |
| 개별 효과 ID | `robot_fusion` |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

합체 재료는 내 살아 있는 로봇 유닛 최소 2기이며, 같은 cardId도 허용한다. 전력 유지비가 가장 높은 로봇이 생존하고 동률이면 먼저 선택한 로봇이 생존한다. 재료의 현재 ATK/HP만 합산하며 효과·공격 횟수·데미지 유형·유지비·상태는 이전하지 않는다. 봉인된 로봇은 재료에서 제외한다.

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
  "specialEffectText": "선택한 로봇 중 전력 유지비가 가장 높은 로봇이 남고, 나머지 로봇의 현재 ATK/HP를 합산해 강화",
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
| 전력 유지비 | 1 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 특수효과 문구 원문 | 전력 -1 |
| 효과 요약 | 전력 유지비 1. 소유자 턴 시작에 `로봇 + 드래프트 On` 유닛 중 하나를 같은 확률로 손패에 생성한다. 현재 후보는 A-111, A-212, A-301이다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | 해당 cardId와 이름이 일치하는 카드 이미지 파일 없음 (보드 스프라이트 대체 여부와 별개) |

**판정 및 구분할 사항**

공장마다 내 턴 시작 유지비 처리 후 1회 생성한다. 후보는 hasRobot=true, includeInDraft=true인 유닛이며 현재 A-111만 해당한다. 후보별 동일 확률, 공장 처리 순서는 (0,0),(0,1),(1,0)…(4,1)이다. 손패 초과 생성 카드는 소멸하고 덱 고갈 피해는 발생하지 않는다. 상대에게 생성 카드의 정체는 숨긴다. 공장 자체에는 로봇 태그가 없다.

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
  "specialEffectText": "전력 -1",
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
| 초기 주문력 | 0 |
| 소환 직후 봉인 횟수 | 0 (마왕 사망 후 봉인은 별도 처리) |
| 초기 무적 설정 | None / 횟수 0 |
| 덱 빌딩 등장 | On |
| 랜덤 보상 설정값 | On |
| 실제 랜덤 보상 대상 | 포함 |
| 강화 가능 여부 | 가능 / 최대 Lv.13 |
| 레벨별 강화 규칙 | 매 레벨 HP +1 |
| Lv.13 결과 | ATK 0 / HP 43 (전투 중 추가 효과 미반영) |
| 효과 문구 원문 | 내 턴 시작 시 골드 1을 지불하고 전력 2를 얻는다. |
| 특수효과 문구 원문 | 없음 / 빈 문자열 |
| 효과 요약 | 소유자 턴 시작 자원 단계에 골드 1을 지불할 수 있으면 전력 2를 얻는다. |
| 개별 효과 ID | 없음 (cardId 기반 코드 처리 여부는 아래 설명 참조) |
| 피해 마법의 기본 피해 / 속성 | 해당 없음. 유닛·건물 효과의 피해는 효과 설명 참조 |
| triggerCount 필드 | 미지정 / 해당 없음 |
| ownerTurnStartsRemaining 필드 | 미지정 / 해당 없음 |
| 지속 종료 문구 | 미지정. 개별 효과는 효과 설명의 종료 조건 참조 |
| 카드 이미지 파일 | `Assets/Project333/Resources/Project333/CardArtwork/PowerPlant.png` |

**판정 및 구분할 사항**

골드 1을 전부 지불할 수 있을 때만 전력 2를 얻는다. 이 지불은 sciencePowerUpkeep가 아니라 별도 건물 효과이므로 전력 유지비와 구분한다.

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
  "effectText": "내 턴 시작 시 골드 1을 지불하고 전력 2를 얻는다.",
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | true |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 3 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 1 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 1; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 없음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
| 일반 공격 1회당 타격 수 | 1회 (반격의 타격 수와 구분) |
| 소환한 턴 공격 | 불가: 기본 소환 후 공격 제한 |
| 전력 유지비 | 0 (0이면 없음) |
| isScience 플래그 | false (생략 기본값 포함; 문명 이름과 구분) |
| 기본 내 턴 시작 자원 생산 필드 | 마나 0 / 기 0 / 전력 0 / 골드 0; 개별 스크립트 효과는 아래 설명 참조 |
| 초기 특수효과 플래그 | 로봇: 없음 / 속공: 없음 / 복제: 없음 / 버서커: 없음 / 불굴: 없음 / 쉴더: 있음 / 흡혈: 없음 / 은신: 없음 / 비행: 없음 / 관통: 없음 |
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
- 천라지망·대환단·초소형 발전기의 보상 플래그 기본값은 On이지만 강화 불가이므로 최종 보상 풀은 Off다.
- 아직 적용 카드가 없는 은신·주문력 등도 시스템은 존재한다. 특수효과 시스템과 그 효과를 가진 카드의 존재는 구분한다.
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
