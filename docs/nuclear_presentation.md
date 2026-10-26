# 뉴클리어 카드 등록과 폭발 연출

현재 카드 수치(2026-10-20): 전력13, 즉시 고정 피해100+강화 레벨. 시전 당시 상대 전장의 유닛·마스터에게만 낙진을 부여하며 이후 소환에는 상속하지 않는다. 카드 미리보기의 고유 툴팁은 공통 `낙진`만 표시한다. [판정 상세](nuclear_fallout.md).

2026-09-30 사용자 제작 카드 이미지 등록 및 덱 빌딩·게임 보상·시전 연출 추가.
전투 수치와 낙진 규칙은 [nuclear_fallout.md](nuclear_fallout.md)를 따른다.

## 카드 출현

- 사용자 이미지 `Assets/Project333/Resources/Project333/CardArtwork/Nuclear.png`와 기존 `.meta`를 그대로 보존했다.
- `Assets/Project333/ScriptableObjects/StarterTen/Cards/Nuclear.asset`을 등록하고 `StarterTenCardCatalog.asset`에 한 번 추가했다. 카드 이미지는 기존 `CardArtworkLibrary`에서 읽는다.
- 정식 `cards.json`의 Nuclear `includeInDraft`·`includeInRewards`는 모두 true다. 드래프트 첫 레전더리 선택과 기존 랜덤 보상 후보에 포함되며 기존 희귀도 확률을 따른다. 첫 선택 이후 레전더리를 제외하는 기존 드래프트 규칙은 유지한다.
- 최초 Nuclear 등록 당시 드래프트 대상 37종, 실제 랜덤 보상 대상 28종, 전체 정의 44종, 강화 가능 33종/불가 11종이었다. 후속 트랩·카드 추가를 포함한 현재 집계는 [카드 목록](card_catalog.md)을 따른다.
- 전력 10, 즉시 적 전장 전체 고정 피해 100+강화 레벨, 사용 당시 적 유닛/마스터 낙진 +1 (2026-10-08: 이후 소환물 제외), 건물 낙진 제외, 양쪽 턴 시작마다 중첩당 고정 피해 10은 변경하지 않았다.

## 애니메이션과 VFX — 현재 연결 (2026-10-01)

실제 상대 전장의 TileBackground 앵커 Canvas를 우선 사용하고 전장 경계를 계산한다. 카드/유닛 그림의 작은 129.6×129.6 Canvas를 전체 폭발의 화면 기준으로 쓰지 않는다. root를 부모에 연결하고 활성화한 뒤 overrideSorting=true, order 4906을 다시 설정한다. 아래 자산·재생 규격은 유지한다.

| 항목 | 값 |
|---|---|
| 실제 편집 파일 | `Assets/Project333/Resources/Project333/SpellEffects/Nuclear/MushroomExplosion.png` |
| 시트 / 프레임 | 2048×2048, 4×4 배열, 16컷 각각 512×512 |
| 재생 순서 | 왼쪽→오른쪽, 위쪽→아래쪽, `MushroomExplosion_00`~`15` |
| 재생 | 9fps, 약 1.78초, 마지막 0.35초에 사라짐 |
| 폭발 발생 지점 | 시전자 반대편 전장의 전체 타일 경계 중앙 |
| 그림 기준점 | 512px 셀의 아래쪽에서 32px 높이, x=256px |
| 표시 크기 | 상대 전장 경계의 긴 변×1.1에서 시작, 화면 여백 2% 안에 들어오도록 제한 |
| 이미지 설정 | 투명 PNG, Point 필터, mipmap 없음, 무압축, PPU100 |

발화→노란 주황색 폭발→거대한 버섯구름→짙은 연기와 불씨→소멸 순서다.
16컷 모두 그림이 자기 512px 셀 안에 들어간다. 마법 이펙트의 상승·확산·소멸 움직임은 유지하며 각 컷을 따로 가운데 정렬하지 않았다.

`NuclearExplosionVfx.cs`가 한 개의 비입력 차단 오버레이를 재사용한다. 타일의 월드 경계를 루트 Canvas 좌표로 계산하여 화면 크기·회전·양쪽 시전자에 대응한다. 실제 전투 화면은 가로 2칸/세로 5칸으로 표시되므로 짧은 가로 폭에만 크기를 종속시키지 않는다. 2340×1080 기준 미리보기에서 VFX 셀은 약552.96px, 씬 기본 유닛 표시 사각형은 약304.56px(96×1.35/ref80)이다. 이미지 안의 투명 여백과 유닛별 포즈에 따라 실제 보이는 그림 크기는 달라진다.

기존 마법 프레임 연출 경로에 연결했다. 유닛의 PNG→AnimationClip→Animator Controller→카드 Board Animator Controller 연결에는 변경이 없다.

- 로컬 인간 시전은 실제 Gateway 사용 성공 후 시작한다. 로컬 AI도 같은 효과를 상대 전장에 재생한다.
- 온라인에서는 서버가 보낸 실제 SpellCast 이벤트의 시전자를 사용한다. 양쪽 시전이 있으면 순서대로 재생한다. 표시 코드가 전투 피해를 새로 적용하지 않는다.
- 비용 부족·실패·연출 중 중복 입력에서는 새 폭발이 발생하지 않는다. 상태 복원이나 낙진 틱만으로 시전 폭발을 추측하지 않는다.
- 일시정지 중에도 unscaled 시간으로 재생한다. 취소·비활성화·씬 종료 시 이미지와 소리를 정리한다. 이전 프레임의 지연을 새 애니메이션 재생 시간으로 세지 않는다.

## 폭발 효과음과 교체

실제 파일: `Assets/Project333/Resources/Project333/SpellEffects/Nuclear/Audio/NuclearExplosion.wav`.

2026-09-30 사용자가 제공한 `dragon-studio-nuclear-explosion-386181.mp3`의 0.000–1.680초 폭발 구간으로 교체했다. mono PCM16/44.1kHz, 20ms/180ms 페이드이며 peak −16.505dBFS/RMS −29.298dBFS다. 평균 음량은 이전 수준에 맞췄으며 one-shot gain 0.30과 기존 AudioListener 설정은 그대로다. 원본 7.575510초와 이전 합성 음원은 보존했다. 현재 연출이 약1.7778초에 음원도 정지하므로 짧은 편집본이 끝 페이드까지 자연 종료한다. 공통 UnitHit와 다른 음원은 변경하지 않았다.

다른 효과음으로 바꾸려면 같은 WAV 경로의 파일을 교체하고 `.meta`는 보존한다. Unity Play Mode를 다시 시작하고 휴대폰에는 새 클라이언트를 빌드·설치해야 한다. 상세 편집·수치·재현 절차는 `Tools/Art/NuclearExplosionAudio.md`에 기록했다.

## 제작 자료와 편집

- 이미지 생성: 내장 image_gen, 투명 배경. 정확한 요청과 원본 위치: `Tools/Art/NuclearExplosionPrompt.json`.
- 보존 원본: `Tools/Art/Source/Nuclear/MushroomExplosionGenerated.png`.
- 프레임 규격 변환: `Tools/Art/PackNuclearExplosionSprites.ps1`. 원본 1254px의 4×4 셀을 일정하게 분할하여 각 512px 셀의 480px 영역에 넣었다. 원본보다 새로운 세부 묘사를 추가하는 변환은 아니다.
- Unity API 가져오기: `Tools/Art/ImportNuclearExplosionAssets.cs`. Sprite Editor Data Provider의 기능을 확인하고 16개 rect/이름/기준점과 PCM import를 설정한다. 카드 등록 도구는 `Tools/Art/ImportNuclearRegistration.cs`다.
- 그림을 편집할 때 시트 규격·컷 순서·공통 기준점과 `.meta`를 보존한다. 기존 스프라이트 이름/GUID/fileID를 유지한다. 과거 생성 원본/가져오기 도구를 이후 작가 편집 위에 무조건 다시 적용하지 않는다.
- 이미지·음원 exporter는 Assets 밖으로만 출력한다. 검토한 결과만 실제 에셋으로 반영한다.
- 실제 배치 계산과 씬 표시 배율을 반영한 렌더 참고: `Tools/Art/Preview/NuclearExplosionPreview.png`.

## 검증과 반영

격리 Unity6000.3.12f1에서 이번 뉴클리어 관련 21개 클래스의 **392개 고유 검사 통과**. 최초22개 클래스401개 중400개가 통과했고, 예전 제외 플래그 검사와 실제 씬 배율 미리보기를 보정하여 Nuclear55+새 연출9=64/64를 다시 통과했다. 이 단계까지401개가 모두 통과했으며 생산 런타임은 동일하다.

이후 별도로 편집된 최신 A-301 Attack/BeAttacked/Death PNG를 격리에 복사해 관련18개를 재검사했다. 뉴클리어 연출9/9, A-3018/9로17/18 통과. 현재 A-301 Attack.png는3072×512(6컷)이지만 기존 clip/meta는8컷을 참조하므로 `RegisteredClipsUseFullCellsAndShortQuietLaserSound`가 실패한다. 뉴클리어 변경에 따른 실패가 아니며 사용자의 이미지·클립은 보존했다. 이 별도 불일치는 남아 있다. 전체 최신 결과는400/401이다.

검사 범위: 카드 자산/그림/카탈로그, 드래프트 출현, 강화와 설명, 현재 피해·낙진 회귀, 양쪽 시전자/온라인 이벤트, 상태 복원 비재생, 실패·중복 방지, 프레임 순서·길이·투명도·화면 적합성, 일시정지 재생, 취소·파괴·비활성화 정리, 원래 공통 피격음/A-301 회귀. 렌더는 실제 배치 계산을 이용한 격리 빈 씬의 참고 이미지이며 전체 전투 씬·휴대폰 청취 검사는 아니다.

오프라인 서버 Nuclear 검사에서 실제 드래프트 생성기·실제 보상 필터/생성기와 양 좌석 Lv.0~13 피해/복원/낙진 검증이 통과했다. 기존 수치 버프와 마법 증폭진 검사도 통과했다. 격리 출력 서버 컴파일은 오류0, 기존 nullability 경고94다. 실행 중 서버·DB·HTTP 리스너는 사용하지 않았다.

현재/격리 입력727개와 서버 입력233개의 SHA256이 일치한다. 스테이징729개 중 재생에 쓰지 않는 A-301 원본 `.aseprite.meta` 두 개는 원본을 가져오지 않아 격리 Unity가 제거했으므로 비교에서 제외했다.

검증 증거: `TempBuild/NuclearPresentation_20260930/`의 `unity-results.xml`, `unity-rerun.xml`, `latest-art-tests.xml`, 세 로그, 입력 해시, `server-build.log`, `server-nuclear.log`, `asset-import-result.json`, `verification.json`. 그림 규격·재현 해시와 독립 PCM 검사도 기록했다. 오디오 자료는 `TempBuild/NuclearExplosionPresentation_20260930/Audio/`.

주 Unity Editor와 실행 중인 서버는 그대로 두었다. 앱 빌드/설치·서버 배포/재시작·commit/push는 수행하지 않았다. 사용자 클라이언트 빌드와 온라인 서버 배포를 통해 실제 기기에 반영한다.

2026-09-30 녹음 교체 검증: 새로운 WAV의 독립 RIFF/PCM·경계·clipping·음량 검사와 반복 변환 SHA256이 통과했다. `.meta`와 재생 코드는 보존했다. 위 Unity/서버 결과는 이전 작업의 검증이며 이번 동일 포맷 음원 교체에서는 다시 실행하지 않았다. 증거: `TempBuild/NuclearRecordedExplosion_20260930/`.


## 실제 전투씬 재생 수정 — 2026-10-01

Battle_VSlice에서 작은 유닛 그림 Canvas(129.6×129.6)를 폭발 영역으로 선택해 viewport 적합 계산이 실패하고 Play가 0을 반환하는 문제를 재현했다. TileBackground 앵커의 부모 Canvas를 우선 선택하도록 수정했다. 이어서 실제 씬 검사에서 활성화 후 overrideSorting=false로 바뀌는 문제를 확인해, 활성화 뒤 overrideSorting=true/정렬 순서4906을 재적용한다.

상대 전장 중앙의 점화·버섯구름 프레임 진행·양쪽 시전자 전환/재사용·취소와 음원 정리를 확인했다. 공개 시전에서 즉시 고정 피해100·유닛/마스터 낙진·건물 낙진 제외, 비용/카드의 한 번 처리도 검증했다. 기존 사용자 폭발 녹음·그림·메타·카드 수치·피해/낙진 규칙·9fps와 재생 시간은 보존했다.

지진과 관련 기존 마법을 포함한 격리 Unity13개 fixture **고유 검사 233 개 모두 통과**(최종 미해결 실패/skip/누락/컴파일 오류0). 전체230/233 및 검사 준비 코드 수정 후15/16·13/14·14/14 실행을 집계하며 코드·연출 그림·음원 입력은 동일하다. 각 검사 증거는 verified-test-summary/cases에 연결했다. 실제 씬 렌더13장(640×480), 입력2100 개 source/copy SHA 일치. 증거: TempBuild/AreaSpellPlaybackFix_20261001/verification.json 및 XML/log/Captures. 수정 전 반환값0과 Canvas 선택만 고쳤을 때의 정렬 실패도 baseline/attempts에 보존했다.

클라이언트 연출 소스 수정이며 사용자 앱 빌드/설치·본 씬 저장·서버 배포/재시작·commit/push는 하지 않았다. 이 연출 수정 자체에는 서버 변경이 없다.
시작 시점과 비교해 별도로 변경된 오크 족장·뉴클리어 카드 그림 PNG는 외부 변경으로 기록하고 현재 원본을 보존했다. 최종 입력에는 새 원본을 반영했다. 뉴클리어 카드 그림은 전체 검사 전부터 최신 버전이며 지진·뉴클리어 연출 프레임과 음원은 변경되지 않았다. 상세 external-source-change.json.
