# 생화학폭탄 독가스 VFX

2026-09-29 추가. 생화학폭탄이 적용된 상대 전장의 왼쪽 또는 오른쪽 4×2 타일에 초록색 독가스를 표시한다. 빈 타일도 포함한다.

## 동작

- `BoardPresenter.Present`가 전달받은 `BattleState.PersistentEffects`를 기준으로 표시한다. 로컬/온라인 공통이며, 재접속 상태 투영 후에도 같은 영역을 복원한다.
- 타일마다 투명한 가스 이미지 두 겹을 서로 다른 속도로 이동·회전·확대/축소한다. 남은 발동 수 감소가 관측되면 투명도를 잠시 높인다.
- 타일 배경 위, 소환물 이미지와 능력치 아래에 배치한다(가스 Canvas sorting order 240, 기존 소환물 250). 이미지의 raycast와 CanvasGroup 입력을 꺼서 타일 조작을 가로막지 않는다.
- 생성·소멸은 0.4초 페이드. 효과 만료 또는 전투 종료 시 제거하며 중첩되어도 타일마다 두 겹만 유지한다. 중첩에 따른 농도 증가를 제한한다.
- 기존 피해량, 발동 횟수, 서버 규칙 및 공통 피격음은 변경하지 않는다. 독가스는 지속 범위 표시이며 별도 피격 사운드를 추가하지 않는다.

## 수정할 파일

- 이미지: `Assets/Project333/Resources/Project333/SpellEffects/BiochemicalGas.png`
- 동작·크기·투명도: `Assets/Project333/Runtime/Presentation/Battle/BiochemicalGasVfx.cs`의 `Animate`.
- 연결: `Assets/Project333/Runtime/Presentation/Battle/BoardPresenter.cs`.
- 이미지는 투명 배경 PNG로 교체할 수 있다. 기존 `.meta`를 유지한다. 원본은 생성 도구의 1254×1254 출력이며 Unity 임포트 최대 크기는 512로 설정했다. 소환물 애니메이션 프레임이 아닌 독립 VFX 텍스처다.

## 제작 기록

내장 image generation 도구로 신규 생성, `transparent_background: true`. 원본을 프로젝트에 복사하고 Unity에서 크기 제한 및 투명도 합성을 적용했다. 사용 프롬프트:

> Create a production game VFX texture, 512x512 px square, transparent RGBA background. A SINGLE isolated low rolling poison gas cloud patch, vivid emerald green and yellow-green wispy curls with darker forest green translucent body, soft feathered fully transparent edges, light curling toxic mist, viewed at a slight top-down angle for a fantasy card battle board. Restrained stylized painted/pixel-art-compatible 2D game look, readable broad shapes and fine wisps. Cloud occupies centered 85% of canvas; no cropped edges. Semi-transparent cloudy density with transparent holes, no solid opaque backdrop. NO ground, no tile, no characters, no flask, no skull, no symbols, no lettering, no border, no checkerboard drawn into image. This texture will be layered with low opacity and animated by drifting/scaling over board tiles. Exactly one cloud patch, not a sprite sheet.

## 검증

격리 Unity 프로젝트에서 좌/우 8칸, 중첩·만료, 양쪽 시점의 재접속 투영, 입력 비차단, 실제 전투 씬 앵커 연결을 검사했으며 7개 모두 통과했다. `TempBuild/BiochemicalGas_20260929`에 결과와 단순 타일 배경 렌더링 `preview.png`를 저장한다. 이 미리보기는 실기기 전투 캡처가 아니다. 앱 빌드·설치는 사용자가 진행한다.
