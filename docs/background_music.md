# After333 배경음악

2026-10-23: 사용자가 제공한 두 음원으로 교체했다. 로비 첨부 파일은 실제로 **MP3**이며 WAV로 변환하거나 확장자만 바꾸지 않고 그대로 가져왔다. 기존 합성 음악의 곡명·측정값·20초 미리듣기는 현재 음악을 설명하지 않는다.

| 용도 | 사용자 원본 | Unity 리소스 파일 |
| --- | --- | --- |
| 게임 시작·드래프트·덱 빌딩·보유 카드·상점·튜토리얼 | After333_Lobby.mp3 | Assets/Project333/Resources/Project333/Audio/BGM/After333_Lobby.mp3 |
| 전투 | BattleBGM2.wav | Assets/Project333/Resources/Project333/Audio/BGM/After333_Battle.wav |

두 파일 모두 첨부 원본과 SHA256이 일치한다. 전투 파일은 기존 리소스 이름을 유지했으며, 로비 파일은 MP3 확장자를 사용한다. 기존 AudioClip GUID를 모두 유지하고 중복되는 로비 WAV는 제거했다. 리소스 키는 확장자를 제외하므로 기존 연결을 그대로 사용한다.

## 재생과 음량

`Project333RuntimeSettings`의 씬 로드 처리가 `After333BackgroundMusic.EnsureForScene`을 호출한다. 씬 파일을 수정하지 않고 하나의 지속 재생 오브젝트를 사용한다.

- `GameStart_VSlice`, `Draft_VSlice`, `DeckBuilding_VSlice`, `OwnedCards_VSlice`, `Shop_VSlice`, `Tutorial_VSlice`: 로비 음악. 메뉴 사이를 이동해도 같은 곡의 재생 위치를 유지한다. 튜토리얼의 전투 실습은 `Battle_VSlice`에서 전투 음악을 사용한다.
- `Battle_VSlice`: 전투 음악. 곡 변경 시 기존 곡은 0.35초 페이드 아웃, 새 곡은 0.65초 페이드 인한다. 게임 시간 정지 중에도 전환한다.
- 기존 설정의 **전체 음량**과 **배경음악 음량**을 유지한다. 기준 음량은 0.48이다. 반복 재생, 중복 플레이어 방지, 비활성화 후 재개와 AudioListener 전환도 유지한다.
- 두 음원 모두 Streaming·Vorbis 품질 0.65·백그라운드 로딩으로 가져오며 스테레오를 유지한다. 실제 앱 용량은 Unity 압축·플랫폼 설정에 따라 달라진다.

효과음 볼륨은 음악에 영향을 주지 않는다. 마스터·배경음악·효과음 세 가지의 관계와 저장 방식은 [오디오 설정](audio_settings.md)을 참고한다.

## 이후 교체 방법

위 두 리소스 파일을 원하는 음원으로 교체하고 기존 `.meta` GUID를 유지한다. 파일 형식을 바꾸면 올바른 확장자로 저장하고 같은 리소스 이름의 옛 파일을 함께 남기지 않는다. Unity 임포트가 끝나면 Play를 다시 시작한다. 새 음악은 새 클라이언트를 빌드·설치해야 휴대폰에 반영된다. 서버 변경은 필요 없다.

`Tools/Audio/GenerateAfter333Music.ps1`, `After333Music.cs`, `InspectAfter333Music.mjs`와 [과거 제작 기록](../Tools/Audio/After333Music.md)은 이전 합성 음악용이다. 현재 음원의 재생성·검증 도구로 실행하지 않는다. 특히 옛 생성기를 현재 Assets 경로에 실행하면 전투 음악을 덮어쓰고 로비 WAV 중복을 만들 수 있다. `docs/audio`의 옛 미리듣기도 현재 곡의 미리듣기가 아니다.

## 검증

- Unity 6000.3.12f1 별도 최소 프로젝트: 기존 `After333BackgroundMusicTests` **3/3 통과**. 실제 MP3/WAV 가져오기 2건과 Play 진입을 포함한 재생 검사 1건이다. 메뉴·튜토리얼 연속성, 전투 전환, 음량/음소거, 일시정지, 중복 방지, 비활성화/재개, 리스너 소유권을 검사했다.
- 활성 프로젝트의 음원·메타·관련 런타임 두 파일·테스트 파일은 검증 복사본과 SHA256이 일치한다. 기존 AudioClip GUID와 임포트 설정 유지도 확인했다.
- 증거: 세션 시각화 폴더 `UserBackgroundMusic_20261023/MusicTests.xml`, `MusicTests.log`, `input_hashes.json`, `verification.json`. 원본 교체 전 파일은 같은 폴더의 `before`에 보관했다.
- 실제 휴대폰 청취나 새 클라이언트 빌드·설치, 서버/DB 작업은 수행하지 않았다. 반복 재생 기능 검사는 새 음원의 반복 경계에 대한 청취 평가와 구분한다.
