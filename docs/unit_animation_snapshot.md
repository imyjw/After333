# 현재 등록 유닛 애니메이션 메타데이터

2026-10-20 읽기 전용 추출. StarterTenCardCatalog의 유닛 카드가 참조하는 Board Animator Controller에서 상태별 `.anim`을 따라 Sprite 키 수·Samples·StopTime·Loop Time을 읽었다. PNG 너비로 재생 프레임 수를 추정하거나 연결되지 않은 옛 클립을 현재 상태로 세지 않는다. 파일 수정·전 모션 재생·프레임별 픽셀 검사·실기기 검증은 수행하지 않았다. 실제 공격/명중/복귀/사망 시간은 클립 메타데이터와 공통 표현 코드가 함께 결정한다.

512×512 프레임·약16px 하단 여백 등 편집 규칙은 [제작 안내](unit_animation_authoring.md)를 따른다. 각 문서의 날짜별 최초 제작/과거 검증 표는 보존하고 현재 연결 값은 이 표와 실제 Assets에서 확인한다. Master는60종 카드 정의에 포함하지 않는다.

JSON 유닛 정의27종 중 네이티브 카탈로그에 등록된 유닛은25종이다. Controller 연결 25종, 상태/clip 125개를 추출했다. 현재 등록 유닛의 Controller 참조 누락은 없다.

## A-111 (`A-111`)

Controller: `Assets/Unit/A-111/A-111.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 7 | 12 | 0.583333 | 끔 | `Assets/Unit/A-111/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/A-111/BeAttacked.anim` |
| Death | 6 | 12 | 0.5 | 끔 | `Assets/Unit/A-111/Death.anim` |
| Drained | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/A-111/Drained.anim` |
| Idle | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/A-111/Idle.anim` |
| Run | 6 | 12 | 0.5 | 켬 | `Assets/Unit/A-111/Run.anim` |

## A-212 (`A-212`)

Controller: `Assets/Unit/A-212/A-212.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 8 | 8 | 1 | 끔 | `Assets/Unit/A-212/Attack.anim` |
| BeAttacked | 3 | 12 | 0.25 | 끔 | `Assets/Unit/A-212/BeAttacked.anim` |
| Death | 8 | 8 | 1 | 끔 | `Assets/Unit/A-212/Death.anim` |
| Drained | 3 | 12 | 0.25 | 켬 | `Assets/Unit/A-212/Idle.anim` |
| Idle | 3 | 12 | 0.25 | 켬 | `Assets/Unit/A-212/Idle.anim` |
| Run | 8 | 12 | 0.666667 | 켬 | `Assets/Unit/A-212/Run.anim` |

## A-301 (`A-301`)

Controller: `Assets/Unit/A-301/A-301.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 6 | 12 | 0.5 | 끔 | `Assets/Unit/A-301/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/A-301/BeAttacked.anim` |
| Death | 8 | 12 | 0.666667 | 끔 | `Assets/Unit/A-301/Death.anim` |
| Disabled | 8 | 12 | 0.666667 | 켬 | `Assets/Unit/A-301/Disabled.anim` |
| Idle | 6 | 12 | 0.5 | 켬 | `Assets/Unit/A-301/Idle.anim` |

## 블루 드래곤 (`BlueDragon`)

Controller: `Assets/Unit/BlueDragon/BlueDragon.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 6 | 12 | 0.5 | 끔 | `Assets/Unit/BlueDragon/Attack.anim` |
| BeAttacked | 6 | 12 | 0.5 | 끔 | `Assets/Unit/BlueDragon/BeAttacked.anim` |
| Death | 8 | 12 | 0.666667 | 끔 | `Assets/Unit/BlueDragon/Death.anim` |
| Idle | 5 | 12 | 0.416667 | 켬 | `Assets/Unit/BlueDragon/Idle.anim` |

## 케르베로스 (`Cerberus`)

Controller: `Assets/Unit/Cerberus/Cerberus.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 6 | 12 | 0.5 | 끔 | `Assets/Unit/Cerberus/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/Cerberus/BeAttacked.anim` |
| Death | 7 | 12 | 0.583333 | 끔 | `Assets/Unit/Cerberus/Death.anim` |
| Idle | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Cerberus/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Cerberus/Run.anim` |

## 엘프 장궁수 (`ElfLongbowScout`)

Controller: `Assets/Unit/ElfLongbowScout/ElfLongbowScout.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/ElfLongbowScout/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/ElfLongbowScout/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/ElfLongbowScout/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/ElfLongbowScout/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/ElfLongbowScout/Run.anim` |

## 엘프 장로 (`ElfElder`)

Controller: `Assets/Unit/ElfElder/ElfElder.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|

## 고블린 (`Goblin`)

Controller: `Assets/Unit/Goblin/Goblin.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/Goblin/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/Goblin/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Goblin/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/Goblin/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Goblin/Run.anim` |

## 스켈레톤 (`Skeleton`)

Controller: `Assets/Unit/Skeleton/Skeleton.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Skeleton/Attack.anim` |
| BeAttacked | 3 | 12 | 0.25 | 끔 | `Assets/Unit/Skeleton/BeAttacked.anim` |
| Death | 6 | 12 | 0.5 | 끔 | `Assets/Unit/Skeleton/Death.anim` |
| Drained | 3 | 12 | 0.25 | 켬 | `Assets/Unit/Skeleton/Idle.anim` |
| Idle | 3 | 12 | 0.25 | 켬 | `Assets/Unit/Skeleton/Idle.anim` |
| Run | 5 | 12 | 0.416667 | 켬 | `Assets/Unit/Skeleton/Run.anim` |

## 스켈레톤아처 (`SkeletonArcher`)

Controller: `Assets/Unit/SkeletonArcher/SkeletonArcher.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/SkeletonArcher/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/SkeletonArcher/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/SkeletonArcher/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/SkeletonArcher/Idle.anim` |

## 좀비 (`Zombie`)

Controller: `Assets/Unit/Zombie/Zombie.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/Zombie/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/Zombie/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Zombie/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/Zombie/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Zombie/Run.anim` |

## 오크 전사 (`OrcWarrior`)

Controller: `Assets/Unit/Orc/OrcWarrior.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Orc/Attack.anim` |
| BeAttacked | 3 | 12 | 0.25 | 끔 | `Assets/Unit/Orc/BeAttacked.anim` |
| Death | 6 | 12 | 0.5 | 끔 | `Assets/Unit/Orc/Death.anim` |
| Drained | 3 | 12 | 0.25 | 켬 | `Assets/Unit/Orc/Idle.anim` |
| Idle | 3 | 12 | 0.25 | 켬 | `Assets/Unit/Orc/Idle.anim` |
| Run | 5 | 12 | 0.416667 | 켬 | `Assets/Unit/Orc/Run.anim` |

## 오크 광전사 (`OrcBerserker`)

Controller: `Assets/Unit/OrcBerserker/OrcBerserker.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/OrcBerserker/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/OrcBerserker/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/OrcBerserker/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/OrcBerserker/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/OrcBerserker/Run.anim` |

## 오크 족장 (`OrcChieftain`)

Controller: `Assets/Unit/OrcChieftian/OrcChieftian.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/OrcChieftian/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/OrcChieftian/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/OrcChieftian/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/OrcChieftian/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/OrcChieftian/Run.anim` |

## 웨어울프 (`Werewolf`)

Controller: `Assets/Unit/Werewolf/Werewolf.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Werewolf/Attack.anim` |
| BeAttacked | 3 | 12 | 0.25 | 끔 | `Assets/Unit/Werewolf/BeAttacked.anim` |
| Death | 6 | 12 | 0.5 | 끔 | `Assets/Unit/Werewolf/Death.anim` |
| Drained | 3 | 12 | 0.25 | 켬 | `Assets/Unit/Werewolf/Idle.anim` |
| Idle | 3 | 12 | 0.25 | 켬 | `Assets/Unit/Werewolf/Idle.anim` |
| Run | 5 | 12 | 0.416667 | 켬 | `Assets/Unit/Werewolf/Run.anim` |

## 금광 채굴꾼 (`GoldMiner`)

Controller: `Assets/Unit/GoldMiner/GoldMiner.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/GoldMiner/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/GoldMiner/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/GoldMiner/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/GoldMiner/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/GoldMiner/Run.anim` |

## 골렘 (`Golem`)

Controller: `Assets/Unit/Golem/Golem.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 6 | 12 | 0.5 | 끔 | `Assets/Unit/Golem/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/Golem/BeAttacked.anim` |
| Death | 7 | 12 | 0.583333 | 끔 | `Assets/Unit/Golem/Death.anim` |
| Idle | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Golem/Idle.anim` |
| Run | 6 | 12 | 0.5 | 켬 | `Assets/Unit/Golem/Run.anim` |

## 개방 방주 (`GaebangChief`)

Controller: `Assets/Unit/GaebangChief/GaebangChief.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 8 | 12 | 0.666667 | 끔 | `Assets/Unit/GaebangChief/Attack.anim` |
| BeAttacked | 1 | 12 | 0.0833333 | 끔 | `Assets/Unit/GaebangChief/BeAttacked.anim` |
| Death | 8 | 8 | 1 | 끔 | `Assets/Unit/GaebangChief/Death.anim` |
| Drained | 1 | 1 | 1 | 켬 | `Assets/Unit/GaebangChief/Drained.anim` |
| Idle | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/GaebangChief/Idle.anim` |
| Run | 8 | 12 | 0.666667 | 켬 | `Assets/Unit/GaebangChief/Run.anim` |

## 광마 (`Gwangma`)

Controller: `Assets/Unit/Gwangma/Gwangma.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 6 | 12 | 0.5 | 끔 | `Assets/Unit/Gwangma/Attack.anim` |
| BeAttacked | 8 | 12 | 0.666667 | 끔 | `Assets/Unit/Gwangma/BeAttacked.anim` |
| Death | 7 | 12 | 0.583333 | 끔 | `Assets/Unit/Gwangma/Death.anim` |
| Idle | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Gwangma/Idle.anim` |
| Run | 6 | 12 | 0.5 | 켬 | `Assets/Unit/Gwangma/Run.anim` |

## 마나 위버 (`ManaWeaver`)

Controller: `Assets/Unit/ManaWeaver/ManaWeaver.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 6 | 12 | 0.5 | 끔 | `Assets/Unit/ManaWeaver/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/ManaWeaver/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/ManaWeaver/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/ManaWeaver/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/ManaWeaver/Run.anim` |

## 레드 드래곤 (`RedDragon`)

Controller: `Assets/Unit/RedDragon/RedDragon.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 6 | 12 | 0.5 | 끔 | `Assets/Unit/RedDragon/Attack.anim` |
| BeAttacked | 7 | 12 | 0.583333 | 끔 | `Assets/Unit/RedDragon/BeAttacked.anim` |
| Death | 7 | 12 | 0.583333 | 끔 | `Assets/Unit/RedDragon/Death.anim` |
| Idle | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/RedDragon/Idle.anim` |
| Run | 6 | 12 | 0.5 | 켬 | `Assets/Unit/RedDragon/Run.anim` |

## 소림 1대 제자 (`Shaolin_1st_Disciple`)

Controller: `Assets/Unit/Shaolin_1st_Disciple/Shaolin_1st_Disciple.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/Shaolin_1st_Disciple/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/Shaolin_1st_Disciple/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Shaolin_1st_Disciple/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/Shaolin_1st_Disciple/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Shaolin_1st_Disciple/Run.anim` |

## 방패병 (`Shieldbearer`)

Controller: `Assets/Unit/Shieldbearer/Shieldbearer.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 4 | 12 | 0.333333 | 끔 | `Assets/Unit/Shieldbearer/Attack.anim` |
| BeAttacked | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Shieldbearer/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Shieldbearer/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/Shieldbearer/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Shieldbearer/Run.anim` |

## 마나 대포 (`ManaCannon`)

Controller: `Assets/Unit/ManaCannon/ManaCannon.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 8 | 10 | 0.8 | 끔 | `Assets/Unit/ManaCannon/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/ManaCannon/BeAttacked.anim` |
| Death | 8 | 8 | 1 | 끔 | `Assets/Unit/ManaCannon/Death.anim` |
| Drained | 1 | 1 | 1 | 켬 | `Assets/Unit/ManaCannon/Drained.anim` |
| Idle | 8 | 8 | 1 | 켬 | `Assets/Unit/ManaCannon/Idle.anim` |
| Run | 8 | 8 | 1 | 켬 | `Assets/Unit/ManaCannon/Run.anim` |

## 뱀파이어 (`Vampire`)

Controller: `Assets/Unit/Vampire/Vampire.controller`

| 상태 | Sprite 키 | Samples | StopTime(초) | Loop | 실제 클립 |
|---|---:|---:|---:|---|---|
| Attack | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Vampire/Attack.anim` |
| BeAttacked | 2 | 12 | 0.166667 | 끔 | `Assets/Unit/Vampire/BeAttacked.anim` |
| Death | 5 | 12 | 0.416667 | 끔 | `Assets/Unit/Vampire/Death.anim` |
| Idle | 2 | 12 | 0.166667 | 켬 | `Assets/Unit/Vampire/Idle.anim` |
| Run | 4 | 12 | 0.333333 | 켬 | `Assets/Unit/Vampire/Run.anim` |

