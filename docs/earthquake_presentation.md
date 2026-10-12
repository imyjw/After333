# 지진 균열과 낮은 진동음 연출

2026-09-30 사용자 요청. 지진을 중립 언커먼 카드로 등록하고 덱 빌딩과 랜덤 보상에 포함했다. 사용자가 제작한 카드 이미지는 PNG와 `.meta` 모두 보존했다. 전투 판정의 기준은 `battle_rules.md`의 Earthquake다.

## 확정 전투 규칙

골드 3을 지불하는 대상 지정 없는 ScriptedSpell이다. 성공한 사용에서 해당 손패 카드 한 장을 소비하고 즉시 상대 전장의 현재 유닛·건물·마스터에게 물리 피해를 준다. 아군 전장에는 지진의 직접 피해가 없으며 지속 효과를 만들지 않는다.

기본 피해는 10이며 Lv.1-13 매 레벨 +1이다. 유닛·마스터는 `10 + 레벨`, 건물은 `3 × (10 + 레벨)`을 물리 방어 전에 계산한다. Lv.13 방어 적용 전 피해는 각각 23과 69다. 예를 들어 물리 방어 4인 건물은 65 피해를 받는다. 골드 비용과 건물 3배 계수는 강화로 변하지 않는다.

물리 피해이므로 주문력을 더하지 않는다. 광역 마법에는 쉴더 대리 피격·은신 회피·비행 감산을 적용하지 않으며, 봉인·무적·불굴·방전은 기존 피해 규칙을 따른다. 지진의 모든 피해 패킷을 적용한 뒤 사망 제거·연쇄 폭발·마스터 결과를 처리한다. 원자력 발전소 등 다른 효과의 연쇄 피해는 그 효과의 기존 범위를 따른다.

## 화면과 소리 — 현재 연결 (2026-10-01)

각 균열은 해당 TileBackground 앵커의 부모 Canvas를 먼저 사용한다. 소환물 크기의 Canvas가 전장 전체 연출 기준이 되는 것을 피하며 기존 유닛 Canvas는 대체 경로로만 남긴다. 실제 씬 재생 복구 결과는 아래 날짜별 기록에 따로 남긴다.

성공한 사용에서 적 전장 10타일에 균열이 동시에 자라나고 점차 옅어진다. 빈 타일에도 표시하며 유닛·마스터·건물 그림 아래에 놓는다. 균열 Canvas는 부모 지정과 활성화를 마친 뒤 sorting order 200을 지정한다. 비활성 상태에서 설정하면 활성화 때 초기화되는 Unity 동작을 피한다. 실제 타일 배경의 중심에 정사각형으로 배치하고 짧은 변의 0.9배를 기준으로 화면 경계 안에 맞춘다. 논리 보드의 5×2 좌표를 명시적으로 찾아 현재 UI 배치를 사용한다.

16컷을 16fps로 재생하며 전체 연출은 1.15초다. 마지막 0.2초에 투명해진다. 화면의 같은 씬 UI를 처음 0.85초 동안 작은 진폭으로 흔들고 점차 감쇠한다. 초기 진폭은 화면의 짧은 변에 따라 약 4-6 화면 픽셀로 제한한다. 카메라나 씬 구조를 이동시키지 않으며, 매 프레임 자신의 흔들림 값만 제거·재적용하여 다른 UI 이동을 보존한다.

낮은 “쿠구구구” 음원은 사용마다 한 번만 재생한다. 타일마다 소리를 겹치지 않으며 공통 UnitHit와 기존 피해 숫자·피격·사망 모션을 유지한다. 고유 rumble gain은 0.30이고 전체 음량·음소거는 기존 AudioListener 설정에서 한 번 적용한다.

로컬 인간·AI와 온라인은 성공한 사용과 실제 결과의 시전자 진영을 따른다. 상태 복원만으로 균열이나 소리를 추측해 재생하지 않는다. 피해를 연출 코드에서 다시 계산·적용하지 않는다. 취소·비활성화·종료 때 균열·소리를 숨기고 화면 흔들림 기여분을 제거한다. 첫 리소스 로딩을 마친 뒤 재생 시작 시각을 기록하여 1.08초 음원의 끝부분을 보존한다. 연출 시간은 재생 시작 이후의 unscaled 경과 시간을 사용하며 UI 클릭을 가로막지 않는다.

## 실제 편집 파일

| 파일 | 용도 |
|---|---|
| `Assets/Project333/Resources/Project333/CardArtwork/Earthquake.png` | 사용자 제작 카드 이미지; 기존 PNG와 `.meta` 보존 |
| `Assets/Project333/ScriptableObjects/StarterTen/Cards/Earthquake.asset` | 지진 카드 정의; `StarterTenCardCatalog`에 한 번 등록 |
| `Assets/Project333/Resources/Project333/SpellEffects/Earthquake/Cracks.png` | 2048×2048, 4×4 배열의 16컷 균열 |
| `Assets/Project333/Resources/Project333/SpellEffects/Earthquake/Audio/EarthquakeRumble.wav` | 조용한 1.08초 mono PCM16/44.1kHz 음원 |
| `Assets/Project333/Runtime/Presentation/Battle/EarthquakeVfx.cs` | 타일 배치, 크기, 프레임·흔들림·음량·취소 처리 |
| `Assets/Project333/Runtime/Presentation/Battle/BattleBootstrapper.cs` | 성공한 로컬/온라인 시전과 기존 피해 반응 연결 |
| `Assets/Project333/Resources/Project333/Data/cards.json` | 비용·기본 피해·출현 플래그·설명 데이터 |
| `Assets/Project333/Runtime/Domain/Cards/EarthquakeRules.cs` | 확정 비용·기본 피해·건물 계수·기본 설명 상수 |
| `Assets/Project333/Runtime/Application/Services/SpellService.cs` | 실제 피해·방어·사망 처리 |

지진 추가 당시(2026-09-30)의 공유 JSON/domain 정의는 45종, Unity 네이티브 카탈로그는 38종이었다. 당시 드래프트 38종·강화 가능 34종·실제 랜덤 보상 29종이며, 후속 메테오 추가를 포함한 최신 집계는 [카드 카탈로그](card_catalog.md)를 따른다. 네이티브 카드 자산 수와 게임 정의 수를 혼동하지 않는다.

## 스프라이트 교체 규격

한 컷은 전체 512×512px, 이름은 `Cracks_00`~`Cracks_15`이며 왼쪽→오른쪽/위쪽→아래쪽 순서다. pivot은 중심 `(256,256)`, PPU 100, Point 필터, mipmap 없음, 무압축, 알파 투명, 전체 사각형을 사용한다. 프레임마다 그림의 bounding box로 잘라 재중심화하지 않는다.

1254×1254 생성 원본의 전체 4×4 격자에 같은 nearest-neighbor 배율 `1920/1254 = 1.5311004784689`를 적용했다. 각 480×480 셀 전체를 512px 캔버스의 `(16,16)`에 넣어 모든 알파 픽셀에 최소 16px 여백을 확보했다. 원본 알파와 균열의 성장·소멸 크기를 유지했으며 컷별 크기 정규화·재그림·크롭·알파 제거는 하지 않았다.

같은 PNG를 교체할 때 `.meta`, 16컷 순서, 512px 격자와 중심을 유지한다. Play Mode를 재시작하면 리소스 캐시가 새 그림을 읽는다. 컷 수나 pivot을 바꾸려면 Unity Sprite Editor Data Provider API와 `EarthquakeVfx`의 프레임 계약을 함께 확인한다. 사용자가 편집한 최신 PNG에 예전 생성 원본을 다시 덮어쓰지 않는다.

## 효과음 교체와 제작

외부 녹음·샘플 없이 고정 seed의 C# DSP로 새 음원을 합성했다. 낮은 대역의 둥근 진동과 느린 굴곡을 사용하며 별도 날카로운 파열음·hiss 층을 넣지 않았다. 4차 high-pass 50Hz와 low-pass 1200Hz, 60ms 시작·200ms 종료 페이드를 적용했다.

실제 PCM 측정은 47,628샘플/1.08초, peak −17.001dBFS, RMS −29.785dBFS, DC offset 약 2.88×10⁻⁸이다. 시작·마지막 샘플은 0이고 clipping은 0이다. 지속부의 중간 200ms RMS −29.079dBFS, 첫 20ms −48.994dBFS, 마지막 50ms −55.316dBFS로 시작·종료 페이드를 확인했다. Suggested gain 0.30을 사용한다.

WAV를 같은 경로에 교체할 때 `.meta`를 보존하고 mono PCM16/44.1kHz와 낮은 출력 수준을 유지한다. Unity에서는 PCM/DecompressOnLoad/PreserveSampleRate/preload로 가져오고 별도 normalize나 loudness boost를 하지 않는다. 길이를 바꾸면 전체 연출 길이와 임포터의 검증 계약도 점검한다. 실제 표시·소리 반영에는 사용자의 최신 클라이언트 빌드·설치가 필요하다.

## 제작 자료와 정확한 이미지 프롬프트

- 생성 원본: `Tools/Art/Source/Earthquake/Cracks-Generated-20260930.png`.
- 정확한 요청·생성 경로: `Tools/Art/EarthquakePrompt.json`; 내장 `image_gen`, transparent background 사용.
- 포장: `Tools/Art/PackEarthquakeSprites.ps1`.
- 재현 가능한 음원: `Tools/Art/EarthquakeRumbleAudio.cs`, `GenerateEarthquakeRumbleAudio.ps1`.
- 독립 RIFF/decoded PCM 검사: `Tools/Art/ValidateEarthquakeRumbleAudio.ps1`.
- Unity 네이티브 Sprite/Audio 등록: `Tools/Art/ImportEarthquakePresentationAssets.cs`.
- 사용자 카드 이미지 보존과 카드/카탈로그 등록: `Tools/Art/ImportEarthquakeRegistration.cs`.

포장·합성 도구는 기본적으로 Assets 밖의 `TempBuild/EarthquakePresentation_20260930/{Packed,Audio}`에 출력하고 Assets로 직접 내보내는 것을 거부한다. 검토한 결과만 실제 에셋에 반영한다. `.meta`는 직접 작성하지 않으며 네이티브 Unity API 결과를 사용한다.

아래는 생성 도구에 보낸 원문이다.

```text
Use case: stylized-concept. Asset type: transparent 2D pixel-art earthquake ground-crack animation atlas for a fantasy card battle game. Create a BRAND NEW sprite effect, not a card design. Exactly 16 chronological animation frames in a strict 4 by 4 equal-cell grid, ordered left to right then top to bottom, ideally 2048x2048 overall, each frame ultimately 512x512. Each frame shows a single TOP-DOWN branching ground fissure centered on the same origin, fitting entirely within its cell with 28px clear margins and no spill. Transparent around and through open spaces: no opaque ground rectangle, no platform, no background, no checkerboard, no grid lines. A jagged dark deep black/brown crack network splitting in 4-6 directions, thin warm ochre/orange rough stone edges, a few tiny lifted pebbles and a subtle tan dust puff. Crisp coherent grouped pixel art clusters; physical ground rupture rather than magical fire or laser, restrained orange, no lava or flames, no white flash. Frame1 short hairline crack begins at center; frames2-5 fracture branches grow rapidly into a wide branching network covering most of the cell; frames6-10 wider dark cracks with sparse rubble and dusty tremors; frames11-16 dust settles and the cracks gradually narrow and become faint, ending nearly transparent. All frames retain consistent scale, center, orientation and maximum footprint so a tiled field never jumps. No text, numbering, characters, buildings, weapons, borders or watermark. This will be overlaid simultaneously on ten target tiles; preserve underlying tile visibility.
```

## 검증 상태

두 번의 독립 PNG 포장과 두 번의 WAV 합성은 각각 같은 SHA256을 만들었다. PNG는 `D78F214C7FA2050AAA3D2630217FCAC16828A52EDB5AA61FA6FE53992B251438`, WAV는 `AF998BE7C62093531DEBB664220083C78CE4BA4365408CA4A1A8C3231233F45B`다. 기존 사용자 카드 PNG/메타데이터는 보존했다.

격리된 Unity Editor에서 native Sprite Data Provider/AudioImporter 등록과 사용자 카드 자산·카탈로그 등록은 성공했다. 독립 WAV 구조·PCM·페이드 검사도 통과했다. 격리 출력으로 빌드한 서버 코드의 최종 `--earthquake-only` 검사는 실제 드래프트/보상 필터, 양쪽 좌석 Lv.0-13, 건물 3배와 방어 순서, 보호·사망 연쇄, 정확한 손패/비용, 참가자 강화·JSON 복원을 통과했다. 실제 사람·AI 명령에서는 부활 대기 마왕의 피해 피드백을 유지하고 잘못된 제거/사망 이벤트만 억제한다. 지워진 마왕의 실제 제거와 기존 Nuclear·TimedBomb 회귀 검사도 통과했다. 서버 컴파일 오류 0개이며 기존 nullability 경고 94개가 남는다.

격리 Unity 6000.3.12f1에서 최종 지진 연출 13/13이 통과했다. 직전 37개 클래스/719개 검사에서 715개가 통과했고, 나머지 4개는 균열 Canvas 활성화 시 레이어 초기화 문제였다. 이를 수정하고 관련 연출을 재검사해 실제 결과 합계 719개의 서로 다른 검사가 모두 통과했다. 마지막에 전체 719개를 한 번에 재실행한 결과는 아니며 `aggregate-verification.json`에 각 검사의 실제 결과 파일을 기록했다.

처음 실패한 새 테스트의 시작 골드·사망 후 HP 감소 피드백 전제와 취소 직후 입력 잠금 대기 순서를 실제 규칙에 맞게 수정했다. 피해 판정이나 기존 입력 잠금 정책을 그 전제에 맞춰 바꾸지 않았다. 네 장의 실제 RGB 렌더 미리보기는 `Tools/Art/Preview/Earthquake-{Cracks-Player,Cracks-AI,Hud-Baseline,Hud-Shaken}.png`에 있으며 양쪽 시전자에서 적 10타일 균열, 아군 무변경, HUD 흔들림을 검사한다. 가로·세로 세 화면 비율과 독립/중첩 Canvas, 외부 UI 이동 보존, 취소·비활성화·컴포넌트 제거, 음원 재사용도 검사했다.

최종 현재/격리 Unity 입력 797개와 서버 입력 198개의 SHA256이 일치한다. 기존 보호 대상 카드 그림·소환물 그림·효과음·메타데이터 123개가 모두 그대로이며 기존 카탈로그 37개 참조도 보존했다. 컴파일된 서버 카드 JSON은 현재 JSON과 같다. 원래의 주 Unity Editor와 실서버는 조작하지 않았으며 격리 테스트에서만 기존 LevelPlay 어댑터를 제외했다.

증거는 `TempBuild/EarthquakePresentation_20260930/`의 `packing-report.json`, `reproducibility-report.json`, `asset-import-result.json`, `card-registration-result.json`, `Audio/EarthquakeRumbleAudioIndependentValidation.json`, `server-earthquake.log`에 보존했다. 실기기 화면·청취, 앱 빌드·설치나 서버 배포·재시작을 검증한 것은 아니다.


## 실제 전투씬 재생 수정 — 2026-10-01

Battle_VSlice의 독립 타일 Canvas 구조에서는 유닛 그림용 작은·비활성 Canvas가 선택되어 지진 재생이 시작 즉시 취소되었다. 실제 씬 렌더 검사에서 기존 코드의 Play 반환값 0을 재현했다. 이제 TileBackground 앵커의 부모 Canvas를 우선 사용하고, 기존 유닛 Canvas 선택은 대체 경로로 유지한다.

상대 10칸의 균열·16fps 프레임 진행·화면 흔들림과 원래 위치 복원을 검증했다. 그림/음원/메타·피해/비용·1.15초 재생 시간·정렬 순서200은 그대로다. 공개 카드 사용 경로에서 피해·비용·손패가 한 번만 반영되고 재생 중 재입력이 차단되는 것도 검사했다.

지진·뉴클리어 및 메테오/파이어스피어/시한폭탄 관련 13개 fixture, 격리 Unity6000.3.12f1 **고유 검사 233 개 모두 통과**(최종 미해결 실패/skip/누락/컴파일 오류0). 전체 검사230/233 이후 검사 준비 코드만 수정해15/16·13/14·최종14/14를 검사했다. 코드·연출 그림·음원 입력은 이 실행들 사이에 동일하며 verified-test-summary/cases가 각 고유 검사의 최종 통과 증거를 연결한다. 실제 씬의 독립 Canvas를 보존한 렌더13장(640×480), 양쪽 시전자와 취소 후 원상 복원 확인. 최종 입력2100 개의 source/copy SHA 일치. 증거: TempBuild/AreaSpellPlaybackFix_20261001/verification.json 및 XML/log/Captures. 초기 검사 fixture 오류와 수정 전 재생 실패 증거는 별도 baseline/attempts에 보존했다.

이번 수정은 클라이언트 연출 소스에만 반영했다. 사용자 앱 빌드·설치, 본 Unity 씬 저장, 서버 배포·재시작, commit/push는 하지 않았다. 실제 휴대폰 경기 확인과 구분한다.
시작 시점과 비교해 별도 오크 족장·뉴클리어 카드 그림 PNG가 변경되어 외부 변경으로 기록했다. 이번 작업에서 해당 카드 그림을 쓰지 않았으며 현재 원본을 유지했다. 두 카드 그림은 최종 격리 입력과 SHA가 일치한다. 뉴클리어 카드 그림은 전체 검사 시작 전 이미 최신 원본이 반영됐고, 지진·뉴클리어 연출 프레임은 변경되지 않았다. 상세 external-source-change.json.
