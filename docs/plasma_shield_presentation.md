# 플라즈마 쉴드 카드와 보호막 형성 연출

2026-10-01. 사용자 지정 카드 규칙과 기존 영구 능력치 버프·대상 지정 규칙을 사용한다.

`PlasmaShield` / 플라즈마 쉴드는 과학문명 커먼 즉시 마법이며 전력1을 소모한다. 부족한 전력은 기존 골드1 대체 규칙을 따른다. 내·상대 전장의 살아 있는 유닛(마스터 제외) 또는 건물 하나에 물리 방어력 +10을 부여한다. 빈 칸·마스터·사망·봉인·은신한 적은 대상이 아니다. 은신한 아군은 대상이며 이 비피해 버프는 은신을 해제하지 않는다.

2026-10-03 변경: 강화 불가이며 부여 물리 방어력은 +10 고정이다. 덱 빌딩에는 포함하고 랜덤 카드 보상·상점 카드팩에서는 제외한다. 기존 저장 레벨도 새 시전의 부여량을 늘리지 않는다. 반복 시전은 +10씩 합산한다. 턴·횟수 제한 없는 영구 능력치 버프이며 HP·마법 방어력·공격력·공격 횟수·이동은 바꾸지 않는다. 피해가 없고 주문력·마법 증폭진은 증가량에 영향을 주지 않는다.

증가량과 카드 출처는 기존 CombatStatBuffState에 기록한다. 망각은 기존 보너스를 지우고 망각 이후 새로 받은 버프는 정상 작동한다. 방전은 저장된 보너스를 지우지 않지만 방전 중 실효 물리 방어력0은 유지한다. 고독 이전·AI 독립 복사·온라인 관전자별 상태·재접속 저장 복원에서 출처와 증가량을 보존한다. 기존 사망/부활 정리 규칙을 유지한다. 버프 표시는 실제 합산 수치와 플라즈마 쉴드 이름을 사용한다.

## 데이터와 수정 경로

- JSON: `Assets/Project333/Resources/Project333/Data/cards.json`의 PlasmaShield.
- `effectId: plasma_shield`, `effectAmount: 10`, `damage: 0`, `damageType: None`. 피해 필드를 방어력으로 재사용하지 않는다. 새 선택 필드 EffectAmount는 기존 ScriptedSpell/JSON/native 자산에서 누락 시0이며 기존 명시값·GUID를 보존한다.
- 네이티브 카드: `Assets/Project333/ScriptableObjects/StarterTen/Cards/PlasmaShield.asset`. BoardSprite/Animator Controller 없이 마법 카드로 카탈로그에 한 번 등록.
- 카드 그림: `Assets/Project333/Resources/Project333/CardArtwork/PlasmaShield.png`. 사용자 첫 첨부 그림 1672×941 전체를 512×288로 비율 유지하여 512×512 중앙에 배치했다. 그림을 자르거나 등장인물을 전장 유닛으로 만들지 않았다.
- 새 보호막 VFX: `Assets/Project333/Resources/Project333/SpellEffects/PlasmaShield/Formation.png`. 2048×1024, 4×2 배열의 512×512 8컷, 윗줄 왼쪽→오른쪽 다음 아랫줄. 실제 RGBA 투명 배경이며 등장인물 없이 돔만 표시한다. 모든 알파가0이 아닌 픽셀도 셀 여백16px 이상, 최소23px.
- 내장 image_gen 생성 원본: `Tools/Art/Source/PlasmaShield/Formation-Generated-20261001.png`.
- 원본 참고 그림 보존: `Tools/Art/Source/PlasmaShield/PlasmaShield-Reference.png`. 다운로드의 두 원본 파일은 수정하지 않았다.
- 정확한 생성 프롬프트: `Tools/Art/PlasmaShieldPrompts.json`. 첫 구도에서 안전 여백과 내부 투명도를 개선한 두 번째 결과를 채택했다.
- 안전 패킹: `Tools/Art/PackPlasmaShieldSprites.ps1`·`.cs`. 고정 셀 중심과 한 가지 배율을 사용하며 알파를 잘라내거나 새로 그리지 않는다. 사용자 카드 그림은 전체 비율 유지로 별도 처리한다.
- 네이티브 등록: `Tools/Art/ImportPlasmaShieldRegistration.cs`, SpriteEditor API·FullRect·Point·mipmap 없음·무압축·PPU100. VFX는 최초 한 번 RGBA를 읽어 형상 좌표를 구하도록 readable, 카드 그림은 CPU 복사본 없이 가져온다.

그림을 교체할 때 `.meta`를 보존한다. 이후 사용자가 수정한 그림에 최초 원본 패커를 임의로 다시 실행하지 않는다.

## 재생과 입력

원형 바닥 에너지가 올라가 육각형 보호막을 형성하고 잠깐 빛난 후 흩어지는 8컷을12fps로 한 번 재생한다. 0.667초 애니메이션 뒤 0.15초 페이드하여 총 약0.817초 후 완전히 숨긴다. 영구 물리 방어력 보너스를 제거하거나 지속 보호막 오브젝트를 만들지 않는다. 새 효과음·피격음·피격 모션을 생성하지 않는다.

대상 본체 그림의 실제 불투명 영역과 Canvas 좌표를 읽어 유닛·건물에 맞춘다. 생성 시트의 두 줄 바닥 높이 차이는 각 프레임의 밝은 하단 고리를 처음 한 번 측정하여 동일한 대상 발밑에 고정한다. 전체 배율은 유지하고 프레임 경계 중심을 재정렬해 형성 모션을 없애지 않는다. 원본 스프라이트 픽셀은 그대로이다.

성공한 로컬·AI·온라인 시전에서만 재생하며 복원한 스냅샷에는 시전 연출을 새로 만들지 않는다. 게이트웨이가 확정한 방어 버프는 즉시 표시된다. 투명·비입력 overlay를 재사용하고, 기존 busy/온라인 대기 경로로 연출이 끝날 때까지 다음 행동을 대기한다. 런타임 식별자가 달라지거나 사망·숨김·봉인·비활성·전장 교체·종료 시 취소한다.

합법 대상의 기존 흰색 펄스와 단일 대상 hover/클릭/드래그를 연결했다. 마스터·빈 칸·비용 부족 상태는 사용 가능 표시에서 제외한다. 카드 확대 열람과 전투 입력 잠금은 기존 규칙을 유지한다.

## 검증

현재 소스·네이티브 가져오기·관련 Unity 검사 및 오프라인 서버 증거는 `TempBuild/PlasmaShield_20261001`에 저장한다. 앱 빌드/설치·운영 서버 배포/재시작·실기기 전체 온라인 경기와 소스·격리 검증을 구분한다. 이번 작업은 사용자 앱 빌드/설치나 운영 서버 재시작을 수행하지 않는다.


## 최종 검증 기록

- 최종 격리 Unity 6000.3.12f1의 관련 22개 fixture **673/673 통과**, 실패·skip·누락·컴파일 오류 0. 신규 도메인 165, 네이티브 자산 6, 실제 연출 8, 기존 회귀 494개이다.
- 복사한 실제 서버 BattleSession에서 양쪽 시전자·양쪽 대상 소유자·유닛/건물·Lv.0–13 조합 **112회 시전**이 통과했다. 두 관전자 JSON 투영과 재접속 저장 복원, 마스터 거부 시 미차감, 명령/이벤트 진입, 실제 서버 AI의 아군 보호 검사도 통과했다. 서버 격리 컴파일은 오류0, 기존 경고95이며 새 production 경고는 없다.
- 첫 전체 실행은 671/673이었다. 같은 대상 RuntimeId 재표시 직후 시각 유지 assertion은 이미 통과했으나 테스트의 샘플 함수가 시각을 옮긴 뒤 이전 시각과 비교했다. 재표시 시각 유지와 실제 프레임 샘플을 분리했다. AI 테스트는 공격 소진·고가치 아군과 물리 위협 두 명을 사용하여 실제 보호막 선택을 확인했다. 검사 범위·실제 명령 선택 assertion은 유지한다. 마지막에는 사용자 지정 이름 ‘플라즈마 쉴드’로 버프 표시와 기대 문자열을 통일해 동일한 전체 범위를 재검증했다. 실패·초기 네이티브 enum 컴파일 기록도 attempts에 보존한다.
- 네이티브 등록은 46→47, 재실행은 47→47이고 기존 순서·GUID·참조·PNG를 보존한다. 기존 카드53개 내용·순서를 그대로 유지했다. 이 등록 검사 당시 공유 정의54, 네이티브/덱 빌딩47, 강화 가능43, 실제 랜덤 보상38이었다. 현재 전체 집계는 [카드 목록](card_catalog.md)을 따른다.
- 양쪽 소유자의 광마·블루드래곤·A-301·마나연못에 표시한 실제 캡처8장은 Tools/Art/Preview/PlasmaShield에 있다. 투명한 육각 돔, 대상 본체에 맞춘 크기, 발밑 고리의 일관된 위치를 확인했다. 단순 격리 검사 장면이며 전체 Battle_VSlice·실기기·운영 온라인 경기 검증은 아니다.
- 증거: TempBuild/PlasmaShield_20261001의 verification.json, unity-results.xml, unity-summary.json, CoreProofSummary.json, CoreChangedManifest.json, native-import.json, media-review.json, independent-review.json, pre-test-input-hashes.json, post-test-meta-hashes.json, final-input-hashes.json, capture-hashes.json 및 attempts.

이번 변경은 소스·자산·문서이며 앱 빌드/설치와 운영 서버 배포/재시작은 하지 않았다. 온라인 반영에는 사용자의 클라이언트 재빌드와 서버 코드 업데이트가 필요하다.