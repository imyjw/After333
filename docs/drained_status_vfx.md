# 방전 디버프 전기 VFX

2026-10-01 제작. 방전 중인 소환물 주변에 약한 푸른 전기가 지직거리는 8컷 이펙트를 추가한다. 캐릭터의 기존 PNG → AnimationClip → Animator Controller → 카드 Board Animator Controller 연결은 유지하며, 전기만 별도 UI Image로 겹쳐 표시한다.

## 재생 조건과 동작

- 현재 타일의 OccupantState.IsDrained가 켜져 있고 살아 있는 소환물에 표시한다. 전투 상태 복원과 같은 RuntimeId의 객체 교체도 현재 객체를 다시 읽는다. 같은 RuntimeId의 상태 갱신 중에는 전기 재생 시간을 보존하고, 새 소환물이나 방전 재적용 때만 다시 시작한다. 규칙상 방전되는 건물도 같은 표시를 사용한다.
- 8컷을 12fps로 약 0.667초 재생하고 0.75초 쉰다. 각 재생의 시작·끝 0.1초는 부드럽게 나타났다 사라지며 최대 알파는 0.55다. 화면을 가리는 큰 번개나 지속적인 번쩍임 대신 짧은 조각을 사용한다.
- 유닛의 현재 시각 오브젝트를 따라 이동·좌우 반전·반응형 크기 조절에 맞춰 표시한다. 원본 프레임의 아래쪽 몸통 영역에 배치하고 얼굴·HP/ATK 표시를 덜 가린다. 실제 Image 영역과 preserveAspect를 계산하며 폭 0.74·높이 0.50·세로 중심 0.35 비율을 사용한다.
- 방전 해제·망각에 따른 방전 해제·타일 비움·사망·Death 연출·봉인·표시 숨김·컴포넌트 비활성화 시 전기를 끈다. 적의 은신을 VFX로 노출하지 않는다.
- 전기 Image는 raycastTarget=false이며 카드 미리보기·타일 조작을 가로막지 않는다. 소환물과 같은 Canvas 안에서 스탯 표시보다 아래에 그린다.
- 프레임 재생과 페이드는 unscaledTime을 사용한다. 타일마다 Image 하나를 재사용하고 스프라이트 배열을 캐시한다. 새 효과음은 추가하지 않는다.
- UI는 기존 방전 상태를 읽기만 한다. HP·자원·공격/이동 제한·방전 판정·게이트웨이·서버 규칙은 변경하지 않는다.

## 그림과 수정 경로

실제 게임 자산: `Assets/Project333/Resources/Project333/StatusEffects/Drained/ElectricArcs.png`와 같은 이름의 `.meta`.

- 전체 PNG 2048×1024px, 4열×2행, 개별 프레임 512×512px. 위 행 왼쪽부터 ElectricArcs_0..3, 아래 행 ElectricArcs_4..7이다.
- 배경은 투명하다. 여덟 프레임 모두 네 방향 16px 이상 안전 여백과 중앙 96×96px 투명 영역을 확보했다. 생성 원본에서 희미한 alpha<16 안개만 최종 출력에 정리했고, 모든 alpha≥16 전기 조각을 같은 배율·중심으로 포장했다. 캐릭터 접지 프레임을 수정한 작업은 아니다.
- Unity SpriteMultiple/FullRect/중앙 pivot(256,256)/PPU100/Point/NoMip/Uncompressed/Readable. 네이티브 GUID는 `42d136589f83981428232ad9cc74e7d4`이다.
- 그림 교체 시 위 규격과 순서를 유지해서 PNG를 편집한다. `.meta`를 삭제하지 않는다. 최초 생성 자료나 포장 도구로 이후 수작업 편집본을 덮어쓰지 않는다.
- 밝기·속도·휴지는 `Assets/Project333/Runtime/Presentation/Battle/DrainedStatusVfx.cs`의 MaximumAlpha·FramesPerSecond·RestDuration·FadeDuration에서 조절한다. TileTextView가 상태·이동·현재 네이티브 Sprite 갱신 뒤 Refresh를 호출한다.
- 앱에 반영하려면 사용자가 최신 클라이언트를 빌드·설치한다. 이 작업에는 서버 규칙 변경이 없다.

## 제작 자료

- 내장 image_gen으로 새 그림을 생성했으며 정확한 프롬프트는 `Tools/Art/DrainedStatusVfxPrompts.json`에 보관했다.
- 생성 원본: `Tools/Art/Source/DrainedStatusVfx/ElectricArcs-Generated-20261001.png`. 원본 1536×1024/투명 PNG를 그대로 보존했다.
- 검증용 출력·픽셀 검사·반복 출력 SHA: `TempBuild/DrainedStatusVfx_20261001/Export/`.
- `Tools/Art/PackDrainedStatusVfx.cs/.ps1`은 Assets 밖에만 내보내는 포장 도구다. `StageDrainedStatusVfxAssets.ps1`은 기존 TempBuild 검증 프로젝트에만 새 자산을 배치하고, `ImportDrainedStatusVfxAssets.cs`는 native sprite provider로 분할한다. 현재 PNG를 자동으로 재생성하는 런타임 도구가 아니다.
- 생성 원본 SHA256: `450D3571606875536141B3A106576C0D519F686ED4F0517D18D69C02397AEEA6`.
- 실제 ElectricArcs PNG SHA256: `F5D4B310D0737D064AC291CF9BA8AB8DC5FD70DA3C133CEFF59FA18D9B9D1BF8`.

## 검증

Unity 6000.3.12f1의 기존 격리 프로젝트에서 최종 **69/69 검사 통과**, 실패·건너뜀 0이다. 새 에셋 검사 3개와 실제 Play Mode 전환·렌더 검사 10개를 포함한다. 양쪽 시점의 wire 복원, 같은 ID/다른 ID 교체, 방전 해제·망각·봉인·은신 방어·사망·비활성/파괴, timeScale=0의 프레임/페이드/휴지, 위치·크기 변경과 풀 재사용을 확인했다. 기존 상태·온라인 projector·공격/반격·A-212·개방 방주·마나 대포 회귀 검사 56개도 통과했다.

첫 검사 52/55의 실패 3개는 검사 타일의 비활성 초기화와 기존 Image를 갱신 전에 검사한 대기 문제였다. 상태·controller·RuntimeId 검사를 유지한 채 fixture를 실제 활성 타일 표시 경로에 맞췄다. 같은 ID snapshot의 재생 시계 보존 수정 후 전체 관련 범위를 다시 실행한 위 69/69가 최종 결과다.

Native import를 두 번 실행해 GUID·여덟 sprite localID·PNG·meta가 유지됨을 확인했다. 최종 소스/자산 1,660개가 실제 검사 입력과 SHA256 일치하고, 기존 입력 1,645개와 보호한 그림·오디오·카드 자산 1,129개가 변경되지 않았다. 전투 수치·규칙·카드 JSON·기존 캐릭터 모션·공통 UnitHit는 보존했다.

실제 화면 8개는 `Tools/Art/Preview/DrainedStatusVfx/`에 보관했다. 마나 대포·광마·A-301 native controller의 서로 다른 형상에서 약한 전기 위치를 열어 확인했다. 증거는 `TempBuild/DrainedStatusVfx_20261001/{unity-results.xml,unity-tests.log,native-import.json,final-input-hashes.json,verification.json}`이다. 전체 온라인 경기·휴대폰 실기기 확인은 아니며 앱 빌드/설치·운영 서버 실행·커밋/푸시는 하지 않았다.