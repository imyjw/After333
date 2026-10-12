# 메테오 하강·폭발과 두 낮은 효과음

2026-09-30 사용자 요청. 메테오를 마나 7의 유니크 마법으로 등록하고 드래프트·랜덤 보상에 포함했다. 같은 날짜 후속 답에서 사용자가 판타지 소속을 명시적으로 확정하고 희귀도를 Rare(레어)에서 Unique(유니크)로 변경하도록 요청했다. 사용자가 제작한 카드 PNG는 보존했고, 별도의 비행·폭발 VFX를 새로 생성했다. 판정의 기준은 [전투 규칙](battle_rules.md)의 Meteor다.

## 확정 전투 규칙

CardId `Meteor`, effectId `meteor`인 ScriptedSpell이다. 자신의 행동 턴에 보드 대상을 지정하지 않고 사용하며, 기존 마나/골드 대체 규칙으로 비용 전액을 지불하고 선택한 정확한 손패 한 장을 버림패로 옮긴다. 지속 효과·카운트다운·새 저장 상태를 만들지 않는다.

사용 즉시 상대 전장의 현재 유닛·건물·마스터에게 각 1회 마법 피해를 준다. 기본 피해 40에 Lv.1-13 매 레벨 +1이며 Lv.13 기본 피해는 53이다. 시전자의 현재 아군 주문력 합계를 이번 동시 광역 피해에서 한 번 읽고, 각 대상에 그 보너스를 한 번 더한 뒤 마법 방어·보호 규칙을 적용한다. 건물 배수는 없다. 예를 들어 Lv.13·주문력 20은 incoming 73, 마법 방어 4인 대상은 다른 보호 적용 전 69다.

쉴더 대리피격·은신 회피·비행 감산은 적용하지 않으며, 봉인·무적·불굴·방전·피해 유발 효과는 기존 규칙을 따른다. 모든 직접 피해 패킷을 먼저 적용한 뒤 사망 제거·부활·연쇄 폭발·마스터 결과를 처리한다. 아군은 메테오 직접 피해를 받지 않지만 원자력 발전소 등 다른 효과의 연쇄 피해는 그 효과의 기존 범위를 따른다. 강화로 마나 비용·대상 범위는 바뀌지 않는다.

## 화면과 소리 — 현재 연결 (2026-10-01)

전체 화면 기준은 실제 `TileBackground` 앵커의 root Canvas다. 유닛 그림의 작은·빈 타일에서 비활성인 Canvas는 우선 기준으로 사용하지 않는다. 재부모화·활성화 뒤 overrideSorting과 order 4908을 다시 설정한다. 이 현재 연결은 아래 최초 제작 설명과 2026-10-01 실제 씬 복구 기록을 함께 반영한다.

하나의 거대 운석이 화면 위쪽 왼편에서 논리적인 적 전장의 중심으로 0.65초 동안 가속하며 내려온다. 시전자의 반대 필드에 속한 실제 5×2 타일 좌표를 찾아 중심과 크기를 계산하므로 어느 좌석의 시전에도 같은 의미를 유지한다. 빈 필드에서도 타일 중심을 사용한다.

충돌 뒤 `Impact_00`~`Impact_15` 16컷을 16fps로 재생하고 마지막 0.25초에 옅어진다. 폭발·tail은 1.15초, 정상 전체 타임라인은 `0.65 + 1.15 = 1.80초`다. 하나의 큰 폭발을 필드 중심에 놓고 비대칭 원점을 고려해 화면 경계 안으로 맞춘다. 부모 지정·활성화 뒤 overlay sorting order 4908을 지정하며 클릭을 막지 않는다. 카메라나 보드 자체의 위치는 바꾸지 않는다.

도착음 0.65초와 충돌음 1초를 각각 한 번 재생하고 각 OneShot gain은 0.30이다. 타일마다 음원을 중복 재생하지 않으며 전체 음량·음소거는 기존 AudioListener 설정에서 한 번 적용한다. 공통 UnitHit와 기존 피해 숫자·피격·사망 모션을 유지한다.

로컬 인간·AI와 온라인의 성공한 시전 결과에서만 재생하며 상태 복원으로 과거 연출을 다시 추측하지 않는다. 실제 피해는 공유 서비스/서버에서 즉시 결정되며 UI가 0.65초의 충돌 시점에 기존 피드백을 보여주도록 연결한다. 동일 충돌 신호는 폭발음을 중복 시작하지 않는다. 첫 리소스 로딩 뒤 unscaled 시각을 기록하며, 렌더 프레임 지연 때는 시작한 폭발과 음원 tail이 끝날 시간을 보존한다. 취소·비활성화 때 표시와 소리를 정리하고 종료 때 소유 overlay/AudioSource를 제거한다.

온라인에서는 실제 충돌·피격·사망·폭발 tail의 완료 신호를 기다린 뒤 다음 HP/BattleEnded 스냅샷을 표시한다. 그동안 행동 입력은 잠그고 카드 선택·확대·긴 누르기 열람은 유지한다. 체력 표시만 RuntimeId에 묶어 비행 중 HpBefore, 충돌 때 HpAfter로 투영하며 실제 전투 상태는 수정하지 않는다. 권위 스냅샷·재바인드·취소·비활성화 때 표시 덮어쓰기를 해제한다. 손패와 덱 목록 미리보기는 강화 레벨·현재 주문력 변경을 설명과 수치에 반영한다.

## 실제 편집 파일

| 파일 | 용도 |
|---|---|
| `Assets/Project333/Resources/Project333/CardArtwork/Meteor.png` | 사용자 카드 PNG를 덮어쓰지 않고 후속 수정 유지. 작업 전에 없던 metadata는 Unity API 생성 |
| `Assets/Project333/ScriptableObjects/StarterTen/Cards/Meteor.asset` | 카드 정의와 `StarterTenCardCatalog` 등록 |
| `Assets/Project333/Resources/Project333/SpellEffects/Meteor/Flight.png` | 전체 512×512의 비행 그림 1장 |
| `Assets/Project333/Resources/Project333/SpellEffects/Meteor/Impact.png` | 2048×2048, 4×4 배열의 폭발 16컷 |
| `Assets/Project333/Resources/Project333/SpellEffects/Meteor/Audio/MeteorIncoming.wav` | 조용한 도착음 0.65초 |
| `Assets/Project333/Resources/Project333/SpellEffects/Meteor/Audio/MeteorImpact.wav` | 조용한 폭발·낮은 잔향 1초 |
| `Assets/Project333/Runtime/Presentation/Battle/MeteorVfx.cs` | 프레임·원점·화면 경계·타이밍·음량·취소 |
| `Assets/Project333/Runtime/Presentation/Battle/BattleBootstrapper.cs` | 로컬/AI/온라인 성공한 시전과 충돌·기존 피해 피드백 연결 |
| `Assets/Project333/Runtime/Presentation/Battle/TileTextView.cs` | 동일 RuntimeId의 표시 체력 투영·스냅샷/취소/재바인드 정리 |
| `Assets/Project333/Runtime/Presentation/Draft/DeckCardPreviewOverlay.cs` | 손패/미리보기 설명과 레벨 갱신 경로 |
| `Assets/Project333/Resources/Project333/Data/cards.json` | 비용·기본 피해·문명·출현 플래그·설명 |
| `Assets/Project333/Runtime/Domain/Cards/MeteorRules.cs` | 확정 비용·기본 피해·기본 설명 |
| `Assets/Project333/Runtime/Application/Services/SpellService.cs` | 실제 주문력·피해·보호·사망 처리 |

메테오 등록 당시의 공유 JSON/domain 정의 46종과 Unity 네이티브 카드 자산 39종은 별도 집계했다. 당시 드래프트 39종, 강화 가능 35종, 실제 랜덤 보상 30종이며 후속 오크 광전사 추가를 포함한 최신 집계는 [카드 카탈로그](card_catalog.md)를 따른다.

## 전체 셀과 실제 원점

Flight는 이름 `Flight`인 full 512px Sprite 1개이며 네이티브 importer는 Multiple 모드에 한 Sprite를 저장한다. Impact는 full 512px 셀 16개, 이름 `Impact_00`~`Impact_15`, 왼쪽→오른쪽/위쪽→아래쪽 순서다. 모두 pivot 중심 `(256,256)`, PPU 100, Point, mipmap 없음, 무압축, RGBA 알파와 FullRect를 사용한다.

생성 원본 두 장은 각각 1254×1254다. Flight 전체를 480px로 같은 배율 `480/1254 = 0.382775119617`로 줄이고 512px 셀의 `(16,16)`에 둔다. Impact 전체 4×4 격자는 같은 배율 `1920/1254 = 1.531100478469`로 resample한 뒤 각 전체 480px 셀을 full512px 셀의 `(16,16)`에 둔다. 원본 알파와 단계별 크기를 보존하며 프레임별 bounding box 크롭·재중심화·크기 정규화·알파 제거는 하지 않는다.

새 원본의 alpha≥16이 원래 셀 첫/마지막 픽셀에 닿으면 포장 도구가 거부한다. 출력에 여백만 더해 원본 경계에서 잘린 그림을 숨기지 않는다. 실제 출력의 모든 nonzero alpha는 셀 내부 `16..495`에 있어 최소 16px 바깥 여백을 지킨다.

중심 pivot을 변경하지 않고 다음 실제 원점으로 표시 위치를 보정한다. 값은 Unity의 왼쪽 아래 원점 기준 정규화 좌표다.

| 원점 | 값 | 측정·선택 근거 |
|---|---|---|
| 비행 암석 머리 | `(.65,.359375)` | 생성 원본에서 선택한 top-origin `(.66,.65)`를 전체 셀 배율·16px 여백으로 변환 |
| 첫 충돌 flash | `(.5137399525,.3744585921)` | 가장 넓은 흰 접촉 cross 부근의 가중 centroid; packed x263.034856/top-y320.277201 |

접촉점은 Frame0의 alpha≥128/R≥240/G≥235/B≥170 픽셀에서, 가장 넓은 흰 행(y321) 주변 25px 띠의 G×alpha 가중 centroid를 사용했다. pivot이나 프레임별 위치를 이 값에 맞춰 다시 자르지 않았다. 새 그림의 머리·접촉점이 달라지면 native pivot은 유지한 채 `MeteorVfx`의 두 원점 상수와 화면 경계 검사를 함께 점검한다.

## 효과음과 교체

외부 녹음·샘플 없이 고정 seed의 C# DSP로 새 두 음원을 합성했다. 둥근 저역 진동과 낮은 불기운을 사용하고 날카로운 휘파람·파열음·hiss 층을 넣지 않았다. 4차 high-pass 55Hz, Incoming low-pass 1400Hz/Impact low-pass 1300Hz로 대역을 제한했다.

| 음원 | PCM 규격 | peak/RMS dBFS | 페이드 |
|---|---|---|---|
| MeteorIncoming | mono PCM16/44.1kHz, 28,665샘플·0.65초 | −17.001 / −32.250 | 시작 50ms·종료 90ms |
| MeteorImpact | mono PCM16/44.1kHz, 44,100샘플·1초 | −17.001 / −33.511 | 시작 60ms·종료 220ms |

두 음원은 시작·마지막 샘플 0, clipping 0, DC 절대값 4.36×10⁻⁸ 이하이며 각각 gain 0.30을 사용한다. 독립 RIFF/decoded PCM 검사는 끝 20ms와 본문 200ms를 비교해 실제 페이드를 확인한다. 90ms만에 끝나는 도착음의 마지막 50ms 전체를 fade 종료 구간으로 가정하지 않는다.

같은 PNG/WAV를 바꿀 때 기존 `.meta`, Sprite 이름·순서·full512 셀·pivot을 보존한다. WAV는 mono PCM16/44.1kHz와 낮은 출력 수준을 유지하고 PCM/DecompressOnLoad/PreserveSampleRate/preload로 가져온다. 이미 mono인 파일을 forceToMono/normalize하거나 별도 loudness boost하지 않는다. 리소스 캐시는 Play Mode를 재시작하면 새 파일을 읽는다. 사용자가 나중에 수정한 파일을 예전 생성 원본으로 덮어쓰지 않는다. 실제 기기에 반영하려면 사용자의 최신 클라이언트 빌드·설치가 필요하다.

## 제작 도구와 정확한 생성 프롬프트

- 선택된 비행/폭발 원본: `Tools/Art/Source/Meteor/{Flight,Impact}-Generated-20260930.png`.
- 참고용 원본 복사: `Tools/Art/Source/Meteor/Reference-Meteor-20260930.png`; 사용자 업로드/카드 PNG를 수정 대상으로 삼지 않았다.
- 요청·모드·선택/기각 source와 정확한 prompt: `Tools/Art/MeteorPrompt.json`.
- 전체 셀 포장: `Tools/Art/PackMeteorSprites.ps1`.
- 재현 가능한 음원: `Tools/Art/MeteorAudio.cs`, `GenerateMeteorAudio.ps1`.
- 독립 WAV 검사: `Tools/Art/ValidateMeteorAudio.ps1`; Impact 검사에는 `ExpectedDurationSeconds 1.0` 지정.
- 네이티브 Sprite/Audio: `Tools/Art/ImportMeteorPresentationAssets.cs`의 `Run()`.
- 사용자 PNG 보존과 카드/카탈로그: `Tools/Art/ImportMeteorRegistration.cs`의 `Run()`.

선택된 두 이미지는 내장 `image_gen`의 **generate** 모드와 transparent background로 새로 생성했다. 참조는 모양·분위기 참고용이다. 초기 생성과 framing edit 후보는 원래 셀 경계를 강한 알파가 넘어서 채택하지 않았으며, 별도의 마지막 generate가 실제 Impact 원본이다. 포장·음원 도구의 기본 출력은 Assets 밖 `TempBuild/MeteorPresentation_20260930/{Packed,Audio}`이고 Assets로 직접 출력하는 것을 거부한다. metadata는 손으로 쓰지 않으며 capability/name-fileID 검사를 통과한 Unity Sprite Editor Data Provider와 AudioImporter API로 등록한다.

선택된 Flight 생성 원문:

```text
Use case: stylized-concept. Asset type: one original transparent pixel-art VFX sprite for a Unity fantasy card battle. Primary request: a gigantic flaming meteor rushing diagonally DOWN RIGHT, inspired only by the user's small fiery meteor example; create new art, do not reproduce the reference. Scene/backdrop: genuinely transparent RGBA, no ground, no background. Subject: a rugged dark volcanic rock with angular charcoal ridges, glowing orange lava cracks, white-gold fiery leading edge, saturated orange/red flames and sparse bright embers trailing towards the upper left. Style: crisp hand-crafted 2D pixel art with stepped chunky edges and restrained palette matching fantasy sprite battle VFX, no soft painterly gradients. Composition/framing: SQUARE cell; the solid meteor head is centered EXACTLY at (50% width,50% height), ~38% of cell diameter; tapered fire tail runs toward top-left within the cell. Entire rock/flames/embers fit inside the square leaving 5% transparent margin on ALL edges. The impact/contact anchor is the rock-head center, not the tail or full bounds. A single sprite, not a sheet. Keep all pixels within the cell. No text, labels, border, grid, watermark, UI, characters or landscape.
```

선택된 Impact 생성 원문:

```text
Create an ORIGINAL production-safe transparent PIXEL ART sprite animation sheet for a fantasy game's gigantic meteor EXPLOSION. Output a SQUARE image containing EXACTLY 16 SEPARATE SMALL sprites in a STRICT uniform 4-COLUMN, 4-ROW grid. Reading order left-right then top-bottom. Each square cell contains ONE COMPLETELY ISOLATED explosion sprite. Each sprite may occupy AT MOST the CENTRAL 55% of its cell width AND CENTRAL 55% of its cell height. Leave a very wide empty transparent gap between ALL sprites, at least22% of every cell on each side. These EMPTY GAPS are mandatory because the game cuts the sheet into equal square frames. NOTHING whatsoever may touch a cell boundary. No effect may connect into a neighboring frame; no smoke/fire/rock/dust can overlap, crop, cross or touch the boundaries. IMPORTANT: use small isolated icon-like compact round silhouettes, do NOT draw tall plumes reaching boundaries. A fixed ground impact point sits at cell-local(50% width,65%height) with the compact blast above it, always perfectly aligned. Sixteen phases: 0 compact white-gold star strike;1 orange ignition;2 small growing orange fireball;3 larger round white-gold fireball;4-6 peak bright round orange blast with a SMALL dust ring and TWO charred fragments;7-9 round dark gray smoke puff with orange cracks;10-12 compact cooling smoke;13 faint smoke;14 few embers;15 almost empty three tiny embers. All visible sprites remain within central55% bounding rectangle even atpeak. Crisp chunky pixel art, hard stepped edges, restrained fantasy-game colors white/yellow/orange/red/charcoal/gray, transparent alpha. No landscape, large ground slab, mushroom-cloud tall plume, blurred glow, photo, text, grid lines, panel borders, backgrounds, checkerboard, UI or watermark. The displayed in-game size will be enlarged programmatically, so keep each source sprite SMALL with massive transparent spacing.
```

## 검증 상태

아래 실행 결과와 입력 SHA 검사는 희귀도 변경 전 Rare로 등록한 메테오의 기록이다. 후속 Unique 변경에 맞춰 과거 로그/XML을 고치지 않으며, 이번 희귀도 변경의 별도 실행 검증과 구분한다.

전체 셀 포장과 DSP 합성을 각각 두 번 실행한 PNG/WAV SHA256이 일치했다. 최종 Impact 16컷 모두 원본 visible 경계 픽셀 0이며 출력 알파 여백도 통과했다. Earthquake 카드 PNG/meta와 Downloads 참고 원본의 SHA는 보존했다. 작업 중 후속 수정된 사용자 Meteor 카드 PNG는 현재 파일을 유지했다.

| 미디어 | SHA256 |
|---|---|
| Flight.png | `C1057DA7120D653D0C849E54DA59D68DD345F4E55E95032A46A5A4F85EB30444` |
| Impact.png | `E02B4806432CCE820F1D6A4DC252B1B03D72BE9A1274280159AAE3FAD6530221` |
| MeteorIncoming.wav | `FE2D82442C314DF0235F8BDEB0537C535B43B723F8A13F560BF1F8E043070CD9` |
| MeteorImpact.wav | `00413AA645849A1F01E687986EDFDF06274533B0924B87B330AF92677602A7A4` |

실제 설치 WAV의 독립 RIFF/PCM/DC/클리핑/경계/페이드 검사도 통과했다. 격리 서버 빌드는 오류 0·기존 nullability 경고 94개이며 서버를 실행하지 않는 `--meteor-only`가 실제 드래프트/보상 필터, 양쪽 좌석 Lv.0-13·현재 주문력·표시·정확한 손패/비용, 광역 보호, 서버 AI, 부활 대기 피드백, 사망 연쇄·결과, 참가자 강화·전체 JSON 복원을 통과했다. 기존 Earthquake/Nuclear/TimedBomb 서버 회귀도 통과했다.

격리 Unity Editor의 Sprite/Audio API 등록과 카드/카탈로그 등록이 통과했다. `asset-import-result.json`과 `card-registration-result.json`의 success=true를 확인했고 네이티브 카탈로그는 실제 39개다. 사용자 카드 PNG에는 이전에 없던 metadata만 네이티브 API로 생성했다.

격리 Unity 6000.3.12f1에서 **837개 고유 케이스의 최종 결과가 PASS**다. 전체 41클래스 829개 검사에서는 826개가 통과하고 새 온라인 큐 fixture 3개가 실패했다. 최종 IsBusy의 메테오 pending 잠금 조건을 포함한 집중 53개 검사에서는 51개가 통과했으며, 테스트의 Play Mode 전환·보드/Animator 준비·관측 시점 전제를 교정한 뒤 온라인 큐 3/3이 통과했다. 마지막 체력 기대값은 표시용 0과 서버의 실제 사망 체력 -20을 구분하도록 고쳤다. 837개는 전체 회귀와 8개 추가 입력/hold 검사를 합친 **중복 제거 집계이며, 마지막에 전체를 한 번 다시 실행한 결과는 아니다**. 제품 코드의 피해 계산이나 Animator 재생을 테스트 편의로 바꾸지 않았다.

메테오 연출 15개, 실제 온라인 큐 3개, 카드 미리보기 10개, 타일 점등 17개, 긴 누르기 6개와 턴 종료 잠금/열람 2개를 포함한다. 실픽셀 미리보기 4장과 양쪽 좌석·2340×1080/1280×720/1080×2340 화면·실제 전장 중심/클릭 비차단을 검증했다. 10대상 체력 라벨의 비행 유지/충돌 갱신, 피격/취소/재바인드, 지연된 폭발 tail과 후속 HP·BattleEnded 스냅샷의 실제 완료 대기, 이전 세대 취소 후 재시작, busy 중 카드 열람을 확인했다.

현재 Unity 입력 827개와 테스트 복사본의 SHA256이 모두 일치한다. 서버의 실제 빌드 입력 197개와 배포 후보 카드 JSON도 현재 소스와 일치한다. 보호 입력 131개 중 기존 130개는 변경되지 않았다. 유일하게 후속 수정된 사용자 Meteor 카드 PNG는 현재 SHA 2F7E2CD8034FB170BCCF3747511AD737D8CF89FE8499FE525F2E12E62C1E3191를 유지했다. 카드 PNG를 쓰는 생성·등록 코드는 없으며 native 메타/카드 연결도 유지한다. 초기 컴파일의 오래된 NUnit API 문제, 신규 fixture 실패와 교정 결과는 별도 최초 실행 로그/XML에 보존했다.

최종 입력/보존/카탈로그/미디어 검사는 verification.json, 케이스별 최종 집계는 aggregate-passing-cases.json이다. 전체 결과 full-829-unity-results.xml, 마지막 집중 결과 final-focused-results.xml, 최종 온라인 큐 결과 final-queue-results.xml을 함께 읽는다. 실제 미리보기는 Tools/Art/Preview/Meteor-{Flight|Impact}-{Player|AI}.png다. 기존 LevelPlay 어댑터는 격리 검증 사본에서만 제외했으며 원본을 변경하지 않았다.

증거는 `TempBuild/MeteorPresentation_20260930/`의 `Packed/packing-report.json`, `reproducibility-report.json`, `anchor-measurements.json`, `Audio/*IndependentValidation.json`, `asset-import-result.json`, `card-registration-result.json`, `server-build.log`, `server-meteor.log`에 보존했다. 실기기 화면·게임 청취, 앱 빌드·설치나 실제 서버 배포·재시작은 이번 작업의 검증 범위에 포함하지 않는다.

유니크 변경 추가 검증(2026-09-30): 기존 정의·native 자산·보유카드 현재 설명 검사 3/3과 오프라인 서버 --meteor-only의 실제 드래프트/보상 경로가 통과했다. 카드 JSON/native 자산에서 희귀도만 변경됐고 기존 그림·VFX·음원·metadata·카탈로그 14개 보호 파일은 그대로다. 증거: TempBuild/MeteorRarityUnique_20260930/verification.json. 앱 빌드/설치나 서버 배포는 하지 않았다.

## 실제 전투씬 연출 재생 수정 — 2026-10-01

실제 Battle_VSlice에는 타일 배경 Canvas와 별도의 유닛 그림 Canvas가 있다. 유닛 그림용 작은 Canvas(빈 타일에서는 비활성)를 전체 화면 기준으로 선택하면서 메테오와 시한폭탄의 배치가 실패했고, 파이어스피어는 타일별 서로 다른 Canvas를 거부해 재생을 시작하지 못했다. 세 연출 모두 타일 배경 앵커의 Canvas를 우선 사용하도록 수정했다. 파이어스피어는 각 타일의 화면 좌표를 연출 Canvas로 투영해 선택한 소유자 전장의 두 타일을 관통하고 끝까지 빠져나간다. 왼쪽으로 이동할 때 180도 회전한 RectMask2D의 범위가 음수가 되어 화면에서 사라지는 추가 오류도 실제 픽셀 검사로 확인했다. 마스크는 양의 축을 유지하고 투사체만 회전하며 비행 좌표의 부호를 맞춰 양방향 재생을 복구했다. 시한폭탄은 활성화 뒤 표시 우선순위 4910을 다시 설정한다.

원본 실제 씬 회귀 6개 실패를 재현한 뒤 수정했다. 최종 격리 Unity 6000.3.12f1의 관련 12개 fixture **321/321 통과**, 실패·skip·누락·컴파일 오류 0. 실제 전투씬 공개 카드 사용·정확한 피해/자원/손패 1회 처리, 양쪽 시전자·대상 전장, 메테오 낙하/폭발, 시한폭탄 중앙 03→02→01→00와 상대 10타일 중심, 파이어스피어의 두 칸 관통·클릭 비차단·취소 및 기존 규칙/온라인 큐를 검증했다. 실제 씬 렌더 PNG 27장은 TempBuild/SpellPlaybackFix_20261001/Captures에 있다. 캡처는 독립 타일/유닛 Canvas를 유지하며 Overlay 출력만 별도 카메라에 연결한 검증 하네스다. 실기기·운영 온라인 경기 검증과 구분한다.

최종 입력 2088개의 소스/검사 사본 SHA가 일치하며 기존 카드 정의·PNG·음원·metadata·씬·Bootstrapper는 보존했다. 최초 실패와 중간 수정 실패도 별도 증거에 보존했다. 증거: TempBuild/SpellPlaybackFix_20261001/verification.json, unity-results.xml, baseline/, after-first-fixes/. 앱 빌드·설치나 서버 배포·재시작은 하지 않았다.