# 전투 결과 배너

2026-10-20 구현. 클라이언트 전투 종료 연출이며 승패 판정·결과 저장은 변경하지 않는다.

## 최종 이미지와 문구

- 승리: 사용자가 처음 첨부한 황금 왕관·파란 리본·나팔 디자인을 사용한다. 재설계한 화려한 시안은 사용하지 않는다.
- 패배: 사용자가 이후 첨부한 `패배.png`의 금이 간 왕관·부서진 나무 명판을 사용한다. 빨간 깃발·보석은 없다. 승리 이미지와 글꼴·명판 색감을 맞췄다.
- 이미지에 `승리`·`패배`만 포함하며 느낌표가 없다. 배경을 투명하게 분리했다. 승리/패배 위에 다른 글꼴을 추가로 덮지 않는다.
- 원본 다운로드 파일은 보존하고 프로젝트에 최종 복사본을 넣었다. 경로: `Assets/Project333/Resources/Project333/UI/BattleOutcomes/Victory.png`, `Defeat.png`.
- Single Sprite / FullRect / 원본 알파 / Bilinear / mipmap 없음 / 최대4096. 종횡비를 유지한다. 그림 교체 시 `.meta`의 GUID를 보존하고 같은 파일을 교체한다.

## 동작과 조정 위치

- 기존 씬의 `BattleOutcomeTextView`와 Bootstrapper 참조를 그대로 사용한다. 확정 종료되고 `IsBusy`와 `IsOpponentPlayedCardRevealPending`이 모두 해제된 뒤 표시한다. 마지막 공격·피격·사망 및 접수된 상대 카드 미리보기를 가리지 않는다. 연결 취소나 상태 없음은 패배로 표시하지 않는다.
- `BattleOutcomeBannerView.cs`: 독립 Overlay Canvas, 기준1920×1080, 정렬12000, 모든 요소 입력 비차단. 튜토리얼 대화/선택/스킵32000보다 아래이며 Battle 씬과 함께 제거된다.
- `Resize()`의 `.82f`는 화면 너비 최대82%, `.78f`는 화면 높이 최대78%다. 둘 중 먼저 도달하는 크기로 제한해 원본 그림을 늘이거나 찌그러뜨리지 않는다. 명판이 더 세로로 긴 패배 배너는 와이드 화면에서 높이 제한에 먼저 도달한다.
- `ApplyAnimation()`은 비례 확대와 부드러운 정착(승리), 짧은 낙하/충격 흔들림(패배)을 처리한다. 배경은 약66% 어두워진다. `BattleOutcomeAccentGraphic.cs`의 제한된 UI 메시는 금빛 광선/반짝임 또는 은은한 가라앉는 입자를 표시한다.
- `Time.unscaledDeltaTime`을 사용해 시간 배율0에서도 애니메이션이 진행된다. 같은 결과 재수신은 다시 시작하지 않으며 새 진행 중 전투/비활성화에서 초기화된다.
- 무승부는 별도로 `무승부`를 표시하고 승리/패배 그림을 사용하지 않는다. 서버/기존 formatter의 `Victory`·`Defeat`·`Draw` 식별자는 그대로다.
- `MinimumDisplaySeconds = 3.2f`. `BattleBootstrapper.ReturnToDraftSceneAfterDelay()`는 마지막 연출과 상대 카드 공개 대기열 종료부터 이 값과 Inspector의 기존 지연 중 더 긴 시간만큼 기다린다. [상대 카드 미리보기 누락 점검](opponent_card_reveal.md). 이후 기존 서버 런 동기화·모드별 기록·드래프트 이동을 수행한다. 연출에서 승패/보상을 저장하지 않는다. 튜토리얼의 자동 복귀 비활성화는 유지한다.

## 승리·패배 효과음 (2026-10-24)

- 사용자가 첨부한 `승리.mp3`·`패배.mp3`를 내용 변경 없이 각각 `Assets/Project333/Resources/Project333/Audio/BattleOutcomes/Victory.mp3`, `Defeat.mp3`로 가져왔다. 원본과 SHA256이 일치하며 원래 채널 수를 유지한다. 짧은 효과음용 Decompress On Load·preload·PCM 가져오기를 사용한다.
- `BattleOutcomeBannerView.Show()`의 기존 중복 결과 방지 검사 뒤, 애니메이션을 시작하는 `ApplyAnimation(0f)` 직후 해당 소리를 재생한다. 마지막 공격/사망 표시가 끝날 때까지 기다리는 기존 `BattleOutcomeTextView` 흐름을 따른다. 같은 결과의 LateUpdate/온라인 최종 상태 재수신으로 반복 재생하지 않는다.
- 승리와 패배가 각각 전용 2D AudioSource 하나씩을 재사용한다(최대2개). `After333SoundEffects.RegisterSource()`로 효과음 볼륨에 연결한 뒤 해당 clip을 Play한다. 마스터는 기존 AudioListener를 따르고 배경음악 볼륨은 영향을 주지 않는다. 개별 기준 볼륨은 `_victorySoundVolume` / `_defeatSoundVolume`으로 분리되어 있고 초기값은 둘 다1이다. 배너가 숨겨지거나 비활성화되면 남은 소리를 멈춘다.
- 무승부·잘못된 결과에는 두 효과음을 재생하지 않는다. 다시 진행 중인 전투로 돌아가 Hide된 뒤 새로 승패 연출이 시작되면 새 결과 소리를 재생한다. 표시 상태를 관찰할 뿐 승패 판정/저장/보상은 변경하지 않는다.
- 가져온 승리 길이 약 2.116초, 패배 약 0.784초로 둘 다 기존 최소 표시 3.2초보다 짧다. 자동 복귀 지연을 바꾸지 않았다.
- 독립 Unity6000.3.12f1 검사 **12/12 통과**: 새 음원 가져오기2·실제 Play 재생1, 기존 배너/복귀 타이밍4·승패 formatter5. 실제 clip 매핑, 결과 반복 시 재시작 방지, 하나의 소스 재사용, 배너 숨김/비활성화/무승부에서 정지, 마스터·효과음 볼륨 적용 및 시간 배율0에서 시작을 확인했다. 활성 소스/음원은 검증 복사본과 SHA256이 일치한다.
- 증거: 세션 시각화 `OutcomeAudio_20261023/OutcomeTests.xml`, `OutcomeTests.log`, `input_hashes.json`, `verification.json`, `task.patch`. 작업 준비 폴더 날짜는 시작일이며 반영 완료는 2026-10-24다. 앞선 적용 시도는 승인 검토의 사용량 제한으로 실행 전 중단됐고, 재개 요청 뒤 정상 승인된 적용과 검증을 완료했다.
- 실제 휴대폰 청취·클라이언트 빌드/설치·서버 재배포/DB 작업은 수행하지 않았다. 새 클라이언트를 빌드·설치하면 반영된다.

## 검증 범위

별도 Unity6000.3.12f1 프로젝트에서 전체 Runtime을 컴파일하고 EditMode11개(기존 formatter5·새 통합/타이밍4·실제 Battle 씬 표시2), PlayMode1개가 통과했다. 1920×1080·2340×1080에서 승리 등장/정착, 패배 충격/정착 총8장을 렌더링해 크기·투명 배경·문구를 확인했다. 비활성화/새 전투/무승부/중복 종료 상태/입력 비차단과 실제 일시정지 중 프레임 진행을 검증했다.

증거는 세션 작업 폴더 `BattleOutcomeBanners_20261020/verification.json`, `EditMode4.xml`, `PlayMode.xml`, `captures/`에 있다. 최초 검증의 TMP 셰이더 누락 및 동기식 캡처 크기 설정 실패는 격리 검증 준비를 보완한 뒤 재검사했다. 앱 빌드·설치·휴대폰 실측·운영 전투/서버 배포·DB 작업은 이번 검증에 포함하지 않는다. 사용자의 다음 클라이언트 빌드에 반영된다.


## Editable UI before Play (2026-10-23)

The shared editor coordinator now prepares the current saved UI before Play, preserving existing scene objects and attached button components. See [editable_scene_layouts.md](editable_scene_layouts.md) for the common menu, dirty-scene saving policy and the distinction between editable UI templates and live account/battle data.

## 승리·패배 볼륨을 따로 수정하기 (2026-10-24 후속)

`Assets/Project333/Runtime/Presentation/Battle/BattleOutcomeBannerView.cs` 상단에서 각각 수정한다. 아래는 **승리50%, 패배100% 예시**이며 실제 초기값은 둘 다1이다.

```csharp
[SerializeField, Range(0f, 1f)] private float _victorySoundVolume = 0.5f;
[SerializeField, Range(0f, 1f)] private float _defeatSoundVolume = 1f;
```

승리만 조절하려면 `_victorySoundVolume`만 바꾼다. 0은 음소거,1은100%다. 배너는 실행 중 생성되므로 코드 저장 후 Play를 다시 시작하고, 휴대폰은 새 앱을 빌드·설치한다. 공통 효과음·마스터 볼륨도 각각 곱해진다. 전용 소스의 원래 볼륨은 생성 시 한 번 등록하므로 서로 전환해도 효과음 설정이 초기화되거나 두 번 곱해지지 않는다. 한쪽을0으로 해도 다른 쪽에는 영향이 없다.

실제 Play를 포함한 격리 회귀24/24 통과: 승리0.4·패배0.8 및 공통 효과음0.25/0.5에서 각 source 볼륨, 승리만 음소거, cue별 소스 재사용·이전 소리 정지·동일 결과 중복 억제·비활성화/일시정지와 기존 설정/BGM/배너 검증. 첫 실행의 기존 중복 재생 검사는 Play 직후 같은 프레임 seek 가정을 드러냈으므로 실제 timeSamples 진행을 기다린 뒤 끝부분 seek와 중복 억제를 검사하도록 보완했다. 첫23/24 후 최종24/24. 새 음원·씬·글로벌 오디오 헬퍼·계정·서버·DB 변경 없음. 증거: 세션 시각화 OutcomeSeparateVolume_20261024/{Checks.xml,Final.xml,Final.log,verification.json,before}.
