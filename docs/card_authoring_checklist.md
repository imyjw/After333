# Card Authoring Checklist

이 문서는 새 카드 1장을 추가하거나 기존 카드를 수정할 때 따르는 표준 작업 순서다.

현재 구현된 전체 카드와 아직 기획에만 남아 있는 카드는 `docs/card_catalog.md`에서 확인한다. 2026-10-03 현재 카드 데이터·네이티브 자산·강화·보상 코드를 대조한 기준으로 JSON/domain 정의 60종, 드래프트 On 54종, Unity 네이티브 카드 53종이다. 강화 가능 44종, includeInRewards 적용값 On 43종과 서버 실제 보상 41종은 별도로 집계한다.

핵심 원칙:

- 전투와 서버가 읽는 카드 데이터의 기준은 `Assets/Project333/Resources/Project333/Data/cards.json`이다.
- 카드 효과 문장만 추가하면 구현된 것이 아니다. 실제 전투 로직, UI, 서버 동기화, 테스트까지 연결되어야 한다.
- 새 효과가 기존 규칙에 없으면 먼저 `docs/battle_rules.md`에 규칙을 고정한 뒤 구현한다.
- 특수효과를 추가할 때는 `OccupantSpecialEffectTooltipCatalog`의 길게 누르기 툴팁과 해당 툴팁 테스트도 반드시 함께 추가한다.
- 카드 추가 후에는 반드시 `Tools > Project333 > Data > Validate cards.json`를 실행한다.

## 1. 카드 규칙 먼저 확정

새 카드의 효과가 이미 구현된 효과인지 먼저 확인한다. 새 유닛은 종족값(없음·복수 포함)을 사용자에게 확인하고 지정값을 기록한다. 강화 가능 여부와 Lv.1–13 증가량 확인도 기존대로 수행한다.

현재 바로 재사용 가능한 대표 효과:

- 단일 대상 피해 마법: `definitionType: "DamageSpell"`, `damage`, `damageType`
- 턴 시작 자원 생산: `turnStartResourceGain`
- 전력 소모: `sciencePowerUpkeep`, `specialEffectText: "전력 소모:n"`
- 버서커: `hasBerserker: true`, `specialEffectText`에 `Berserker`
- 엔듀어: `hasEndure: true`, `specialEffectText`에 `Endure`
- 쉴더: `hasShielder: true`, `specialEffectText`에 `쉴더` 또는 `Shielder` (`hasGuard`는 구버전 JSON 읽기 전용 호환 필드)
- 흡혈: `hasLifeSteal: true`, `specialEffectText`에 `LifeSteal`
- 속공: `hasRush: true`, `specialEffectText`에 `속공` 또는 `Rush`; 손패에서 소환한 턴의 공격 제한만 해제
- 복제: `hasReplicate: true`, `specialEffectText`에 `복제` 또는 `Replicate`; 성공적으로 사용하면 그 턴에만 유지되는 원본 비용의 사본 생성
- 로봇 합체: `definitionType: "ScriptedSpell"`, `effectId: "robot_fusion"`; 같은 CardId도 각각 한 기로 세며 살아 있는 아군 로봇 유닛 2기 이상 필요
- 로봇 공장: `Building`, `sciencePowerUpkeep: 1`; `races에 Robot 포함 && includeInDraft`인 유닛 중 하나를 서버가 동일 확률로 생성
- 액티브: `specialEffectText`를 `액티브: 효과 내용` 형식으로 작성한다. 기본 사용 시점은 자신의 턴이며, 상대 턴 발동은 카드 설명에 명시한 경우에만 허용한다. 예외 카드에는 별도 조건과 검증을 구현한다. 문구만으로 동작하지 않으므로 ActiveEffectService에 해당 CardId의 조건·비용·처리를 구현하고 명령·UI·서버·AI·재접속을 검증한다. [상세 규칙](active_effects.md)
- 발전소: `Building`, CardId `PowerPlant`; 소환한 턴부터 내 행동 턴에 타일의 액티브 버튼으로 발전소마다 1회 골드 1을 소모해 전력 2 획득. 턴 시작 자동 교환 없음.
- 원자력 발전소: `Building`, CardId `NuclearPowerPlant`; 턴 시작 전력 +3은 `turnStartResourceGain`, 파괴 시 양쪽 전장 물리 피해 30은 `OccupantDestructionService`에서 처리
- 보조배터리: `definitionType: "ScriptedSpell"`, `effectId: "power_bank"`; 골드 3 지불 후 즉시 전력 +6
- 마정석: `definitionType: "ScriptedSpell"`, `effectId: "mana_stone"`; 골드 2 지불 후 즉시 마나 +3
- 마정석 꾸러미: `definitionType: "ScriptedSpell"`, `effectId: "mana_stone_bundle"`; 골드 5 지불 후 즉시 마나 +9
- 고독: `definitionType: "ScriptedSpell"`, `effectId: "gu"`; 기 10을 지불하고 합법적인 상대 유닛을 첫 번째 빈 아군 타일로 영구 이전
- 환술: `definitionType: "ScriptedSpell"`, `effectId: "huan_shu"`; 기 3을 지불하고 상대 유닛의 일반 공격 대상을 영구적으로 서버가 무작위 변경
- 상단: `Building`, CardId `MerchantCaravan`; 소유자의 턴 시작 자원 획득 단계에 골드 +3
- 객잔: `Building`, CardId `Inn`; 소유자의 턴 시작 자원 획득 단계에 골드 +1
- 웨어울프: `Unit`, CardId `Werewolf`; 현재 같은 아군 필드의 다른 살아 있는 웨어울프마다 ATK +10을 실시간 계산하며, 효과를 받는 자신은 방전·망각·봉인 상태가 아니어야 한다. 셀 때는 방전·망각 웨어울프를 포함하고 봉인·사망 웨어울프를 제외한다.
- 마나 대포: `Unit`, CardId `ManaCannon`, 사용자 확정 판타지 언커먼·종족 없음(`races: []`), 소환 마나 2, 기본 ATK/HP 40/10, 원거리 Magic, 방어 0/0, 이동 가능. `effectText: "공격할 때마다 마나 1 소모. 부족한 마나는 골드로 대신 지불할 수 있습니다."`, `effectId`·고유 특수효과 없음. 유효한 일반 공격 행동마다 현재 소유자 마나 1 우선 지불, 부족하면 골드 1로 대체; 모든 검증을 마친 뒤 한 번만 차감하며 실패 시 미차감. 망각 중 비용 효과 억제, 고독 후 새 소유자 자원 사용. 강화 Lv.3/6/9/13 ATK +1·나머지 HP +1, Lv.13 기본 44/19; 드래프트·보상 On. 원본 카드 PNG와 기존 파일을 보존하고 native 등록·실행 검증을 실제 결과와 구분한다.
- 개방 방주: `Unit`, CardId `GaebangChief`, 사용자 확정 무림 유니크·인간, 기 7, 기본 ATK/HP 10/60, 근거리 Physical, 방어 0/0, 이동 가능. `effectText: "현재 소유한 골드 1당 공격력 +10. 골드가 변하면 즉시 반영되며 골드를 소모하지 않습니다."`, `effectId`·고유 특수효과 플래그 없음. 현재 소유자의 전투 골드 ×10을 저장 BaseAttack과 분리해 동적 계산하며 사망·방전·망각·봉인 중 보너스 0, 고독 이후 새 소유자 골드 사용. 비용을 골드로 대신 내면 차감 후 잔액을 사용. 강화 Lv.3/6/9/13 ATK +1·나머지 HP +1, Lv.13 기본 14/69; 드래프트·보상 On.
- 용사: `Unit`, CardId `Hero`; 기본 33/33·속공. 일반공격으로 상대 소환물을 파괴할 때마다 ATK +33 또는 최대/현재 HP +33을 각각 50% 확률로 부여한다. 반격·마스터·아군·파괴 효과 연쇄 사망 제외. 기존 무적·턴 종료 성장 제거.
- 엘프 장로: `Unit`, CardId는 사용자 변경(2026-10-09)에 따라 `ElfElder`로 통일한다. 판타지 레어·엘프, 마나5, ATK/HP30/25, 원거리 Physical, 방어0/0, 이동 가능, 카드 효과 없음. `hasPiercing: true`·`hasRush: true`, `specialEffectText: "관통\n속공"`. 강화 가능: Lv.3/6/9/13 ATK+1, 나머지 HP+1; Lv.13 34/34. 드래프트·보상 On. 사용자 지정 기존 ElfLongbowScout 카드 이미지·Idle_0·controller를 재사용하며 이후 전용 ElfElder.png를 우선 사용한다.
- 스켈레톤아처: `Unit`, CardId `SkeletonArcher`, 사용자 확정 판타지 커먼·언데드, 마나 1, ATK/HP 10/3, 원거리 Physical, 방어 0/0, 이동 가능. 카드 효과·고유 특수효과 없음. 강화 가능: Lv.3/6/9/13 ATK +1, 나머지 Lv.1-13 HP +1; Lv.13 14/12. 드래프트·보상 On. 전용 PNG 우선, 없으면 기존 Skeleton.png alias; 기존 Skeleton.controller/Idle_0 공유.
- 오크 족장: `Unit`, CardId `OrcChieftain`, 사용자 확정 판타지 레어·오크, 마나 7, 기본 ATK/HP 50/70, 근거리 Physical, 방어 0/0, 이동 가능, 고유 특수효과 플래그 없음. 같은 내 전장의 현재·이후 오크 유닛과 자기 자신에게 족장별 ATK/최대·현재 HP +10, 다중 족장 중첩. 해당 족장 퇴장 때 같은 수치를 제거하며 HP≤0 연쇄 사망 가능. 강화 가능: Lv.3/6/9/13 ATK +1, 나머지 HP +1; Lv.13 기본54/79·자기오라64/89. 현재 보드 Animator는 전용 `Assets/Unit/OrcChieftian/OrcChieftian.controller`이며 실제 경로 철자를 보존한다.
- 오크 광전사: `Unit`, CardId `OrcBerserker`, 사용자 확정 판타지 언커먼·오크, 마나 5, ATK/HP 20/50, 근거리 Physical, 방어 0/0, 이동 가능. 카드 효과 없음, `hasBerserker: true` 및 `specialEffectText: "Berserker"`. 강화 가능: Lv.3/6/9/13 ATK +1, 나머지 Lv.1-13 HP +1; Lv.13 24/59. 드래프트·보상 On. 사용자 OrcBerserker.png 보존, 기존 OrcWarrior.controller 공유.
- 플라즈마 쉴드: `PlasmaShield`, 과학문명 커먼 ScriptedSpell, 전력1, `effectId: "plasma_shield"`, `effectAmount: 10`, `damage: 0`, `damageType: "None"`. 내·적 유닛(마스터 제외) 또는 건물 하나의 물리 방어력을 영구 증가, 출처별 중첩. 강화 불가·부여량 +10 고정. 드래프트 On·보상 Off. 보호막은 시전 때 한 번만 재생. EffectAmount는 피해 필드와 구분하며 누락 시0, 새 카드에 사용하려면 해당 Rules/검증/표시/시전 처리를 모두 연결한다. [상세](plasma_shield_presentation.md)
- 마나쉴드: `ManaShield`, 판타지 커먼 ScriptedSpell, 마나 1(부족하면 골드 1 대체), `effectId: "mana_shield"`, `effectAmount: 10`, `damage: 0`, `damageType: "None"`. 살아 있는 내·적 유닛(마스터 제외) 또는 건물 하나의 마법 방어력을 영구 증가하며 중첩 가능. 물리 방어력은 바꾸지 않고 플라즈마 쉴드와 독립적으로 합산한다. 강화 불가·부여량 +10 고정, 주문력·마법 증폭진 보너스 없음. 드래프트 On·보상 Off. 기존 대상·망각·방전·고독·온라인 복원 규칙을 유지한다. 매끄럽고 투명한 마나 구체는 약 0.817초 동안 한 번만 재생 후 숨기며 피해·피격·효과음이 없다. [상세](mana_shield_presentation.md)

- 파이어스피어: FireSpear, 판타지 커먼 ScriptedSpell, 마나3, effectId fire_spear, Magic15. 살아 있는 아군·적 유닛/건물/마스터를 지정하여 같은 전장의 열 앞·뒤2칸에 즉시 광역 피해. 빈 칸·봉인·적 은신 직접 지정 불가, collateral은 광역 규칙. 매 Lv.1–13 피해+1, Lv.13 28+현재 주문력. 드래프트·보상 On. [규칙·편집 경로](fire_spear_presentation.md)
- 메테오: Meteor, 사용자 확정 판타지 유니크 ScriptedSpell, 마나 7, effectId meteor, damage 40/Magic, 대상 지정 없음. 즉시 상대 전장의 현재 유닛·마스터·건물에 각 1회 마법 피해. Lv.1-13 매 레벨 기본 피해 +1(Lv.13: 53), 현재 아군 주문력을 대상마다 한 번 더한 뒤 마법 방어·보호 적용; 건물 배수 없음. 모든 직접 피해 후 사망·부활·연쇄 처리. 드래프트·보상 포함. 사용자 CardArtwork/Meteor.png 보존, 네이티브 API로 카드/카탈로그와 새 VFX/음원을 등록한다. [규칙·편집 경로](meteor_presentation.md)
- 지진: Earthquake, 중립 언커먼 ScriptedSpell, 골드 3, effectId earthquake, damage 10/Physical, 대상 지정 없음. 즉시 상대 전장 전체(유닛·마스터·건물)에 물리 피해; 건물은 강화된 기본 피해를 3배로 만든 뒤 물리 방어 적용. Lv.1-13 매 레벨 피해 +1, Lv.13 유닛/마스터 23·건물 69(방어 전). 드래프트·보상 포함. 사용자 CardArtwork/Earthquake.png와 `.meta`를 보존하고 native API로 카드/카탈로그를 등록한다. [규칙·편집 경로](earthquake_presentation.md)
- 뉴클리어: Nuclear, 과학문명 레전더리 마법, 전력 10. 즉시 상대 전장 전체(마스터·유닛·건물)에 고정 피해 100, 강화 가능·매 레벨 즉시 피해 +1(Lv.13: 113). 사용 당시 상대 전장에 있는 유닛(마스터 포함)에 영구 낙진 +1중첩, 건물은 낙진 중첩·피해 제외. 양쪽 턴 시작마다 중첩당 고정 피해 10(강화로 변하지 않음). 사용자 이미지와 카드 자산 등록, 드래프트/보상 포함. [세부 규칙](nuclear_fallout.md)
- 은신: `hasHiding: true`; 은신에서 시작한 일반 공격의 전체 연타·관통은 최종 피해 2배 치명타. 피해 종류와 관계없이 실제 HP가 줄면 은신 해제, 피해가 0이면 유지. 반격 치명타 없음. 확률형 치명타 카드는 아직 없으며 문구만으로 발동하지 않는다.
- 관통: `hasPiercing: true`, `specialEffectText`에 `관통` 또는 `Piercing`; 일반공격의 각 타격 후 같은 열 반대 행에 별도 피해; 쉴더 이전 적용
- 더블/트리플 어택: `hitsPerAttack: 2` 또는 `3`, `specialEffectText`에 `Double Attack` 또는 `Triple Attack`
- 트랩: `definitionType: "TrapSpell"`, `hasTrap: true`, 타입화된 `trapTrigger`·`trapEffect`·별도 `trapActivationCost`. 내 행동 턴에 무료 설치, 조건·발동 비용 충족 시 1회 소모. 같은 소유자의 동일 CardId 중복 설치 불가, 다른 트랩은 설치 순서대로 검사하며 조건/비용 미충족은 건너뛴다. 트랩: 리턴은 중립 Rare·발동 골드4·강화 불가·드래프트/보상 On(사용자 예외). [규칙](battle_rules.md#trap-return)
- 트랩-미러 필드: `TrapSpell`, CardId `TrapMirrorField`, 판타지 Rare, 설치 무료·발동 마나 2(부족분 골드 1:1 대체). `EnemyUnitAttacksUnit` / `ReflectAttackerAttackAsMagic`: 상대 유닛·마스터가 내 유닛·마스터를 공격하면 공격을 취소하고 공격자의 현재 ATK와 내 현재 주문력으로 본인에게 Magic 피해를 준다. 건물 제외, 마법 방어·피해 보호 적용. 강화 불가·드래프트 On·보상 Off; 상대 비공개, 같은 CardId 동시 설치 불가, 성공 발동 1회 소모. 반사 실제 피해 표시와 온라인 사망·부활 판정을 확인한다. [규칙](battle_rules.md#trapmirrorfield)
- 스크립트 마법: `definitionType: "ScriptedSpell"`, 지원 중인 `effectId`만 사용

새 효과라면 바로 `cards.json`에만 적지 말고 다음을 먼저 정한다.

- 언제 발동하는가
- 어떤 대상에게 적용되는가
- 마스터, 건물, 방전(Drained), 망각(Erasure), 스턴(Stun), 빙결(Freeze), Shielder, Endure와 어떻게 상호작용하는가
- 서버 PvP에서도 같은 방식으로 처리할 수 있는가
- 어떤 전투 이벤트와 연출이 필요한가

## 1-1. 새 카드마다 강화 증가량을 사용자에게 요청

2026-09-12 사용자 지시. 새 카드를 추가할 때마다 아래 정보를 먼저 요청한다.

- 강화 가능 여부. 불가능하다고 지정하면 레벨별 증가치는 필요 없다.
- 강화 가능하면 Lv.1~13 각각의 능력치·효과 증가량. **해당 레벨에서 추가되는 값**과 누적 결과를 구분한다.
- 공격력·HP·방어력·마법 피해·기타 효과 중 무엇이 변하는지 명시하고, 지정되지 않은 새 증가 효과를 만들지 않는다.
- 같은 증가량을 갖는 레벨은 묶어서 답해도 된다. 사용자가 해당 카드에 기존 성장 방식을 명시적으로 지정해도 되지만, 모든 레벨의 동작이 확정되어야 한다.
- 카드 종류·희귀도·공격 가능 여부·유사 카드나 코드의 기본 분기를 근거로 강화 방식을 자동 지정하지 않는다.
- 해당 카드의 규칙을 이미 전달받았다면 재확인하지 않는다. 미정인 부분만 질문하고, 답변 전에 강화 규칙을 임의로 구현하지 않는다.
- 확정한 증가량은 카드 상세와 메타 규칙에 기록하고, 각 레벨 증가량·Lv.13 합계·실제 소환 또는 마법 적용값을 검증한다.

현재 확정 예: 케르베로스는 Lv.1~12 HP +1, Lv.13 ATK +1(최종 10/78). 마나의 샘은 Lv.1~13 모두 HP +1(최종 0/33). 이 예를 다른 카드에 자동 적용하지 않는다.

## 2. cardId 정하기

`cardId`는 카드의 영구 식별자다.

권장 규칙:

- 가능하면 영문/숫자/언더스코어/하이픈만 사용한다.
- 이미 쓰인 `cardId`는 기본적으로 바꾸지 않는다. 사용자 요청으로 변경할 때는 저장 데이터·영수증 변환 마이그레이션과 서버·클라이언트 동시 반영 절차를 함께 준비한다. `ElfElder` 통일(2026-10-09)은 마이그레이션 0024를 따른다.
- 카드 이미지 파일명, 덱 목록, 서버 메시지, 저장 데이터가 모두 이 값을 기준으로 연결된다.
- 임시 이름인 `starter_firebolt` 같은 별도 ID를 만들지 말고 실제 카드 ID를 그대로 사용한다.

예시:

```json
"id": "Goblin"
```

## 3. 새 카드 요청 양식

새 카드를 추가하기 전에 아래 양식으로 먼저 정리한다.

```text
카드 이름:
cardId:
개발 상태:
덱 빌딩 등장: On / Off
랜덤 보상 등장: On / Off
강화 가능 여부: 가능 / 불가능
강화 레벨별 추가 증가량 (강화 가능일 때 필수; 같은 규칙의 레벨은 묶어도 됨):
  Lv.1:
  Lv.2:
  Lv.3:
  Lv.4:
  Lv.5:
  Lv.6:
  Lv.7:
  Lv.8:
  Lv.9:
  Lv.10:
  Lv.11:
  Lv.12:
  Lv.13:
Lv.13 최종 능력치/효과 수치 (기본값 + 누적 증가량):

비용:
희귀도:
소속:
유닛/건물/마법:
차지 타일:
효과:
특수효과:
공격 타입:
데미지 유형:
공격력:
체력:
물리 방어력:
마법 방어력:
이동 가능 여부:

일반 효과 문구:
특수효과 문구:
발동 시점:
대상:
처리 순서:
지속 시간과 종료 조건:
중첩 가능 여부:
마스터/유닛/건물 대상 여부:
Shielder/Endure/LifeSteal/Drained/Erasure 상호작용:
다단히트 처리:
필요한 전투 이벤트:
필요한 이펙트/애니메이션/효과음:

카드 이미지 있음:
보드 스프라이트/애니메이션 있음:
```

작성 기준:

- `속성/Attribute`는 더 이상 사용하지 않는다.
- `데미지 유형`은 `Physical`, `Magic`, `Fixed`, `None` 중 하나로 적는다.
- 공격력이 있는 유닛/건물은 `None`을 사용할 수 없다.
- 물리/마법 방어력은 `0` 이상의 정수다. 기본값은 `0/0`이며, 현재 골렘은 `3/0`, 마왕은 `3/3`을 사용한다.
- `강화 가능 여부`는 `강화 가능` 또는 `강화 불가능`으로 적는다.
- `개발 상태`는 `기획 중`, `효과 개발 중`, `기능 검증 중`, `출시 가능`처럼 현재 단계를 적는다.
- `덱 빌딩 등장`과 `랜덤 보상 등장`은 각각 `On` 또는 `Off`로 적는다.
- `includeInDraft`는 덱 빌딩 등장 여부, `includeInRewards`는 랜덤 카드 보상 등장 여부다. 생략하면 둘 다 `true`다.
- 효과만 먼저 개발하는 카드는 두 값을 모두 `false`로 두고, 이미지와 실전 검증이 끝난 뒤 필요한 값만 `true`로 바꾼다.
- 강화 가능 여부는 `CardUpgradeRules`에서 결정한다.
- 강화 불가능 카드는 보유 카드 목록에서 `강화불가`로 표시되고 원칙적으로 카드 보상 풀에서도 제외된다. 사용자 확정 트랩: 리턴만 강화 불가·보상 On이며, 새 비강화 카드에는 이 예외를 자동 적용하지 않는다.

- 트랩 - 반응 장갑: `TrapSpell` / `ExplosiveReactiveArmour`, 과학문명 Rare, 설치 무료·발동 전력 4(부족분 골드 대체), `EnemyMeleeUnitAttacksMaster` / `DestroyAttacker`, `damageType: "None"`. 상대 일반 근거리 유닛의 내 마스터 근접 공격을 취소하고 직접 파괴. `AttackType.Melee` 기준이며 원거리 유닛·마스터·건물 공격자는 제외한다. 강화 불가·드래프트 On·보상 Off. 정상 파괴/부활/연쇄와 비공개·실제 이미지 공개를 확인한다. [규칙](battle_rules.md#explosivereactivearmour)

- 트랩 - 아이언 돔: `TrapSpell` / `IronDome`, 과학문명 Rare, 설치 무료·발동 전력 4(부족분 골드 대체), `EnemyRangedUnitAttacksMaster` / `DestroyAttacker`, `damageType: "None"`. 상대 일반 원거리 유닛(`AttackType.Ranged`)의 내 실제 마스터 공격을 피해 전에 취소하고 직접 파괴한다. 근거리 유닛·마스터·건물 공격자는 제외하고, 환술의 최종 대상 및 승인된 공격 비용 후 판정한다. 강화 불가·드래프트 On·보상 Off. 리턴의 비강화 보상 예외를 적용하지 않는다. [규칙](battle_rules.md#irondome), [그림·등록](iron_dome.md)

- 트랩 - 서먼 트랩: `TrapSpell` / `SummonTrap`, 중립 Common, 설치 무료·발동 전투 골드 1, `EnemyUnitSummoned`(5) / `DamageSummonedUnitPhysical`(4), `damage: 20` / `damageType: "Physical"`. 성공한 상대 일반 유닛 손패 소환의 정상 초기화 후 해당 유닛에 직접 Physical 피해. Lv.1–13 매 레벨 피해 +1, Lv.13 33; 설치 때 ArmedTrapState.EffectAmount에 강화 피해 포착. 마스터·건물·이동·고독·패 반환·부활 제외, 쉴더 이전·주문력 없음. 강화 가능·드래프트/보상 On이며 소환 비용/손패는 돌려주지 않는다. 새 소환 runtimeId의 공개·피해·사망 표시와 비밀 정보/복원을 함께 확인한다. [규칙](battle_rules.md#summontrap).

### 3.1 트랩 카드의 추가 작성 항목

트랩은 마법 공통 항목에 아래 내용을 추가한다. 유닛/건물의 ATK·HP·이동·종족은 트랩의 전장 능력치로 지정하지 않는다.

```text
유형: TrapSpell / hasTrap: true
설치 비용: 0 (무료)
발동 비용: 마나 / 기 / 전력 / 전투 골드 각각의 값
발동 조건 (trapTrigger):
발동 효과 (trapEffect):
판정 순서: 조건 발생·비용 검증·피해/반격 전후·1회 소모 시점
대상 포함/제외: 일반 유닛 / 마스터 / 건물 / 소유자
동일 CardId 중복 설치: 불가 (현재 공통 규칙)
서로 다른 트랩의 처리: 설치 순서, 조건/비용 미충족 건너뜀
반환 등 비피해 제거: 현재 소유자, 새 일반 손패 카드, 손패 상한 처리
강화 가능 여부와 레벨별 증가량:
드래프트 / 보상: 각각 On / Off, 비강화 보상 예외는 별도 사용자 확인
설치 중 비밀 정보 / 발동 공개 정보:
중앙 공개 그림 / 숨김 공통 그림 / 마스터 ?·Xn 표시:
```

현재 확정 트랩은 트랩: 리턴, 트랩-미러 필드, 트랩 - 반응 장갑, 트랩 - 아이언 돔, 트랩 - 서먼 트랩이다. 아래는 리턴의 개별 규칙이다. 무료 설치·골드 4 발동·강화 불가·드래프트/보상 On이며, 보상 On은 사용자 지정 예외다. 일반 상대 유닛의 마스터 공격을 피해·쉴더·반격 전에 취소하고 현재 소유자에게 새 일반 카드로 반환한다. 손패가 가득 차면 반환 카드는 소실되지만 공격 취소는 유지한다. 실제 피해는 없으며 `damageType: "Magic"` 메타데이터를 피해 효과로 해석하지 않는다.

`TrapSpellCardDefinition`의 조건/효과 enum과 발동 비용으로 확장한다. 미지원 조건·효과를 문구나 임의 문자열만 추가해 처리하지 않는다. [확정 전투 규칙](battle_rules.md#trap-return).

## 4. cards.json에 카드 데이터 추가

파일 위치:

```text
Assets/Project333/Resources/Project333/Data/cards.json
```

공통 필드:

```json
{
  "id": "NewCardId",
  "displayName": "카드 이름",
  "definitionType": "Unit",
  "rarity": "Common",
  "includeInDraft": false,
  "includeInRewards": false,
  "affiliation": "Neutral",
  "chargeTileFootprint": "OneByOne",
  "cost": { "mana": 1 },
  "effectText": "",
  "specialEffectText": ""
}
```

유닛 예시:

```json
{
  "id": "ExampleUnit",
  "displayName": "예시 유닛",
  "definitionType": "Unit",
  "rarity": "Common",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "OneByOne",
  "cost": { "mana": 1 },
  "attackType": "Melee",
  "damageType": "Physical",
  "attack": 10,
  "health": 10,
  "physicalDefense": 0,
  "magicDefense": 0,
  "canMove": true,
  "hasRush": false,
  "hasReplicate": false,
  "races": [],
  "sealboundOwnerTurnStarts": 0,
  "hasHiding": false,
  "hasFlying": false,
  "hasPiercing": false,
  "spellPower": 0,
  "invincibleDuration": "None",
  "invincibleOwnerTurns": 0
}
```

속공 유닛은 `hasRush: true`로 바꾸고 `specialEffectText`에 `속공` 또는 `Rush`를 반드시 적는다.
속공은 공격 횟수를 추가하거나 대상 지정, ATK 0, Drained 제한을 무시하지 않는다. 소환 턴에 망각이 적용되면 속공이 억제되어 일반 소환 후 공격 제한이 다시 적용된다.

복제는 유닛, 건물, 마법 모두 사용할 수 있다. `hasReplicate: true`로 바꾸고 `specialEffectText`에 `복제` 또는 `Replicate`를 반드시 적는다. 총비용이 0인 카드도 허용한다. 복제 사본은 원본 CardDefinition의 비용과 규칙을 다시 사용하므로 손패에서 받은 임시 비용 감소나 임시 ATK/HP 변경을 상속하지 않지만, 계정의 전투 시작 카드 강화 레벨은 정상 적용한다. 사용하지 않은 사본은 소유자의 턴 종료 시 버린 카드 더미로 가지 않고 사라진다.

봉인(`Sealbound`)은 구현되어 있다. 유닛 또는 건물에 `sealboundOwnerTurnStarts: n`을 사용하며, `0` 또는 필드 생략은 봉인 없음이다. `1` 이상이면 소환 즉시 봉인되고 이후 소유자의 턴 시작 마지막 단계마다 `1` 감소한다. `specialEffectText`에는 `봉인` 또는 `Sealbound`를 반드시 적는다. 봉인 중에는 공격·반격·이동·위치 교환·효과·유지비·대상 지정·피해·회복·버프·디버프·Shielder·로봇 검색·전열차단에 참여하지 않으며 망각도 받지 않는다. 해제 턴에는 속공이 있을 때만 바로 공격할 수 있다.

은신(`Hiding`)은 구현되어 있다. 유닛 전용 `hasHiding: true`를 사용하고 `specialEffectText`에는 `은신` 또는 `Hiding`을 반드시 적는다. 은신 중에는 상대 일반공격과 상대 단일 대상 마법의 지정을 막고 전열차단과 Shielder에 참여하지 않는다. 합법적인 일반공격 선언, 모든 종류 피해에 의한 실제 HP 감소 또는 Drained/Erasure 진입 시 영구 해제되며, 봉인 중에는 봉인이 우선되어 해제 후 은신 상태로 돌아온다. 건물과 마스터에는 사용할 수 없다.

비행(`Flying`)은 구현되어 있다. 유닛 또는 건물에 `hasFlying: true`를 사용하고 `specialEffectText`에는 `비행` 또는 `Flying`을 반드시 적는다. 지상 근접 공격자도 활성 비행 소환물을 일반공격할 수 있다. 지상 근접의 일반공격·관통 피해는 대상의 방어력·상태 적용 후 절반으로 줄이고 소수점은 버린다. 비행이 없는 근접 소환물의 반격 피해도 타격마다 같은 감소를 적용하며, 마법·효과 피해는 적용하지 않는다. 비행 근접 공격자는 전열차단을 무시하며, 활성 비행 소환물은 전열차단을 하지 않는다. Drained와 Erasure는 비행을 억제하고, 봉인과 은신이 활성화되어 있으면 해당 상태의 대상 지정 규칙이 먼저 적용된다. 마스터의 비행은 런타임 전장 설정에서 지원하며 일반 `cards.json` 카드 레코드로 제작하지 않는다.

관통(`Piercing`)은 구현되어 있다. 유닛 또는 공격 가능한 건물에 `hasPiercing: true`를 사용하고 `specialEffectText`에는 `관통` 또는 `Piercing`을 반드시 적는다. 실제 일반공격 대상이 전열이면 후열에, 후열이면 전열에 각 타격 후 같은 열 반대 행으로 공격자의 동일한 타격 ATK와 데미지 유형의 별도 피해를 준다. 관통은 Shielder가 대신 받을 수 있지만 추가 반격이나 재귀 관통은 발생하지 않는다. 일반공격·관통은 원래 보호 대상의 방어력·상태·비행 감소를 먼저 적용하고 Shielder의 방어력·상태를 적용한다. 초과분에는 원래 대상의 피해 감소를 중복 적용하지 않는다. 단일 대상 마법은 기존 Shielder 우선 처리 순서를 유지한다. Invincible·Endure·Sealbound 판정은 정상 적용한다. 다단히트는 매 타격마다 발동하고 반대 행 피해의 실제 HP 감소량(대신 받은 Shielder와 초과분 포함)도 LifeSteal에 포함한다. HuanShu는 원래 클릭 대상이 아니라 서버가 확정한 실제 대상의 열을 사용한다. Erasure 중에는 관통이 무효화된다. 마스터의 관통은 런타임 전장 설정에서 지원하며 일반 `cards.json` 카드 레코드로 제작하지 않는다.

주문력(`SpellPower`)은 구현되어 있다. 유닛 또는 건물에 `spellPower: n`을 사용하며 `0` 또는 필드 생략은 주문력 없음이다. `specialEffectText`에는 `주문력` 또는 `SpellPower`를 반드시 적는다. 마스터 주문력은 런타임 전장 설정에서 지원하며 일반 `cards.json` 카드 레코드로 제작하지 않는다.

- **모든 Magic 피해**에 피해 소유자의 살아 있는 활성 아군 주문력 합계를 더한다. 일반 공격·반격, 유닛·건물·마스터 효과, 즉시·지속·지연 마법 피해를 포함한다. Physical/Fixed 피해에는 적용하지 않는다.
- 합계는 **피해 발생 시점**에 계산한다. 파이어월 시전 후 제공자를 소환하면 다음 발동부터 반영하고, 제공자가 사라지거나 억제되면 이후 발동에서 제외한다. 시전 시 주문력을 저장하지 않는다.
- 광역은 각 대상, 다단히트는 각 타격에 전액 적용한다. 기존 광역·근접 반격의 동시 처리 규칙을 유지하며, 쉴더 전달·넘치는 피해에서 주문력을 중복 가산하지 않는다.
- Drained·Erasure·Sealbound 상태에서는 제공하지 않지만 Hiding·Flying은 주문력을 억제하지 않는다.
- 툴팁: `마법 데미지를 입힐 때, 주문력만큼의 추가 마법 데미지를 준다.`
- 새 카드의 강화로 주문력 자체가 증가하는지는 사용자에게 별도로 확인한다. 카드 종류를 근거로 증가 규칙을 임의로 정하지 않는다.

무적(`Invincible`)은 구현되어 있다. 유닛 또는 건물에 `invincibleDuration`을 사용하고 `specialEffectText`에는 `무적` 또는 `Invincible`을 반드시 적는다. 지원 값은 `Always`, `SummonTurn`, `OwnerTurnOnly`, `OpponentTurnOnly`, `UntilTurnEnd`, `OwnerTurns`, `GlobalTurnEnds`이며, 무적이 없으면 `None` 또는 필드 생략이다. `OwnerTurns`와 `GlobalTurnEnds`를 사용할 때 `invincibleOwnerTurns: n`에 `1` 이상의 값을 넣는다. `OwnerTurns`는 소유자의 턴 종료 때만 감소하고 카드 문구는 `내 n턴 동안 무적`처럼 작성한다. `GlobalTurnEnds`는 어느 플레이어의 턴이든 끝날 때 감소하며 용사의 기존 소환 무적은 제거되었지만 공용 지속 방식으로 계속 지원한다. 다른 지속 방식에서는 `invincibleOwnerTurns`를 `0`으로 둔다. 무적은 물리·마법·고정 피해로 인한 HP 감소를 막지만 대상 지정과 비피해 제거 효과는 막지 않는다. Drained와 Erasure는 무적을 억제하지만 지속 시간은 계속 흐르며, 재접속 시 각 무적 효과의 지속 방식과 남은 횟수가 복원된다. 마스터 무적은 런타임 전장 설정에서 지원하며 일반 `cards.json` 카드 레코드로 제작하지 않는다.

유닛은 `races` 배열을 설정한다. 여러 종족을 허용하며 종족이 없으면 `[]`이다. 건물·마법에는 필드를 넣지 않는다. 망각·방전에도 종족은 유지된다. 로봇은 특수효과가 아니므로 `hasRobot`이나 특수효과 문구로 추가하지 않는다. Unity 유닛 자산의 Unit Races도 일치시킨다. [종족 목록과 규칙](unit_races.md)을 따른다.

로봇 공장의 후보군은 `races: ["Robot"]`(또는 여러 종족 중 Robot 포함)만으로 충분하지 않다. 반드시 `definitionType: "Unit"`이고 `includeInDraft: true`여야 한다. 효과 개발 중이라 `includeInDraft: false`인 로봇은 공장에서도 생성되지 않는다.

건물 예시:

```json
{
  "id": "ExampleBuilding",
  "displayName": "예시 건물",
  "definitionType": "Building",
  "rarity": "Common",
  "affiliation": "Neutral",
  "chargeTileFootprint": "OneByOne",
  "cost": { "gold": 2 },
  "effectText": "턴 시작 시 골드 +1",
  "damageType": "None",
  "attack": 0,
  "health": 20,
  "physicalDefense": 0,
  "magicDefense": 0,
  "hasPiercing": false,
  "spellPower": 0,
  "invincibleDuration": "None",
  "invincibleOwnerTurns": 0,
  "canAttack": false,
  "turnStartResourceGain": { "gold": 1 }
}
```

단일 피해 마법 예시:

```json
{
  "id": "firebolt",
  "displayName": "파이어볼",
  "definitionType": "DamageSpell",
  "rarity": "Common",
  "affiliation": "Fantasy",
  "chargeTileFootprint": "None",
  "cost": { "mana": 1 },
  "effectText": "대상 하나에게 마법 피해 10",
  "damageType": "Magic",
  "damage": 10
}
```

스크립트 마법 예시:

```json
{
  "id": "CheonraJimang",
  "displayName": "천라지망",
  "definitionType": "ScriptedSpell",
  "rarity": "Rare",
  "affiliation": "Murim",
  "chargeTileFootprint": "None",
  "cost": { "qi": 5 },
  "effectText": "유닛을 하나 선택하고, 내 다음 턴 시작 시 그 유닛 파괴",
  "effectId": "cheonra_jimang"
}
```

## 5. 강화 가능 여부 연결

새 카드의 `강화 가능 여부`를 정한 뒤에는 아래를 확인한다.

- 강화 가능 카드라면 `CardUpgradeRules.IsCardUpgradeable(...)`에서 true가 되도록 한다.
- 강화 불가능 카드라면 `CardUpgradeRules.IsCardUpgradeable(...)`에서 false가 되도록 한다.
- 강화 불가능 카드는 원칙적으로 보상 카드 풀에서 제외한다. 사용자 확정 예외인 TrapReturn만 보상에 포함하고 새 비강화 카드에 이 예외를 확대하지 않는다.
- 강화 가능 카드는 보유 카드 씬에서 다음 강화 비용과 초록 테두리 표시가 정상적으로 떠야 한다.
- 목표 Lv.1/2/3/4/5 비용은 동일 카드·계정 골드 각각 3/6/10/20/30, 목표 Lv.6~13은 각각 33이다. 카드 종류·희귀도와 무관한 [공통 비용표](meta_rules.md#card-upgrade-costs)를 사용하며, 이 비용 변경은 기존 카드별 HP/ATK/효과 증가량을 바꾸지 않는다. 실제 계정 차감은 서버 DB의 card_upgrade_costs와 순서대로 적용한 migration0020·0021을 따른다.
- 사용자가 확정한 Lv.1~13 증가량이 CardLevelStatRules 및 필요한 효과 처리에 반영되고, 카드 표시와 실제 전투 적용값이 일치하는지 확인한다.
- 마법 카드는 강화 가능 카드와 강화 불가능 카드의 정책을 카드별로 명확히 정한다.

현재 카드별 강화 정책:

| cardId | 카드 이름 | 종류 | 강화 가능 여부 | 기준 |
| --- | --- | --- | --- | --- |
| A-111 | A-111 | Unit | 강화 가능 | 유닛 |
| A-212 | A-212 | Unit | 강화 가능 | Lv.1~12 HP +1, Lv.13 ATK +1; Lv.13 ATK/HP 7/34; 드래프트·보상 On |
| A-301 | A-301 | Unit | 강화 가능 | 3·6·9·13레벨 ATK +1, 나머지 레벨 HP +1, 드래프트·보상 On |
| TrapReturn | 트랩: 리턴 | TrapSpell | 강화 불가 | 설치 무료·발동 골드4·반환/공격 취소 불변, 드래프트 On·사용자 확정 비강화 보상 On |
| TrapMirrorField | 트랩-미러 필드 | TrapSpell | 강화 불가 | 설치 무료·발동 마나 2(골드 대체), 공격자 현재 ATK 반사·공격 취소 불변, 드래프트 On·보상 Off |
| MagicAmplificationCircle | 마법 증폭진 | ScriptedSpell | 강화 불가 | 판타지 커먼, 마나 1, 내 턴 종료까지 주문력 +10, 중첩 가능, 드래프트·보상 Off |
| FireSpear | 파이어스피어 | ScriptedSpell | 강화 가능 | 판타지 커먼·마나3, 매 레벨 Magic15+레벨(Lv.13 28)+현재 주문력. 선택한 대상 전장의 앞·뒤2칸; 드래프트·보상 On |
| Meteor | 메테오 | ScriptedSpell | 강화 가능 | 사용자 확정 판타지 유니크, 마나 7. 매 레벨 기본 마법 피해 +1(40+레벨), Lv.13 53에 현재 아군 주문력 추가. 비용 고정, 건물 배수 없음, 드래프트·보상 On |
| Earthquake | 지진 | ScriptedSpell | 강화 가능 | 중립 언커먼, 골드 3. 매 레벨 기본 물리 피해 +1(10+레벨), 건물에는 강화 뒤 3배를 방어 전에 적용. Lv.13 유닛/마스터 23·건물 69. 드래프트·보상 On |
| Nuclear | 뉴클리어 | ScriptedSpell | 강화 가능 | 전력 10, 즉시 적 전장 전체 고정 피해 100 + 레벨(Lv.13: 113); 사용 당시 상대 전장에 있는 유닛(마스터 포함)에만 영구 낙진 +1중첩, 건물은 낙진 제외. 중첩당 10은 강화 불변. 과학문명 레전더리, 드래프트·보상 On |
| PlasmaShield | 플라즈마 쉴드 | ScriptedSpell | 강화 불가 | 부여 방어력 +10 고정. 드래프트 On·랜덤 카드 보상/카드팩 Off |
| ManaShield | 마나쉴드 | ScriptedSpell | 강화 불가 | 부여 방어력 +10 고정. 드래프트 On·랜덤 카드 보상/카드팩 Off |
| BiochemicalBomb | 생화학폭탄 | ScriptedSpell | 강화 가능 | 카드 레벨당 4회 지속 고정 피해 +1 (기본 25), 방어력·주문력 미적용, 드래프트·보상 On |
| BlueDragon | 블루 드래곤 | Unit | 강화 가능 | 기본 ATK/HP 40/50, 표준 강화 (Lv.13: 44/59) |
| Cerberus | 케르베로스 | Unit | 강화 가능 | Lv.1~12 HP +1, Lv.13 ATK +1; Lv.13 ATK/HP 10/78 |
| ElfLongbowScout | 엘프 장궁수 | Unit | 강화 가능 | 유닛 |
| ElfElder | 엘프 장로 | Unit | 강화 가능 | 사용자 확정: Lv.3/6/9/13 ATK+1, 나머지 HP+1; Lv.13 34/34. 공백 없는 CardId 사용, 마나5·방어0/0·엘프·관통·속공 불변, 드래프트·보상 On |
| firebolt | 파이어볼 | DamageSpell | 강화 가능 | 강화 가능한 마법으로 예외 허용 |
| Firewall | 파이어월 | ScriptedSpell | 강화 가능 | 카드 레벨당 지속 피해 +1 |
| Goblin | 고블린 | Unit | 강화 가능 | 유닛 |
| GoldMiner | 금광 채굴꾼 | Unit | 강화 가능 | 유닛 |
| Golem | 골렘 | Unit | 강화 가능 | 유닛 |
| GaebangChief | 개방 방주 | Unit | 강화 가능 | 사용자 확정: Lv.3/6/9/13 기본 ATK +1, 나머지 HP +1; Lv.13 14/69. 골드당 +10 동적 보너스·기 7·방어 0/0·인간 종족 불변, 드래프트·보상 On |
| Gwangma | 광마 | Unit | 강화 가능 | 유닛 |
| Inn | 객잔 | Building | 강화 가능 | 매 레벨 HP +1, 이미지 준비 전 드래프트·보상 제외 |
| ManaCannon | 마나 대포 | Unit | 강화 가능 | 사용자 확정: Lv.3/6/9/13 기본 ATK +1, 나머지 HP +1; Lv.13 44/19. 소환 마나 2·공격 마나 1·골드 1:1 대체·방어 0/0·종족 없음 불변, 드래프트·보상 On |
| ManaPond | 마나의 샘 | Building | 강화 가능 | 마나 6, 주문력 +20; Lv.1~13 HP +1, ATK 증가 없음; Lv.13 ATK/HP 0/33, 주문력 20 유지 |
| ManaWeaver | 마나 위버 | Unit | 강화 가능 | 유닛 |
| RedDragon | 레드 드래곤 | Unit | 강화 가능 | 기본 ATK/HP 40/40, 표준 강화 (Lv.13: 44/49) |
| Shaolin_1st_Disciple | 소림 1대 제자 | Unit | 강화 가능 | 유닛 |
| Shieldbearer | 방패병 | Unit | 강화 가능 | 유닛 |
| Vampire | 뱀파이어 | Unit | 강화 가능 | 유닛 |
| CheonraJimang | 천라지망 | ScriptedSpell | 강화 불가능 | 드래프트 On. includeInRewards 생략 기본값 On이나 서버 실제 보상 Off |
| Daehwandan | 대환단 | ScriptedSpell | 강화 가능, 매 레벨 효과수치 +1 | Lv.0 30 → Lv.13 43만큼 내 마스터 공격력 증가. 기 생산 없음. 드래프트·보상·카드팩 On |
| TenThousandYearSnowGinseng | 영약: 만년설삼 | PersistentResourceSpell | 강화 불가능 | 다음 3번의 내 턴 시작마다 기 +3, 드래프트 On·보상 Off |
| Gu | 고독 | ScriptedSpell | 강화 불가능 | 소유권 이전 마법, 드래프트 On·보상 Off |
| HuanShu | 환술 | ScriptedSpell | 강화 불가능 | 영구적으로 일반 공격 대상 무작위 변경, 드래프트 On·보상 Off |
| ManaStone | 마정석 | ScriptedSpell | 강화 불가능 | 즉시 자원 획득 마법, 드래프트 On·보상 Off |
| ManaStoneBundle | 마정석 꾸러미 | ScriptedSpell | 강화 불가능 | 즉시 자원 획득 마법, 드래프트 On·보상 Off |
| MerchantCaravan | 상단 | Building | 강화 가능 | 매 레벨 HP +1, 드래프트·보상 On |
| Microreactor | 초소형 발전기 | PersistentResourceSpell | 강화 불가능 | 드래프트 On. includeInRewards 생략 기본값 On이나 서버 실제 보상 Off |
| PowerBank | 보조배터리 | ScriptedSpell | 강화 불가능 | 즉시 자원 획득 마법, 드래프트 On·보상 Off |
| PowerPlant | 발전소 | Building | 강화 가능 | 매 레벨 HP +1, 드래프트·보상 On |
| NuclearPowerPlant | 원자력 발전소 | Building | 강화 가능 | 매 레벨 HP +1, 파괴 시 양쪽 전장 물리 피해 30, 드래프트·보상 On |
| RobotFusion | 로봇 합체 | ScriptedSpell | 강화 불가능 | 이미지 준비 전 드래프트·보상 제외 |
| RobotFactory | 로봇 공장 | Building | 강화 가능 | 매 레벨 HP +1, 이미지 준비 전 드래프트·보상 제외 |
| GaebangBranch | 개방 분타 | Building | 강화 가능 | 매 레벨 HP +1, 드래프트·보상 On |
| Skeleton | 스켈레톤 | Unit | 강화 가능 | 표준 강화, 드래프트·보상 On |
| SkeletonArcher | 스켈레톤아처 | Unit | 강화 가능 | 사용자 확정: Lv.3/6/9/13 ATK +1, 나머지 HP +1; Lv.13 14/12. 마나1·방어0/0·언데드·특수효과 없음 유지, 드래프트·보상 On |
| Zombie | 좀비 | Unit | 강화 가능 | 표준 강화, 드래프트·보상 On |
| OrcWarrior | 오크 전사 | Unit | 강화 가능 | 기본 ATK/HP 20/40. 표준 강화; Lv.13 24/49, 드래프트·보상 On |
| OrcBerserker | 오크 광전사 | Unit | 강화 가능 | 사용자 확정: Lv.3/6/9/13 ATK +1, 나머지 HP +1; Lv.13 24/59. 비용·방어·종족·버서커 유지, 드래프트·보상 On |
| OrcChieftain | 오크 족장 | Unit | 강화 가능 | 사용자 확정: Lv.3/6/9/13 ATK +1, 나머지 HP +1; Lv.13 기본54/79·자기오라1개64/89. 오라각+10은 강화 불변, 드래프트·보상 On |
| Werewolf | 웨어울프 | Unit | 강화 가능 | 표준 강화, 드래프트·보상 On |
| DemonKing | 마왕 | Unit | 강화 가능 | Lv.3/6/9 ATK +1, Lv.13 ATK +3, 나머지 HP +1, 드래프트·보상 Off |
| Hero | 용사 | Unit | 강화 가능 | Lv.3/6/9 ATK +1, Lv.13 ATK +3, 나머지 HP +1, 드래프트·보상 Off |
| ExplosiveReactiveArmour | 트랩 - 반응 장갑 | TrapSpell | 강화 불가 | 설치 무료·발동 전력4(골드 대체), 상대 일반 근거리 유닛의 내 마스터 근접 공격 취소·공격자 직접 파괴, Lv.1–13 증가 없음, 드래프트 On·보상 Off |
| IronDome | 트랩 - 아이언 돔 | TrapSpell | 강화 불가 | 설치 무료·발동 전력4(골드 대체), 상대 일반 원거리 유닛의 내 마스터 공격 취소·공격자 직접 파괴, Lv.1–13 증가 없음, 드래프트 On·보상 Off |
| SummonTrap | 트랩 - 서먼 트랩 | TrapSpell | 강화 가능 | 설치 무료·발동 골드1, 성공한 상대 일반 유닛 손패 소환 후 직접 Physical20+레벨, 매 Lv.1–13 피해 +1·Lv.13 33, 설치 당시 피해 포착, 드래프트·보상 On |
| TimedBomb | 시한폭탄 | ScriptedSpell | 강화 가능 | 비용 전력 3, 매 레벨 물리 피해 +1 (기본 33), 드래프트·보상 On |

정책 테스트:

- `CardUpgradeRulesTests`는 현재 `cards.json`의 강화 가능/불가능 카드 목록을 고정한다.
- 새 카드를 추가하면 이 표와 `CardUpgradeRulesTests`의 기대 목록도 함께 갱신한다.

## 6. 새 효과가 필요할 때 구현할 곳

카드가 기존 효과만 사용하면 이 단계는 건너뛴다.

새 효과가 필요하면 보통 아래를 함께 수정한다.

- 규칙 문서: `docs/battle_rules.md`
- JSON DTO: `JsonCardDefinitionRecord`
- 카드 정의 타입: `UnitCardDefinition`, `BuildingCardDefinition`, `SpellCardDefinition` 계열
- 소환/사용 연결: `PlayCardService`, `SpellService`; 트랩은 `InstallTrapCommand`·`TrapService`와 승인된 공격의 피해 전 또는 정상 유닛 손패 소환 초기화 완료 후의 타입별 연결점을 사용
- 전투 해상: `AttackService`, `DamageResolutionRules`, `TurnStartService`, `EndTurnService` 등 해당 효과가 발동되는 서비스
- 온라인 동기화: `BattleStateViewDto`, `BattleStateViewFactory`, `BattleStateViewProjector`
- UI/연출: `BattleBootstrapper`, `TileTextView`, `HandPresenter` 등 필요한 표시 계층
- 데이터 검증: `CardDatabaseValidator`
- 강화 가능 여부: `CardUpgradeRules`
- 테스트: 효과별 EditMode 테스트와 현재 `cards.json` 검증 테스트

## 7. 카드 이미지 추가

손패, 카드 프리뷰, 일부 보드 fallback 이미지는 `CardArtworkLibrary`가 `cardId`로 찾는다.

이미지 위치:

```text
Assets/Project333/Resources/Project333/CardArtwork/{cardId}.png
```

예시:

```text
Assets/Project333/Resources/Project333/CardArtwork/Goblin.png
Assets/Project333/Resources/Project333/CardArtwork/firebolt.png
```

권장:

- 파일명은 가능하면 `cards.json`의 `id`와 정확히 같게 둔다.
- Unity Import Settings에서 `Texture Type = Sprite (2D and UI)`로 설정한다.
- 픽셀 아트는 `Filter Mode = Point`, `Compression = None` 또는 낮은 압축을 권장한다.

### 7.1. 카드별 표시 크기와 비율 통일

현재 카드 앞면 원본은 `1086 x 1448`의 3:4 비율이다. `Default` 텍스처의
`Non Power of 2 = To nearest` 설정은 이를 `1024 x 1024`처럼 정사각형으로
변형할 수 있다. 덱 빌딩과 손패는 이미지 비율을 유지해서 표시하므로, 이렇게
불러온 카드만 세로가 짧아지고 같은 카드 칸에서 작게 보인다.

`Project333CardArtworkImporter`가 위 `CardArtwork` 폴더의 카드 앞면 PNG에
다음 설정을 자동 적용한다. 카드 뒷면과 유닛 애니메이션 시트는 대상이 아니다.

- `Texture Type = Sprite (2D and UI)`
- `Sprite Mode = Single`
- `Mesh Type = Full Rect`: 프레임과 ATK/HP 표시 영역을 포함한 사각형 전체 사용
- `Non Power of 2 = None`: 원본 가로/세로 비율 유지
- `Generate Mip Maps = Off`, `Alpha Is Transparency = On`

기존 이미지들을 일괄 복구하려면 플레이 모드를 종료하고
`Tools > Project333 > Cards > Normalize Card Artwork Imports`를 실행한다.
이미지 원본, GUID, 필터 모드, 압축, 최대 해상도, 플랫폼별 설정은 변경하지 않는다.
UI 크기와 스탯 텍스트의 글꼴/위치도 덮어쓰지 않는다. 원본 자체에 큰 여백이 있다면
별도로 이미지 여백을 조정해야 하며, 이 도구가 이미지를 자르지는 않는다.

`CardArtworkAspectTests`는 카드별 원본/스프라이트 비율과 여러 크기의 손패·선택
카드 영역에서 동일한 표시 크기를 검사한다. Android 설치본에는 다시 빌드해야 반영된다.

### 7.2. 카드 이미지의 ATK/HP 및 피해량 위치

모든 카드 수치는 실제 이미지가 그려지는 사각형을 기준으로 배치한다.
공통 기본 위치는 이미지 왼쪽 아래를 (0,0), 오른쪽 위를 (1,1)로 보았을 때
공격력/마법 피해량 (0.09, 0.055), HP (0.92, 0.055)이다.
마법 피해량은 공격력과 같은 건틀릿/지팡이 위치를 사용하며, 피해 마법에는 HP를 표시하지 않는다.

공통 계산은 `HandCardStatOverlayLayout`에 있다. Unity Image의 피벗과
실제 종횡비 유지 영역을 따르고, 이미지의 좌표를 텍스트 부모의 좌표로 변환한다.
작은 기존 StatOverlay 영역에 좌표를 강제로 가두지 않는다.
카드 확대·회전·해상도 변경에도 문양을 따라가며, 글꼴은 이 계산에서 변경하지 않는다.

화면별 Inspector 조정 위치:

- 손패: `HandCardTextView > Runtime Stat Attack/Hp Normalized Position`
- 멀리건: `BattleMulliganOverlayPresenter > Attack/Hp Stat Normalized Position`
- 덱 빌딩: `DraftOverlayPresenter > Option Attack/Hp Stat Normalized Position`
- 보유 카드: `OwnedCardsSceneController > Detail Attack/Hp Stat Normalized Position`
- 소환물 미리보기: `TileTextView > Card Preview Attack/Hp Normalized Position`
- 상대 카드 공개: `BattleBootstrapper > Opponent Played Card Attack/Hp Stat Normalized Position`

저장된 씬의 Inspector 값은 코드의 기본값보다 우선한다. 기본값만 고쳐도 기존 씬이
자동으로 바뀌는 것은 아니므로, 의도적으로 미세 조정한 값은 해당 씬에서 관리한다.

## 8. 보드 위 유닛/건물 비주얼 추가

유닛이나 건물을 타일 위에서 애니메이션으로 보여주려면 Unity 카드 에셋의 `Board Sprite`, `Board Animator Controller`도 연결한다.

확인할 것:

- 해당 카드의 `CardDefinitionAsset`이 있는가
- `CardId`가 `cards.json`의 `id`와 같은가
- Inspector의 `Board Sprite`에 기본 스프라이트가 들어갔는가
- Inspector의 `Board Animator Controller`에 Idle, Run, Attack, BeAttacked, Death가 들어 있는 컨트롤러가 연결됐는가

주의:

- `cards.json`은 전투 규칙 데이터의 기준이다.
- `CardDefinitionAsset`은 현재 드래프트 UI, 보드 비주얼, 일부 프리뷰 표시에서 여전히 사용된다.
- 보드 애니메이션은 `cards.json`만으로 자동 생성되지 않는다.

### 8.1. 파이어월·생화학폭탄 타일 이펙트 연결

두 지속 마법은 서버가 발동 범위를 이벤트로 보내고, 각 클라이언트가 범위 안의 모든 타일에서 같은 프레임 애니메이션을 동시에 재생한다. 파이어월은 선택한 `5x1`의 5개 타일, 생화학폭탄은 선택한 `4x2`의 8개 타일에서 재생하며 빈 타일도 포함한다.

`BattleBootstrapper` Inspector의 `Tile Area Spell Effects`에서 다음 값을 설정할 수 있다.

- `Firewall Tile Effect Frames`: 파이어월 프레임을 재생 순서대로 등록
- `Firewall Tile Effect Frames Per Second`: 파이어월 FPS
- `Firewall Tile Effect Size`: 타일 하나를 기준으로 한 표시 크기
- `Biochemical Bomb Tile Effect Frames`: 생화학폭탄 프레임을 재생 순서대로 등록
- `Biochemical Bomb Tile Effect Frames Per Second`: 생화학폭탄 FPS
- `Biochemical Bomb Tile Effect Size`: 타일 하나를 기준으로 한 표시 크기

Inspector 배열을 비워 두고 아래 경로에 슬라이스된 스프라이트 시트를 넣어도 자동으로 불러온다.

```text
Assets/Project333/Resources/Project333/SpellEffects/Firewall-Sheet.png
Assets/Project333/Resources/Project333/SpellEffects/BiochemicalBomb-Sheet.png
```

자동 로딩 프레임은 스프라이트 이름순으로 정렬되므로 이름을 `Firewall_00`, `Firewall_01`처럼 두 자리 번호로 짓는다. 에셋이 아직 없으면 연출만 생략하고 서버 피해 판정과 턴 진행은 기다림 없이 정상 처리된다.

## 9. 드래프트에 나오게 할지 결정

새 카드가 드래프트에 나와야 한다면 다음을 확인한다.

- `cards.json`의 `includeInDraft`가 `true`인가
- 드래프트 카드 풀 또는 카탈로그에 같은 `CardId`의 `CardDefinitionAsset`이 포함되어 있는가
- 카드 등급이 의도한 확률 그룹에 들어가는가
- 전설 카드는 첫 선택 후보로만 들어가야 하는가
- 한 덱에 같은 카드 최대 3장 제한에 걸렸을 때 정상적으로 제외되는가

효과 개발 중이거나 이미지가 아직 없다면 `includeInDraft: false`로 둔다. 랜덤 보상에서도 제외하려면 `includeInRewards: false`도 함께 둔다.

```json
"includeInDraft": false,
"includeInRewards": false
```

나중에 카드 이미지와 실전 검증이 끝나면 원하는 항목을 `true`로 바꾸고 검증 도구와 EditMode 테스트를 다시 실행한다.

## 10. 검증 순서

카드를 추가하거나 수정한 뒤에는 아래 순서로 확인한다.

1. Unity에서 `Tools > Project333 > Data > Validate cards.json` 실행
2. EditMode 테스트 실행
3. 서버 빌드 확인

서버 빌드 명령:

```powershell
dotnet build C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj
```

효과가 있는 카드라면 추가로 확인한다.

- PVE에서 손패 이미지가 보이는가
- 자원이 충분할 때 초록 glow가 뜨는가
- 드래그 사용 또는 소환이 되는가
- 보드 비주얼과 애니메이션이 맞게 재생되는가
- 물리/마법/고정 피해가 대상 방어력 및 Drained 상태와 맞게 계산되는가
- 다단히트라면 방어력이 매 타격마다 적용되는가
- Shielder 초과 피해와 LifeSteal 회복량이 최종 실제 피해를 기준으로 하는가
- 피해, 회복, 상태 표시, 죽음 처리 등이 의도대로 보이는가
- PVP 서버 전투에서도 같은 결과가 내려오는가

## 11. 자주 나는 실수

- `cards.json`에는 효과 문장만 쓰고 실제 플래그를 빼먹음
- 새 카드 요청 양식에는 `강화 가능 여부`를 적었지만 `CardUpgradeRules`를 수정하지 않음
- 사용자 확정 예외(현재 TrapReturn) 없이 강화 불가능 카드를 보상 풀에 포함함
- `specialEffectText`에는 `LifeSteal`이 있는데 `hasLifeSteal`이 false임
- `hitsPerAttack`을 3으로 했지만 `specialEffectText`에 `Triple Attack`을 안 씀
- `sciencePowerUpkeep`은 있는데 `specialEffectText`에 `전력 소모:n`을 안 씀
- 카드 이미지 파일명이 `cardId`와 다름
- 서버에는 JSON 카드가 있는데 드래프트 카탈로그에는 카드 에셋이 없음
- 새 `ScriptedSpell`의 `effectId`를 만들었지만 `CardDatabaseValidator`와 `SpellService` 구현을 안 함
- 로컬에서는 되는 것처럼 보이지만 온라인 `BattleStateView`에 필요한 상태가 안 내려감
- 트랩 설치 이벤트나 상대 StateView/AI 복사본에 비밀 CardId·비용·조건·효과를 노출함
- 반환을 죽음으로 처리하거나 손패 상한에서 전장 제거/공격 취소를 누락함

## 12. 새 카드 추가 완료 기준

새 카드는 아래를 모두 만족해야 완료로 본다.

- `cards.json`에 데이터가 있다.
- 강화 가능/불가능 정책이 코드와 UI에 반영되어 있다.
- 카드 이미지가 있다. 단, 임시 테스트 카드는 예외 가능하다.
- 필요한 경우 보드 스프라이트/애니메이터가 연결되어 있다.
- 드래프트에 나와야 하는 카드라면 드래프트 카탈로그에도 포함되어 있다.
- 새 효과라면 규칙 문서, 전투 로직, 온라인 동기화, UI 표시, 테스트가 모두 연결되어 있다.
- `Validate cards.json`가 통과한다.
- EditMode 테스트가 통과한다.
- 서버 빌드가 통과한다.
