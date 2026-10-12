# 소환물 표시 배율

2026-10-01 현재 씬·표시 코드 대조. Battle_VSlice의 TileTextView 20개 모두 공통 기준 영역96×96 UI단위, 공통 표시 배율1.35를 사용한다. 최종 프레임 표시 영역은 129.6×129.6 UI단위 × 타일 반응형 배율이다. 화면의 실제 픽셀 크기는 Canvas 배율도 적용된다.

타일 반응형 배율은 min(타일 너비/기준 타일 너비, 타일 높이/기준 타일 높이)이며 카드별 차이가 아니다. 아래 추가 배율은 공통 배율에 곱하는 값이다. 같은 프레임 표시 영역이어도 PNG 안의 그림 크기와 여백에 따라 캐릭터의 실제 크기는 다르게 보일 수 있다.

| 소환물 | 종류 | 카드별 추가 배율 |
| --- | --- | --- |
| A-111 (`A-111`) | 유닛 | 1배 |
| A-212 (`A-212`) | 유닛 | 1배 |
| A-301 (`A-301`) | 유닛 | 1배 |
| 로봇 공장 (`RobotFactory`) | 건물 | 1배 |
| 발전소 (`PowerPlant`) | 건물 | 1배 |
| 원자력 발전소 (`NuclearPowerPlant`) | 건물 | 1배 |
| 블루 드래곤 (`BlueDragon`) | 유닛 | 1배 |
| 케르베로스 (`Cerberus`) | 유닛 | 1배 |
| 엘프 장궁수 (`ElfLongbowScout`) | 유닛 | 1배 |
| 고블린 (`Goblin`) | 유닛 | 1배 |
| 골렘 (`Golem`) | 유닛 | 1배 |
| 스켈레톤 (`Skeleton`) | 유닛 | 1배 |
| 좀비 (`Zombie`) | 유닛 | 1배 |
| 오크 전사 (`OrcWarrior`) | 유닛 | 1배 |
| 웨어울프 (`Werewolf`) | 유닛 | 1배 |
| 마왕 (`DemonKing`) | 유닛 | 1배 |
| 용사 (`Hero`) | 유닛 | 1배 |
| 마나의 샘 (`ManaPond`) | 건물 | 1배 |
| 마나 위버 (`ManaWeaver`) | 유닛 | 1배 |
| 레드 드래곤 (`RedDragon`) | 유닛 | 1배 |
| 뱀파이어 (`Vampire`) | 유닛 | 1배 |
| 개방 분타 (`GaebangBranch`) | 건물 | 1배 |
| 상단 (`MerchantCaravan`) | 건물 | 1배 |
| 객잔 (`Inn`) | 건물 | 1배 |
| 광마 (`Gwangma`) | 유닛 | 1배 |
| 소림 1대 제자 (`Shaolin_1st_Disciple`) | 유닛 | 1배 |
| 금광 채굴꾼 (`GoldMiner`) | 유닛 | 1배 |
| 방패병 (`Shieldbearer`) | 유닛 | 1배 |
| 오크 광전사 (`OrcBerserker`) | 유닛 | 1배 |
| 오크 족장 (`OrcChieftain`) | 유닛 | 1배 |
| 스켈레톤아처 (`SkeletonArcher`) | 유닛 | 1배 |
| 엘프 장로 (`ElfElder`) | 유닛 | 1배 |
| 개방 방주 (`GaebangChief`) | 유닛 | 1배 |
| 마나 대포 (`ManaCannon`) | 유닛 | 1배 |
| 마스터 | 마스터 | 1배 |

- 블루드래곤: Idle/Drained 1배, Attack/BeAttacked/Run/Death 0.7배였으나 모두1배로 통일했다.
- 광마와 나머지 소환물은 기존1배 유지. 등록됐지만 전용 그림이나 모션이 없는 카드도 공통 배율 기준은 같으며, 이 목록은 전용 애니메이션 제작 완료를 의미하지 않는다.
- 등록 카드와 마스터 기준이며 SampleBattle 개발용 샘플은 제외했다. 기존 Assets/Unit 클립에서 별도 scale 곡선은 발견되지 않았다.
- 사용자 편집 PNG, 프레임/클립/컨트롤러, 공통1.35배 설정은 변경하지 않았다. 소스 기본값1.5보다 씬에 저장된1.35가 우선한다.
- 현재 근거는 `Assets/Battle_VSlice.unity`의 20개 `_occupantVisualSize`/`_occupantVisualScaleMultiplier`, `TileTextView.RefreshVisualLayout` 및 BlueDragon/Gwangma의 `BoardScale=1f`다. 이 문서 갱신에서는 화면 캡처를 새로 실행하지 않았다.
- 소환물 표시 배율은 마법 VFX 배율과 다르다. 메테오·뉴클리어·지진·시한폭탄·파이어스피어는 현재 실제 TileBackground 앵커/전장 범위를 사용하며 작은 유닛 Canvas 크기를 전체 VFX 기준으로 쓰지 않는다. 레드드래곤 불꽃은 별도 `RedDragonFireBreath.PlaceFlame`의 거리·표시폭 계산을 따른다.
