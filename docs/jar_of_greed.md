# 욕망의 단지 카드

## 욕망의 단지 (2026-10-28)

| 항목 | 확정값 |
|---|---|
| CardId / 이름 | `JarOfGreed` / 욕망의 단지 |
| 종류 / 소속 / 희귀도 | 마법(`ScriptedSpell`) / 중립 / 레어 |
| 비용 | 전투 골드 1 |
| 효과 | 사용 즉시 카드 2장 드로우 |
| effectId / effectAmount | `jar_of_greed` / 2 |
| 덱 빌딩 / 랜덤 보상 / 강화 | **On / Off / 불가** |
| 카드 이미지 | `Assets/Project333/Resources/Project333/CardArtwork/JarOfGreed.png` |

첫 요청에서는 덱 빌딩 제외였으나 같은 날짜 후속 요청으로 **덱 빌딩 포함**으로 변경했다. 랜덤 보상 제외와 강화 불가는 유지한다. AI 고정 덱에는 자동 추가하지 않는다.

### 사용·드로우 규칙

- 손패에서 위로 드래그해 허공에 놓아 사용한다. 대상 타일/유닛을 지정하지 않는다.
- 자기 Main 단계·실제 손패 RuntimeId·골드1을 검사한다. 성공하면 골드와 선택한 사본만 소비해 버림 더미로 옮긴 뒤 공통 `BattleDrawService`로 2번 드로우한다. 다른 동명 사본은 유지한다.
- 손패 제한을 넘는 카드는 덱에서 빠지고 소멸한다. 사용 카드가 먼저 손패에서 제거되므로 그 빈 슬롯부터 활용한다.
- 덱이 비면 기존 누적 덱 소진 고정 피해(실패 횟수×3)를 드로우 시도마다 적용한다. 도중 마스터가 쓰러져 전투가 끝나면 나머지 드로우는 중단한다.
- 실제 손패에 들어온 카드마다 출처 `JarOfGreed`의 드로우 이벤트를 남긴다. 기존 로컬/온라인 드로우 연출·로그·시청자별 상태 전송 경로를 사용하며 상대에게 새 손패 카드 정체를 공개하지 않는다.
- 사용 확정 시 놓은 자리에서 기존 공통 푸른 조각 분해 연출(0.145초)을 재생한다. 드로우 연출과 구별하며, 명령이 거절되면 분해하지 않고 원래 표시를 복구한다.

### 이미지·생성·공통 연출 확장

- 이미지 바이트는 수정하지 않았고, 기존 카드와 같은 Single Sprite / Full Rect / NPOT 유지 / mipmap off 설정을 메타데이터에 적용했다. `CardArtworkLibrary`가 CardId와 같은 Resources 경로로 로드하므로 별도 이미지 허용 목록은 없다.
- 네이티브 `JarOfGreed.asset`과 `StarterTenCardCatalog` 참조를 추가했다. 전용 `Tools > Project333 > Cards > Create or Update Jar Of Greed` 메뉴는 기존 자산/GUID를 유지하며 JSON 정의를 다시 연결한다.
- `SpellCardUseRules.IsUntargeted`를 입력 허용과 실제 시전 경로가 함께 사용한다. 새 대상 없는 ScriptedSpell은 별도의 연출 카드 허용 목록 없이 공통 캡처·승인·분해 경로를 사용한다. 타일 대상 효과는 `RequiresBoardTarget`에 명시하고 로봇 합체는 별도 재료 선택 흐름을 유지한다. 효과 자체의 서버/공유 서비스 구현은 여전히 필요하다.

### 검증·반영 범위

- 실제 서버 빌드 DLL에 연결한 공유 효과/메타데이터/실제 서버 보상 필터 검사 **18/18 통과**. 양쪽 플레이어, 정확한 손패 사본, 비용 부족·잘못된 턴/타일/RuntimeId, 손패 초과, 덱 소진/도중 사망, 재사용 거절, 새 대상 없는 효과 자동 분류를 확인했다.
- 서버 `.NET8` 컴파일 오류0·경고101. 실제 Unity 런타임 소스는 설치된 엔진/UI/TMP/Newtonsoft 참조로 별도 컴파일 오류0·경고88(직렬화 필드 미할당 등), 별도 LevelPlay asmdef는 제외해 광고 SDK 검증으로 확대하지 않는다.
- 새 생성 도구·Unity 전용 카드 테스트·기존 공통 스펠 사용 연출 테스트 소스 별도 컴파일 오류0·경고0. 기존 연출 통합 검사에 JarOfGreed를 추가했지만 이 실행에서는 Play 모드로 통과했다고 주장하지 않는다.
- 분리 Unity 실행은 최초 라이선스 초기화 실패, 권한 조정 후 초기 어셈블리 로드에서 정체되어 해당 작업 프로세스만 종료했다. Unity 네이티브 import/AssetDatabase 실행·Play 모드 연출 검증은 완료하지 못했다. 신규 카드/메타는 기존 직렬화 형식에 맞춰 작성하고 필드·GUID·참조를 정적 확인했다.
- 증거: 세션 `JarOfGreed_20261028`의 `server-shared-tests.json`, `server-build.log`, `runtime-compile.log`, `editor-compile.log`, `native-registration.json`, `verification.json`. 실패 시도 로그도 보존한다.
- 사용자 Unity에서 import/Play 확인과 클라이언트 재빌드·설치가 필요하다. 운영 온라인 전투에는 서버 코드와 함께 publish된 `Data/cards.json`도 재배포해야 한다. 이 작업에서 운영 서버/DB·앱 빌드·커밋/push는 실행하지 않았다. DB 마이그레이션은 필요하지 않다.
