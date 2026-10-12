# A-301 애니메이션·레이저 연출

2026-09-30 제작. 기존의 흰색·회색 헬리콥터형 로봇, 짙은 하부 몸체, 오른쪽 푸른 광학 렌즈와 청록색 발사구를 유지하면서 Idle·Attack·Death를 새로 제작했다. 기존 BeAttacked·Disabled, 카드 능력치·특수효과·원거리 공격 판정과 게임 표시 배율은 보존한다.

## 현재 클립과 연결 — 2026-10-01

아래 현재 표는 2026-10-01 `.anim`의 스프라이트 키 수·Samples·StopTime·Loop Time 및 카드/컨트롤러 참조를 읽어 확인한 값이다. PNG 픽셀·모든 게임 모션을 새로 실행 검증한 기록은 아니다. 이전 제작 표와 검사 결과는 당시 기록으로 유지하며, 현재 Assets 편집본이 재생 기준이다.

| 모션 | 현재 키 수 / Samples / 길이 | Loop Time |
|---|---|---|
| Idle | 6 / 12fps / 0.5초 | 켬 |
| Attack | 6 / 12fps / 0.5초 | 켬 |
| BeAttacked | 2 / 12fps / 약0.167초 | 켬 |
| Death | 8 / 12fps / 약0.667초 | 켬 |
| Disabled | 8 / 12fps / 약0.667초 | 켬 |

`A-301.asset`의 현재 Controller GUID는 계속 `46d9d6ef07bae8047b36b48d3ac244b6`이며 위 다섯 상태를 연결한다. Run 상태는 없다. 발사구→대상 직선 레이저의 코드상 명중 시점은 계속 Attack 시작 0.20초이며 피해는 공유 판정 결과만 표시한다. 최초 8컷/0.8~1초 제작 값과 다른 현재 수동 편집본을 옛 제작 도구로 되돌리지 않는다.

## 최초 제작 모션과 편집 파일 — 2026-09-30 기록

| 모션 | 동작 | 컷 수 | 규격 | 속도·반복 |
| --- | --- | ---: | --- | --- |
| Idle | 작은 호버 움직임과 서로 다른 로터 회전 각도 | 8 | 각 512×512px | 8fps, 1초, 반복 켬 |
| Attack | 조준 → 발사구 충전 → 발사·작은 반동 → 안정화 | 8 | 각 512×512px | 10fps, 0.8초, 반복 끔 |
| Death | 렌즈·로터 정지 → 기울어져 추락 → 찌그러진 잔해로 정지 | 8 | 각 512×512px | 8fps, 1초, 반복 끔 |

실제 게임 편집 대상은 `Assets/Unit/A-301/{Idle,Attack,Death}.png`다. 각 시트는 가로 한 줄 **4096×512px**이고 로터·몸체·연기·잔해가 각자의 512px 셀 안에 들어간다. 세 모션 모두 공통 배율로 포장했으며, 원래 크기의 기준인 첫 Idle 약 295px 높이를 사용했다. 게임의 유닛 표시 배율을 별도로 키우지 않았다.

하단 기본 여백은 **16px**다. 의도한 호버 움직임을 보존하여 Idle은 16–24px, Attack은 16–20px이며 Death는 추락 중 16–32px에서 접지한 잔해 컷의 16px로 이어진다. 모든 컷을 개별적으로 다시 확대하거나 매번 아래쪽으로 붙여 호버·추락 동작을 없애지 않았다. 최대 포즈는 셀 안에 들어가며 넘친 부분을 단순히 잘라내지 않았다.

재생 연결은 `PNG → 해당 .anim → A-301.controller → A-301.asset의 Board Animator Controller`다. 기존 컨트롤러 GUID는 `46d9d6ef07bae8047b36b48d3ac244b6`이다. Unity 임포트 단계에서 기존 PNG·클립 GUID와 유지되는 Sprite 이름의 fileID, 컨트롤러·카드·BeAttacked·Disabled·기존 .aseprite 파일의 보존을 확인했다. 별도 Resources 캐릭터 시트로 Animator Controller를 우회하지 않는다.

같은 컷 수로 그림을 교체하려면 위 PNG를 편집하고 투명 배경·512px 격자·약 16px 기본 여백을 유지한다. `.meta`를 삭제하지 않는다. 컷 수를 바꾸면 Unity Sprite Editor에서 다시 분할하고 `.anim` 키프레임도 수정한다. 기존 `.aseprite`는 제작 전 원본으로 보존한 파일이므로, 새 그림을 편집할 때는 현재 PNG를 기준으로 새 작업 파일을 저장한다. 일회성 제작·임포트 도구를 나중에 수동 편집한 게임 그림 위에 다시 실행하지 않는다.

## VFX와 효과음

- 레이저 VFX는 `Assets/Project333/Runtime/Presentation/Battle/A301LaserAttack.cs`에서 UI 도형으로 그린다. 별도 파이어브레스 같은 VFX PNG가 없으며 붉은 광선·가느다란 흰 중심선·약한 외곽광·청록 발사광·짧은 대상 타격광으로 구성된다. 임의의 대상 방향에 맞춰 발사구와 실제 피격점을 직선으로 연결하므로 캐릭터 Attack PNG에 긴 가로 광선을 포함하지 않는다.
- 발사구 좌표와 타이밍은 같은 폴더의 `A301PresentationAssets.cs`가 제공한다. 발사구 좌표는 발사 컷 2·3의 실제 발사구 중심을 측정한 `(349.08/512, 70.56/512)`이며 Unity의 아래쪽을 원점으로 계산한다. 캐릭터의 표시 방향과 UI 위치에 맞춰 좌표도 변환된다.
- 대상 타격광은 다른 유닛 VFX와 같은 공통 하향 피격 위치인 `TileTextView.GetVisualImpactWorldPosition()`에 나타난다. 발사구와 대상의 표시 위치를 프레임마다 따라가며 클릭을 가로막지 않는다.
- 발사음은 `Assets/Project333/Resources/Project333/Units/A-301/Audio/LaserShot.wav`, Resources 키는 `Project333/Units/A-301/Audio/LaserShot`이다. **0.260초 mono PCM16 / 44,100Hz**이며 둥근 하강 chirp와 아주 약한 공기감으로 제작했다. 16ms 어택·50ms 릴리즈, peak **-14.000dBFS**·RMS **-25.011dBFS**, 발사 시 gain **0.30**으로 한 번 재생한다. 사용자 피드백을 반영한 A-212의 낮춘 음량과 비슷한 수준이다.
- 피격음은 기존 공통 `Assets/Project333/Resources/Project333/Audio/Common/UnitHit.wav`다. A-301 전용 피격·이동·사망음은 추가하지 않았다. 파일 측정만으로 실제 청취의 편안함을 확인했다고 주장하지 않는다.

## 전투 타이밍과 판정

A-301은 원거리 유닛이므로 자기 타일에서 Attack을 재생하고 접근 이동이나 원거리 공격의 몸체 전진을 하지 않는다. Attack 시작 **0.20초** 뒤 직선 레이저·기록된 피해 숫자·공통 UnitHit를 표시한다. 광선은 **0.20초**, 대상 타격광은 **0.24초** 유지되며 발사음은 **0.260초** 자연 종료한다. 현재 연결한 Attack 클립 길이는 **0.5초**다. `PlayLaserAttackExchange`는 현재 클립 길이와 준비+광선 시간 중 긴 값을 기준으로, 발사 후 남은 Attack 시간·피격 반응·타격광·효과음 중 가장 긴 시간을 기다린다. 따라서 피격 반응이 더 길면 복귀도 늦어지며, 최초 제작의 0.8초를 고정 대기하지 않는다. 사망한 유닛은 현재 연결한 Death 클립 길이(현재 약0.667초)와 공통 사망 유지 시간 중 긴 값을 기다린다.

피해 수치·타격 횟수·반격 자격·사망 판정은 기존 전투 상태/로그에서 받으며 연출 코드가 체력이나 공격 횟수를 변경하지 않는다. 레이저라는 외형 때문에 카드의 물리 공격을 마법 공격으로 바꾸지 않았다. 여러 피해 기록이 있으면 각각 표시하며 화면 표시를 위해 합산하거나 피해 판정을 새로 계산하지 않는다. A-301의 원거리 공격·반격 규칙은 기존 그대로다.

취소·씬 비활성화·공격자/대상 교체·비활성화에서는 레이저와 발사음을 정리한다. 주 대상뿐 아니라 쉴더 초과 피해 같은 보조 대상의 RuntimeId도 공격 시작에 캡처하여, 충전 중 또는 사망 연출 전 새 소환물로 바뀌면 이전 공격의 반응·사망을 적용하지 않는다. 바뀌지 않은 유닛만 기본 상태로 복구한다.

구현 파일은 `A301PresentationAssets.cs`, `A301LaserAttack.cs`, `BattleBootstrapper.cs`다(모두 `Assets/Project333/Runtime/Presentation/Battle`).

## 제작·보관과 파일 검증

- 생성 원본과 참고 이미지: `Tools/Art/Source/A-301/`, 프롬프트: `Tools/Art/A301Prompts.json`. 기존 디자인을 참조하여 built-in image_gen으로 투명 이미지 원본을 제작하고 별도 도구로 정리했다. Attack의 긴 내장 가로 광선은 추가 이미지 편집으로 제거했다.
- 시트 정리: `Tools/Art/PackA301Sprites.cs`, `PackA301Sprites.ps1`. 기본 출력은 Assets 밖의 `TempBuild/A301SpriteExport`이며 Assets 내 출력은 거부한다. 생성 원본의 포즈를 공통 배율로 512px 셀에 배치하고 작은 호버/추락 위치 변화를 유지한다.
- Unity 임포트: `Tools/Art/ImportA301Presentation.cs`. 별도 Editor에서 Sprite Data Provider capability를 먼저 확인한 뒤 분할·클립·텍스처·오디오 설정을 갱신했다. Sprite `.meta`를 직접 작성하지 않는다. 레이저 WAV는 PCM / Decompress On Load / Preserve Sample Rate / preload enabled로 가져왔다.
- 레이저 음원 제작·독립 검사: `Tools/Art/PrepareA301LaserAudio.py`, `ValidateA301LaserAudio.ps1`, [음원 제작 기록](../Tools/Art/A301LaserAudio.md). Python exporter는 Assets 밖 `TempBuild/A301LaserAudioExport`로 기본 출력하고 Assets 내 출력은 거부하므로 현재 음원을 자동 덮어쓰지 않는다.
- 음원을 교체할 때는 검토한 `LaserShot.wav`만 활성 경로에 복사하고 기존 `.meta`를 보존한다. Play Mode를 다시 시작하여 캐시된 클립을 갱신한다.
- 교체 전 백업: `Tools/Art/Archive/A301Before_20260930.zip`.
- 포장 미리보기: `Tools/Art/Preview/A301ContactSheet.png`, 실제 Unity 레이저 렌더: `Tools/Art/Preview/A301LaserPreview.png`. 검증용 빈 씬의 캡처이며 전체 전투씬 캡처는 아니다.
- 임포트·보존·재현 증거: `TempBuild/A301Presentation_20260930/`의 `import-result.json`, `audio-import-result.json`, `preserved-assets.json`, `applied-assets.json`, `reproduction-validation.json`과 `Packed/packing-report.json`.
- 오디오 검증 증거: 같은 폴더의 `Audio/{Prepared,Reproduced}`, `Audio/A301LaserAudioIndependentValidation.json`, `Audio/ExportSafetyValidation.json` 및 Tools/Art의 두 오디오 검사 기록.

Unity 임포트 결과에서 3개 모션의 8개 512px Sprite 참조·클립 길이·기존 연결 보존과 .260초 mono44100 오디오 가져오기를 확인했다. PNG 재현 출력은 포장본과 해시가 같다. 별도 RIFF/PCM 검사에서 WAV 헤더·클리핑 0·시작/끝 샘플 0·페이드 경계·결정적 재현 해시·활성 파일 일치를 확인했다. Assets 내 오디오 출력 거부와 거부 후 활성 파일 보존도 통과했다.

## 최종 Unity 재생 검사 — 2026-09-30

별도 Unity **6000.3.12f1**의 기존 `TempBuild/OwnedCardsPresentation_20260926`에서 **21/21 검사 통과**했다. A-301 9개와 A-212·블루드래곤·광마·레드드래곤·컨트롤러 변경의 기존 회귀 검사 12개다. 실제 Animator Attack→Idle, 양방향 발사구와 공통 피격점, 직선광선, 발사 시점의 피해 숫자·공통 UnitHit, Death 전체 유지, 취소·대상 교체·비활성화와 보조 대상 교체 경계를 확인했다. 연출 전후 실제 체력이 바뀌지 않고 자기 타일에서 발사함을 검사했다.

화면 안에서 이동하는 대상에 대한 렌더 캡처를 개선한 뒤 해당 직선 레이저 추적 검사만 다시 실행하여 **1/1 통과**했다. 레이저 양방향 캡처에서 발사구·연결선·타격광 위치를 확인했다. 이 재실행은 테스트의 카메라 내 이동량만 바꿨으며 게임 소스나 에셋 변경은 없다.

전체 런타임·검사 소스 컴파일도 통과했다. 실제 검사 입력 755개가 활성 파일과 해시 일치하며 원본 카드·컨트롤러·보호 모션·.aseprite 등 29개 파일은 제작 전과 동일하다. 검증 프로젝트에서 원본 없이 제거된 .aseprite 메타데이터 4개는 재생 입력에서 제외하고 활성 원본 보존을 별도 확인했다. 해당 파일들은 실제 게임에서 수정하지 않았다.

증거: `TempBuild/A301Presentation_20260930/{unity-results.xml,unity-tests.log,capture-results.xml,capture-test.log,tested-file-hashes.json,final-preservation-validation.json,Captures/}`. 그림·소리의 결정적 재현과 Unity API 임포트 결과도 같은 증거 폴더에 보관한다.

검증용 프로젝트에 외부 광고 SDK가 없으므로 이 프로젝트에서만 별도 LevelPlay 어댑터를 제외했다. 활성 프로젝트의 광고 코드·패키지는 변경하지 않았다. 기존 격리 환경의 TMP 피해 팝업 재질은 일부 캡처에서 분홍색으로 표시되어 숫자는 생성된 TMP_Text의 값·개수로 확인했다. 모션·레이저 렌더는 확인했으나 전체 전투씬 글꼴·재질 검수로 해석하지 않는다. 사용자의 주 Unity 편집기는 종료하거나 다시 열지 않았다.

실제 전체 전투씬과 휴대폰에서의 화면·청취 검증, 클라이언트 빌드·설치는 별도다. 이 작업에서 서버 변경·배포·앱 빌드·설치·커밋·푸시는 수행하지 않는다. 설치된 휴대폰 앱에 반영하려면 사용자가 최신 클라이언트를 빌드·설치해야 한다.
