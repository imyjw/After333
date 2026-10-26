# 개방 방주 제작과 편집

## 현재 연결 메타데이터 — 2026-10-20

현재 카드가 참조하는 Controller/clip을 정적으로 다시 읽은 값이다. 아래10월1일 등 날짜별 제작·검증 값은 당시 기록이며 프레임 픽셀/전체 모션을 오늘 재검증한 것으로 해석하지 않는다. [전체 유닛 현재 값](unit_animation_snapshot.md).

| 상태 | Sprite 키 / Samples / StopTime | Loop |
|---|---|---|
| Attack | 8 / 12fps / 0.666667초 | 끔 |
| BeAttacked | 1 / 12fps / 0.0833333초 | 끔 |
| Death | 8 / 8fps / 1초 | 끔 |
| Drained | 1 / 1fps / 1초 | 켬 |
| Idle | 4 / 12fps / 0.333333초 | 켬 |
| Run | 8 / 12fps / 0.666667초 | 켬 |

2026-10-01. 사용자 원본의 낡은 삿갓·긴 회색 머리와 수염을 유지하고, 공격 때 눈을 금화로 바꿔 평상시·주먹 공격·이동·사망 4종 모션과 금화 명중 VFX, 짧고 조용한 공격음 1개를 제작·연결했다.

카드 규칙은 [전체 상세](card_details.md#card-gaebangchief), [전투 규칙](battle_rules.md), [강화 규칙](meta_rules.md)을 따른다. `GaebangChief`는 무림 유니크 인간 유닛, 기 7, 기본 ATK/HP 10/60이며 Lv.13 카드 표시 수치는 14/69다. 현재 소유자의 전투 골드 1당 ATK +10을 즉시 계산하고 골드를 소모하지 않는다. 사망·방전·망각·봉인 중 보너스는 0, 고독 이전 후에는 새 소유자의 골드를 사용한다. 연출은 피해·골드·소유권 판정을 변경하지 않는다.

## 현재 편집 대상과 연결

`Assets/Unit/GaebangChief/<모션>.png` → 같은 이름의 `.anim` → `GaebangChief.controller` → `Assets/Project333/ScriptableObjects/StarterTen/Cards/GaebangChief.asset`의 **Board Animator Controller** → 전투 타일.

| 상태 | 그림 / 컷 수 | Samples | 길이 | 반복 |
|---|---|---:|---:|---|
| Idle | 사용자 제작 Idle.png / 4 | 12fps | 약 0.333초 | 켬 |
| Attack | Attack.png / 8 | 12fps | 약 0.667초 | 끔 |
| Run | Run.png / 8 | 12fps | 약 0.667초 | 켬 |
| Death | Death.png / 8 | 8fps | 1초 | 끔 |
| BeAttacked | Idle_0 고정 / 1 | 12fps | 약 0.083초 | 끔 |
| Drained | Idle_0 고정 / 1 | 1fps | 1초 | 켬 |

2026-10-02 사용자가 직접 제작해 교체한 Idle.png와 직접 편집한 Idle.anim을 현재 재생 기준으로 사용한다. Idle_0→1→2→3의 가로 4컷을 12fps로 약 0.333초 동안 반복하며, 튜토리얼 안내자도 같은 순서·속도로 재생한다. 튜토리얼의 골드 설명은 기존 금화 눈 정지 이미지를 계속 유지한다. 이번 연결 작업에서는 사용자 PNG·스프라이트 메타·Idle 클립을 그대로 보존했고, 공격·이동·사망 모션도 변경하지 않았다.

Idle PNG는 **2048×512px의 가로 4컷**, Attack·Run·Death는 각각 **4096×512px의 8컷**이다. 개별 컷은 **512×512px**이며 접지 아래 투명 여백은 **16px**다. 캐릭터 윤곽과 동작 전체가 셀 안에 들어간다. Sprite는 Full Rect, 중앙 피벗, 100 PPU이며 런타임 추가 표시 배율은 공통 **1배**다. 사용자 Idle 그림을 다시 포장하거나 크기·위치를 보정하지 않았다.

컨트롤러의 여섯 상태는 위 클립을 사용한다. Idle이 기본 상태이고 State Speed는 1이다. 자동 전이를 추가하지 않았으며 전투 연출이 완료·취소·복귀 시점을 제어한다. 별도 BeAttacked·Drained 그림을 새로 생성하지 않았다.

카드 정지 그림은 `Idle_0`이다. 카드 원화는 [GaebangChief.png](../Assets/Project333/Resources/Project333/CardArtwork/GaebangChief.png)에 사용자 제공 원본 전체를 그대로 복사했다. [원본 보관본](../Tools/Art/Source/GaebangChief/References/개방방주.png)과 파일 SHA-256이 같으며 자르기·재합성으로 교체하지 않았다.

## 주먹 공격과 금화 VFX

Run으로 대상 앞에 접근하고 Attack을 재생한다. 준비 구간은 **3/12초 = 0.25초**이며 `Attack_3`의 시점과 맞는다. 그 뒤 실제 피격 대상의 공통 명중점에 금화 빛과 작은 금빛 입자를 표시한다. 대상 명중점은 `GetVisualImpactWorldPosition()`을 사용해 기존 하향 10 UI 단위 보정을 따른다. 공격 방향에 맞춰 좌우를 반전하고 생존한 공격자는 Run으로 원래 자리로 돌아온다.

VFX는 [CoinImpactAtlas.png](../Assets/Project333/Resources/Project333/Units/GaebangChief/CoinImpactAtlas.png)다. **2048×1024px**, **4열×2행**, **512×512px 셀 8개**를 중앙 피벗으로 분할한다. **16fps / 0.5초** 동안 재생한다. VFX의 중심은 명중점이므로 캐릭터 발 기준선의 하단 16px 규칙을 이 독립 효과의 축에 적용하지 않는다.

일반 공격과 반격은 각각의 재사용 슬롯을 사용한다. 취소·타일 재바인딩·비활성화·씬 종료 시 남은 VFX와 소리를 정리한다. 금화가 보이는 것은 공격 테마이며 효과 자체가 골드를 획득하거나 소비하는 것은 아니다. 피해 표시와 실제 일반 공격·반격 판정은 기존 전투 경로를 따른다.

타이밍·리소스 경로는 [GaebangChiefPresentationAssets.cs](../Assets/Project333/Runtime/Presentation/Battle/GaebangChiefPresentationAssets.cs), 금화 효과와 공격음 재생은 [GaebangChiefCoinPunch.cs](../Assets/Project333/Runtime/Presentation/Battle/GaebangChiefCoinPunch.cs)에서 확인한다.

## 공격음

[CoinPunch.wav](../Assets/Project333/Resources/Project333/Units/GaebangChief/Audio/CoinPunch.wav)는 잔잔한 주먹·옷 바람에 은은한 금화 흔들림을 합성한 **0.440초** 효과음이다. mono PCM16 / 44,100 Hz, 피크 **−19.999dBFS**, RMS **−35.843dBFS**이며 클리핑이 없다. 공격이나 반격의 준비 시작에 **한 번**, OneShot gain **0.55**로 재생한다.

기존 `AudioListener.volume`·음소거를 따르고 전체 음량을 소스에 다시 곱하지 않는다. PCM, Decompress On Load, Preserve Sample Rate와 preload 설정을 사용하며 forceToMono·background load는 끈다. 명중·피격·사망음을 별도로 추가하지 않았고 피해 소리는 기존 공통 **UnitHit** 경로를 사용한다.

합성기·독립 WAV 분석·재현성 검증은 [오디오 제작 기록](../Tools/Art/GaebangChiefCoinPunchAudio.md)과 `TempBuild/GaebangChiefPresentation_20260930/Export/AudioReports/`에 있다. 파일·파형 검증을 실제 기기나 전체 게임 믹스 청취 결과로 해석하지 않는다.

## 생성 원본과 안전한 제작 도구

- 내장 imagegen에 사용한 모션 4개와 VFX 1개의 정확한 프롬프트: [GaebangChiefPrompts.json](../Tools/Art/GaebangChiefPrompts.json).
- 생성 원본: `Tools/Art/Source/GaebangChief/<Idle·Attack·Run·Death·CoinImpactAtlas>-Generated-20260930.png`.
- 포장: [PackGaebangChiefSprites.ps1](../Tools/Art/PackGaebangChiefSprites.ps1), [PackGaebangChiefSprites.cs](../Tools/Art/PackGaebangChiefSprites.cs).
- 격리 검증 프로젝트의 자산 준비: [StageGaebangChiefPresentation.ps1](../Tools/Art/StageGaebangChiefPresentation.ps1).
- 일회성 Unity API 임포트: [ImportGaebangChiefPresentationAssets.cs](../Tools/Art/ImportGaebangChiefPresentationAssets.cs).
- 제작 출력·포장 기록·해시: `TempBuild/GaebangChiefPresentation_20260930/Export/`.

이전 16컷 Idle의 생성 원본·프롬프트·포장 도구는 `Tools/Art/Source/GaebangChief/Idle-Generated-20261002.png`, `Tools/Art/GaebangChiefIdlePrompt-20261002.json`, `Tools/Art/PackGaebangChiefSmoothIdle.ps1`·`.cs`에 과거 제작 기록으로 남아 있다. 당시 검증은 `TempBuild/GaebangSmoothIdle_20261002/`에 있다. **현재 사용자 제작 4컷 Idle에는 과거 포장·임포트 도구를 재실행하지 않는다.** 이번 연결·사용자 입력 보존 검증은 `TempBuild/GaebangUserIdle_20261002/`를 따른다.

포장 도구는 기본적으로 TempBuild에 출력하고 **Assets 내부 출력은 거부**한다. Stage 도구는 기존 격리 Unity 프로젝트 아래에만 복사하며 다른 내용의 기존 파일은 덮어쓰지 않는다. 어느 도구도 Unity Editor를 자동으로 시작하거나 종료하지 않는다.

Unity 임포트는 스프라이트 provider의 편집 가능 여부를 확인하고 Unity API로 분할·클립·컨트롤러·카드·카탈로그를 등록했다. 메타를 직접 작성하지 않았다. 두 번째 임포트에서 GUID·스프라이트 참조를 유지하고 카탈로그가 44개로 유지됨을 확인했다.

현재 **Assets의 PNG·클립이 편집 권위**다. 사용자가 새 PNG를 수정한 뒤 과거 생성 원본으로 포장·임포트를 다시 실행해 덮어쓰지 않는다. Source 폴더는 제작 보관본이며 그 파일만 고쳐도 게임은 바뀌지 않는다.

## 이후 수정 방법

1. 모션 그림은 `Assets/Unit/GaebangChief/`의 네 PNG를 편집한다. Idle 4컷·가로 격자, 다른 모션 8컷·가로 격자와 512px 셀·투명 배경·하단 약 16px 접지 여백 및 기존 `.meta`를 유지한다.
2. 컷 수·Samples를 바꾸면 Sprite Editor 분할과 해당 `.anim`의 키프레임·Loop Time도 맞춘다. Attack 속도·준비 컷을 바꾸면 0.25초 명중 시점도 함께 조정한다.
3. 금화 VFX는 CoinImpactAtlas.png의 4×2 셀과 중앙 명중 축을 유지해 편집한다. 컷 수나 재생 시간을 바꾸면 리소스 분할과 ImpactDuration도 맞춘다.
4. 소리는 CoinPunch.wav와 기존 메타를 유지해 교체하고 필요하면 GaebangChiefCoinPunch의 OneShot gain을 조정한다. 공통 UnitHit와 중복되는 묵직한 명중음은 붙이지 않는다.

휴대폰 반영에는 사용자가 최신 클라이언트를 빌드·설치해야 한다. 이 작업에서 앱 빌드·설치, 운영 서버 배포·재시작, 사용자의 주 Unity Editor 조작은 수행하지 않았다.

## 검증 증거와 실제 캡처

[최종 검증 기록](../TempBuild/GaebangChiefPresentation_20260930/verification.json)에 자산·소스·서버·Unity 입력 비교와 결과가 있다. 네이티브 카탈로그 43→44 등록 후 기존 43개 참조 순서, 기존 카드 JSON 50개와 보호 대상 자산 1,052개를 유지했다. 동일 입력의 포장을 두 번 실행한 PNG 해시도 일치한다.

격리 Unity 6000.3.12f1에서 개방 방주 관련 **54/54**를 통과했다. 세부 항목은 규칙 **40/40**, 자산 **9/9**, 전투 연출 **5/5**다. 기존 A-212 연출 **7/7**, 반격 연출 **6/6**도 통과했다. 선택 실행 전체는 **504/506 통과(2개 실패)**다. 두 실패는 현재 A-301 편집본의 6컷 Idle/Attack·약 0.667초 Death와 과거 검사의 8컷·Death 0.95초 이상 조건 차이로 확인했으며, 개방 방주 작업에서 A-301 파일을 수정하지 않았다. 자세한 판정은 최종 검증 기록과 [Unity 결과](../TempBuild/GaebangChiefPresentation_20260930/unity-results.xml)를 따른다.

서버 빌드는 오류 0개, 기존 nullable 경고 94개이며 개방 방주 실제 소환·공격 **84케이스**와 관련 카드·종족·전투 능력치 회귀의 오프라인 검사 명령이 통과했다. 양쪽 좌석의 Lv.0–13 소환, 골드 획득·소비·비용 지불, 억제 상태·고독·전투 뷰·JSON 복원을 포함한다. 이 결과는 운영 서버나 Android 실기기 전투 검증이 아니다.

| 미리보기 | 파일 |
|---|---|
| 전체 모션·VFX | [GaebangChiefContactSheet.png](../Tools/Art/Preview/GaebangChiefContactSheet.png) |
| 플레이어 공격 명중 | [gaebang-chief-contact-player.png](../Tools/Art/Preview/gaebang-chief-contact-player.png) |
| AI 공격 명중 | [gaebang-chief-contact-ai.png](../Tools/Art/Preview/gaebang-chief-contact-ai.png) |
| 플레이어 복귀 | [gaebang-chief-return-player.png](../Tools/Art/Preview/gaebang-chief-return-player.png) |
| AI 복귀 | [gaebang-chief-return-ai.png](../Tools/Art/Preview/gaebang-chief-return-ai.png) |
| 사망 | [gaebang-chief-death.png](../Tools/Art/Preview/gaebang-chief-death.png) |

캡처는 격리 연출 검증 장면에서 실제 Animator와 VFX를 실행한 결과다.
