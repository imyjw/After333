# 마나쉴드 — 2026-10-01

## 확정 규칙

ManaShield / 마나쉴드는 판타지 커먼 마법이며 마나1을 사용한다. 부족한 마나는 기존 규칙대로 골드로 대체한다. 내·상대 전장의 살아 있는 유닛(마스터 제외) 또는 건물 하나에 마법 방어력+10을 즉시 영구 부여한다. 턴 제한·발동 횟수 제한이 없고 반복 사용은 중첩한다. 2026-10-03 변경: 강화 불가이며 부여량은 +10 고정이다. 덱 빌딩에 포함하고 랜덤 카드 보상·상점 카드팩에서는 제외한다. 기존 저장 레벨도 새 시전의 부여량을 늘리지 않는다.

기존 PlasmaShield와 같은 대상 검증을 사용한다. 빈 칸·사망·봉인·마스터·은신한 적은 직접 지정하지 못하며 은신한 아군은 가능하다. 버프만으로 은신을 해제하지 않는다. 물리 방어력·공격력·HP·이동·액티브를 변경하지 않고 피해도 없다. 주문력·마법 증폭진은 부여량에 영향을 주지 않는다. 시전 실패에는 비용·손패·대상을 바꾸지 않고 성공 시 비용과 선택 카드를 한 번만 소비하여 즉시 버린다.

CombatStatBuffState의 SourceCardId=ManaShield 및 MagicDefenseBonus를 재사용한다. 플라즈마 쉴드의 물리 방어 버프와 독립적으로 중첩한다. 방전 중 실효 방어력0은 유지하되 저장된 보너스는 보존한다. 망각은 기존 버프를 지우고 망각 이후 새 시전은 정상 적용한다. 고독 소유권 이전·AI 복사·양쪽 관전자 JSON·재접속 저장 복원은 출처와 실제 증가량을 보존하며 사망/부활은 기존 정리 규칙을 따른다.

AI는 마나쉴드 출처의 추가 방어가 있는 대상에만, 현재 공개된 마법 일반 공격의 실제 예방 피해를 평가한다. 현재 공격자의 주문력과 마나 대포 공격 비용·유효 타겟·무적 등 기존 공통 규칙을 사용하며 물리·고정 피해의 예방 가치는 만들지 않는다. 적에게 부여하는 이득은 상대의 보호 가치 증가로 처리한다. 이미 방어력이 충분한 무효 중첩은 추가 가치를 얻지 않는다. 기존 플라즈마 쉴드 평가 본문과 다른 카드 점수를 보존한다.

## 자산과 수정 경로

- 공유 데이터: Assets/Project333/Resources/Project333/Data/cards.json의 ManaShield. effectId=mana_shield, effectAmount=10, damage=0, damageType=None. 기존 optional EffectAmount를 사용하고 별도 schema를 추가하지 않는다.
- 네이티브 카드: Assets/Project333/ScriptableObjects/StarterTen/Cards/ManaShield.asset. 마법이라 BoardSprite/Animator Controller를 연결하지 않는다.
- 카드 그림: Assets/Project333/Resources/Project333/CardArtwork/ManaShield.png. 완성 보호막의 zero-based frame4 전체512×512 RGBA를 사용한다.
- 애니메이션: Assets/Project333/Resources/Project333/SpellEffects/ManaShield/Formation.png. 2048×1024, 4×2 배열의512×512 8컷, 위 행부터 왼쪽→오른쪽. 실제 투명 RGBA 배경이며 대상 캐릭터를 그림에 포함하지 않는다.
- 내장 image_gen 최종 생성 원본: Tools/Art/Source/ManaShield/Formation-Generated-20261001.png. 원형 마나 구체의 연속 표면·빛 반사와 형성/소멸을 제작했다. 육각형·패널·격자 무늬가 없다.
- 최종 편집 참고 원본: Tools/Art/Source/ManaShield/Formation-Edit-Reference-20261001.png. 채택 전 참고 입력을 보존한다.
- 정확한 프롬프트와 선택 기록: Tools/Art/ManaShieldPrompts.json. 생성3회 뒤 네 번째 편집 결과를 채택했다.
- 패킹: Tools/Art/PackManaShieldSprites.ps1 및 .cs. 고정 셀 중심·공통 배율로 모든 RGBA를 보존하고 효과를 잘라내지 않는다. 모든 alpha>0 픽셀의 셀 여백은 최소23px이며 native importer도16px 안전 여백을 확인한다.
- 등록: Tools/Art/ImportManaShieldRegistration.cs, StageManaShieldRegistration.ps1, ExportManaShieldNativeInputs.ps1. FullRect·Point·PPU100·mipmap 없음·무압축, VFX만 최초 경계 측정을 위해 readable이다. 네이티브 카드 등록은 기존 순서·참조·GUID를 보존해 한 번 추가한다.

이미지를 교체할 때 기존 .meta와 스프라이트 이름/개수를 보존한다. 사용자가 변경한 그림을 이전 생성 원본으로 임의로 덮어쓰지 않는다.

## 재생과 입력

투명한 푸른 마나 구체가 형성·빛남·소멸하는8컷을12fps로 한 번 재생하고, .15초 fade 뒤 완전히 숨긴다. 총 길이는 약 .817초이다. 표시가 사라져도 방어 버프는 남는다. 지속 보호막·피격음·새 효과음·HP 팝업을 만들지 않는다.

본체의 실제 alpha 경계에 맞춰 구체 중심을 배치한다. 완성 frame4의 핵심 구체 경계를 최초 한 번 읽어 빛 번짐을 제외한 반지름을 구한다. 몸체 사각형의 반대각선을 감싸도록 동일한 X/Y 배율로8컷을 재생하므로 긴 유닛이나 넓은 건물에서도 원이 찌그러지지 않는다. 셀별 포즈를 재중앙화하거나 원본 pixels를 수정하지 않는다.

Image opacity는 시작 .14, 최대 .28로 표시한다. 완성 프레임의 내부 source alpha가 높아도 화면에 합성되는 opacity는 .28 이하이며 몸체가 비친다. 구체 모양과 투명도는 실제 대상 렌더 캡처로 확인한다.

로컬·AI·온라인의 확정된 성공 이벤트에서 실제 대상 RuntimeId를 사용해 재생한다. 스냅샷 복원은 시전 연출을 다시 만들지 않는다. authoritative 버프는 즉시 반영하고 실제 연출 종료까지 busy/온라인 메시지를 대기한다. 런타임 식별자 교체·사망·적 은신·봉인·비활성·전장 종료에는 취소한다. 비입력 overlay는 재사용하며 타일 pulse/hover/클릭/드래그는 합법 대상만 표시한다. 마스터·빈 칸·비용 부족은 제외하고 기존 카드 확대 열람 기능은 유지한다.

## 검증

현재 소스·네이티브 가져오기·관련 Unity 검사·복사한 실제 서버의 증거는 TempBuild/ManaShield_20261001에 저장한다. 격리 검증과 전체 Battle_VSlice·실기기·운영 온라인 경기를 구분한다. 이번 작업은 소스·자산·문서 추가이며 사용자 앱 빌드/설치·운영 서버 배포/재시작은 수행하지 않는다.


## 최종 검증 기록

- 격리 Unity6000.3.12f1의 관련25개 fixture **856/856 통과**, 실패·skip·누락·컴파일 오류0. 신규 도메인169·네이티브6·실제 연출8 및 기존 플라즈마 쉴드/전투/온라인 회귀 검사를 포함한다.
- 복사한 실제 서버 BattleSession112회 시전(양쪽 시전자·양쪽 대상·유닛/건물·Lv.0–13), 양쪽 JSON/관전자 투영·영구 저장 복원·마스터 거부 미차감·명령/이벤트 진입·실제 AI 선택 검사 통과. 서버 격리 컴파일은 오류0·기존 경고95, 새 production 경고는 없다.
- 등록47→48·재실행48→48, 기존47개 네이티브 참조 순서·GUID·그림과 기존 공유 카드54개 내용·순서를 유지한다. 이 등록 검사 당시55shared/48native/48draft/44upgradeable/39actual reward, 유닛27/건물7/마법21, 판타지28/커먼23이었다. 현재 전체 집계는 [카드 목록](card_catalog.md)을 따른다.
- alpha>0 전체 픽셀의 최소 여백23px, 완성 frame4와 카드 그림 전체 RGBA 동일, 기존 아트/자산 보호 및 실제 검사본과 현재 소스/자산 hash 일치를 확인했다. 약한 alpha haze를 잘라내거나 배경으로 간주하지 않는다. 원본의 거의 불투명한 중앙도 화면 opacity .28 이하로 합성해 몸체가 보인다.
- 양쪽 소유자의 광마·블루드래곤·A-301·마나연못 렌더 캡처8장을 Tools/Art/Preview/ManaShield에 보존한다. 단순 격리 검사 장면의 렌더링이며 전체 Battle_VSlice·실기기·운영 온라인 경기 검증은 아니다.
- 증거 TempBuild/ManaShield_20261001: verification.json, unity-results.xml/summary/log, CoreChangedManifest/CoreProofSummary, native-import, media-review, independent-review, pre-test-input-hashes, post-test-meta-hashes, final-input-hashes, capture-hashes 및 재현 스크립트. 초기 시각/배경 검사 기록과 재시도는 존재할 경우 attempts에 보존한다.

온라인 반영에는 클라이언트 재빌드와 서버 코드 업데이트가 필요하다. 사용자 앱 빌드/설치나 운영 서버 배포/재시작을 수행하지 않았다.