# 시한폭탄 카운트다운과 폭발 연출

2026-09-30 사용자 요청. 전투 규칙은 `battle_rules.md`의 Timed Bomb을 따른다.

## 화면 동작 — 현재 연결 (2026-10-01)

중앙 타이머와 전장 폭발은 실제 TileBackground 앵커의 Canvas를 기준으로 표시한다. 유닛 전용 Canvas가 작거나 비활성이어도 빈 타일 배경 앵커를 사용할 수 있으며, 표시 root 활성화 뒤 overrideSorting과 order 4910을 다시 설정한다.

카드 사용 직후에는 기존대로 폭탄을 예약하고 피해를 주지 않는다. 다음 양쪽 턴 시작마다 화면 중앙에 사용자가 제공한 빨간 시한폭탄이 나타나며, LED 숫자가 `00:00:03 → 00:00:02`, `00:00:02 → 00:00:01`, `00:00:01 → 00:00:00`으로 바뀐다. 마지막 전환 후 시전자 반대편 전장 전체 10타일에서 폭발이 동시에 재생되고 폭발음은 한 번만 들린다. 빈 타일도 폭발 그림을 표시한다.

각 숫자 전환은 0.8초이며, 처음 0.18초 기존 숫자를 보여주고 0.25초 동안 새 숫자로 전환한다. 0이 되면 16컷 폭발을 16fps로 1초 재생하고 마지막 0.2초에 사라진다. 여러 폭탄은 각각 생성 순서대로 처리한다. 내 턴 배너는 중앙 숫자와 겹치지 않도록 연출 뒤에 표시하며 원래 배너 그래픽을 유지한다.

## 실제 편집 파일

| 파일 | 용도 |
|---|---|
| `Assets/Project333/Resources/Project333/SpellEffects/TimedBomb/BombBody.png` | 512×512 투명 폭탄 이미지 |
| `Assets/Project333/Resources/Project333/SpellEffects/TimedBomb/Explosion.png` | 2048×2048, 4×4 배열의 16컷 폭발; 한 컷 512×512 |
| `Assets/Project333/Resources/Project333/SpellEffects/TimedBomb/Audio/TimedBombExplosion.wav` | 0.96초 mono PCM16/44.1kHz 폭발음 |
| `Assets/Project333/Runtime/Presentation/Battle/TimedBombPresentationVfx.cs` | 위치, 재생시간, 폭발 크기와 음량 |
| `Assets/Project333/Runtime/Presentation/Battle/BombTimerGraphic.cs` | LCD 가림, LED 숫자, 원본 좌표 매핑 |

첨부 원본 `C:/Users/youndonghyeok/Downloads/시한폭탄.png`는 713×374이며 보존했다. 동일 그림을 종횡비대로 축소하여 512px 셀의 `(16,130,480,252)` 사각형에 넣었다. 원본에 쓰인 `00:00:33`은 그림 파일에 남아 있지만 실제 표시에서는 LCD 안쪽만 짙은 적갈색 메시로 덮고 빨간 7세그먼트 숫자를 그린다. 폭탄 몸체는 변경하지 않는다. LCD 영역의 위치를 바꾼 그림으로 교체할 때는 코드의 원본 좌표 매핑도 함께 조절해야 한다.

폭발 이름은 `Explosion_00`~`Explosion_15`, 왼쪽→오른쪽/위쪽→아래쪽 순서이며 pivot은 셀 중심 `(256,256)`이다. Point 필터, mipmap 없음, 무압축, 알파 투명을 사용한다. 각 그림은 셀 안에 완전히 들어간다. 타일 크기의 1.5배를 기준으로 화면 안에 들어오도록 제한하며, 빈 타일을 포함한 상대 전장의 모든 타일 중심에 배치한다.

효과음은 짧은 압력음과 낮은 몸통, 부드러운 잔향으로 합성했다. peak −14.000/RMS −32.277dBFS, one-shot gain 0.30이며 기존 전역 음량을 한 번 적용한다. 타이머 beep는 추가하지 않았다. 공통 UnitHit와 기존 피해 반응은 유지한다. WAV를 같은 경로에 교체할 때 `.meta`를 보존하고 Play Mode를 재시작한다.

## 데이터와 재접속

게임의 상태를 보고 숫자를 추측하지 않고 실제 TurnStart 감소 이벤트에 연결한다. 로컬 인간/AI는 같은 공유 서비스의 일시적 이벤트를 사용한다. 서버는 `TimedBombCountdown` 이벤트에 폭탄 인덱스, 시전자, 대상 진영, 감소 전후 숫자를 담아 해당 폭발 피해 앞에 보낸다. 온라인 로컬 좌석 변환에서도 이 필드를 보존한다.

상태 복원만으로 이전 숫자나 폭발을 재생하지 않는다. 비용, 강화 저장 피해, 물리 방어/봉인/무적, 사망 처리와 기존 턴 순서는 변경하지 않는다. 연출은 전투 피해를 다시 적용하지 않으며, 취소·비활성화·씬 종료 시 화면과 소리를 정리한다.

## 제작 자료

- 이미지 생성 도구: 내장 `image_gen`, 투명 배경. 정확한 생성 요청과 원본 경로는 `Tools/Art/TimedBombExplosionPrompt.json`에 보존했다.
- 원본: `Tools/Art/Source/TimedBomb/BombReference.png`, `ExplosionGenerated.png`.
- 일정한 셀 분할과 규격 변환: `Tools/Art/PackTimedBombSprites.ps1`.
- Unity Sprite Editor Data Provider와 AudioImporter 등록: `Tools/Art/ImportTimedBombPresentationAssets.cs`.
- 재현 가능한 음원 합성 및 독립 RIFF/PCM 검사: `Tools/Art/PrepareTimedBombExplosionAudio.py`, `ValidateTimedBombExplosionAudio.ps1`, `TimedBombExplosionAudio.md`.
- Exporter는 Assets 밖에 출력한다. 검토한 결과만 실제 에셋에 반영한다. 이후 사용자의 그림 편집 위에 오래된 원본을 자동으로 다시 적용하지 않는다.

## 검증과 반영

Unity 6000.3.12f1 격리 프로젝트에서 관련 25개 클래스 420개 검사가 최초 통과했다. 이후 실제 PNG를 검사하면서 숫자 메시의 CanvasRenderer 누락을 발견했다. 필수 컴포넌트를 명시하고 화면 픽셀 검사와 고정 시점 캡처를 추가했다. 최종 시한폭탄 검사 15/15가 통과했다. 나머지 405개 검사의 입력은 같으며, 초기 405개와 최종 15개를 합한 420개 결과를 `aggregate-verification.json`에 기록했다. 전체 420개를 수정 후 한 번에 다시 실행한 결과는 아니다.

검사는 다음을 포함한다.

- 실제 화면의 03/02/01/00 LED와 원본 33 가림, 두 진영 각각의 상대 10타일 폭발, 한 번만 재생되는 음원.
- 로컬 플레이어·AI 및 온라인 좌석 변환, 여러 폭탄의 순서, 취소/비활성화/종료, 상태 복원 후 과거 연출 미재생, 내 턴 배너 지연과 종료 패킷 억제.
- 폭탄 피해와 후행 낙진·방화벽·생화학폭탄을 구분하는 순서, 첫 폭탄에서 발생한 사망/연쇄 피해의 경계.
- 기존 턴·이동·공격·방어·온라인/공유 서비스 회귀 검사. Nuclear 연출 9개와 안내창 10개도 통과한 기존 결과에 포함된다.

서버를 실행하지 않는 `--timed-bomb-only` 검사는 양쪽 시전자, 3회 감소, 저장 강화 피해, wire 숫자/정체성, 다중 폭탄, 혼합 효과 순서, 치명 피해, JSON 복원/양쪽 뷰를 통과했다. 서버 격리 컴파일 오류 0개, 기존 nullability 경고 94개.

최종 Unity 적용 입력 746개와 서버 소스/데이터 입력 234개가 검사 입력과 SHA256으로 일치했다. 원본 첨부 파일과 보존 복사본은 그대로이며 이미지/음원 재생성 해시와 독립 PCM 검사도 통과했다. 증거 위치는 `TempBuild/TimedBombPresentation_20260930/`의 `verification.json`, `aggregate-verification.json`, XML/로그, 입력 해시 목록이다. 정상 렌더 캡처는 `Tools/Art/Preview/TimedBomb*.png`에 보존했다.

화면 검사는 실제 2×5 타일 배치를 재현한 격리된 렌더 하네스이다. 전체 전투 씬, 실기기 터치/청취를 검증한 것은 아니다. 격리 프로젝트에서만 무관한 LevelPlay 어댑터를 제외했으며 주 프로젝트 설정은 변경하지 않았다.

이 변경은 소스와 에셋 작업이다. 휴대폰에는 새 클라이언트 빌드·설치가 필요하며 온라인 카운트다운에는 업데이트된 서버 이벤트 코드의 배포가 필요하다. 주 Unity Editor와 실행 중 서버는 유지한다. 앱 빌드·설치, 서버 배포·재시작, commit/push는 수행하지 않는다.
## 실제 전투씬 연출 재생 수정 — 2026-10-01

실제 Battle_VSlice에는 타일 배경 Canvas와 별도의 유닛 그림 Canvas가 있다. 유닛 그림용 작은 Canvas(빈 타일에서는 비활성)를 전체 화면 기준으로 선택하면서 메테오와 시한폭탄의 배치가 실패했고, 파이어스피어는 타일별 서로 다른 Canvas를 거부해 재생을 시작하지 못했다. 세 연출 모두 타일 배경 앵커의 Canvas를 우선 사용하도록 수정했다. 파이어스피어는 각 타일의 화면 좌표를 연출 Canvas로 투영해 선택한 소유자 전장의 두 타일을 관통하고 끝까지 빠져나간다. 왼쪽으로 이동할 때 180도 회전한 RectMask2D의 범위가 음수가 되어 화면에서 사라지는 추가 오류도 실제 픽셀 검사로 확인했다. 마스크는 양의 축을 유지하고 투사체만 회전하며 비행 좌표의 부호를 맞춰 양방향 재생을 복구했다. 시한폭탄은 활성화 뒤 표시 우선순위 4910을 다시 설정한다.

원본 실제 씬 회귀 6개 실패를 재현한 뒤 수정했다. 최종 격리 Unity 6000.3.12f1의 관련 12개 fixture **321/321 통과**, 실패·skip·누락·컴파일 오류 0. 실제 전투씬 공개 카드 사용·정확한 피해/자원/손패 1회 처리, 양쪽 시전자·대상 전장, 메테오 낙하/폭발, 시한폭탄 중앙 03→02→01→00와 상대 10타일 중심, 파이어스피어의 두 칸 관통·클릭 비차단·취소 및 기존 규칙/온라인 큐를 검증했다. 실제 씬 렌더 PNG 27장은 TempBuild/SpellPlaybackFix_20261001/Captures에 있다. 캡처는 독립 타일/유닛 Canvas를 유지하며 Overlay 출력만 별도 카메라에 연결한 검증 하네스다. 실기기·운영 온라인 경기 검증과 구분한다.

최종 입력 2088개의 소스/검사 사본 SHA가 일치하며 기존 카드 정의·PNG·음원·metadata·씬·Bootstrapper는 보존했다. 최초 실패와 중간 수정 실패도 별도 증거에 보존했다. 증거: TempBuild/SpellPlaybackFix_20261001/verification.json, unity-results.xml, baseline/, after-first-fixes/. 앱 빌드·설치나 서버 배포·재시작은 하지 않았다.