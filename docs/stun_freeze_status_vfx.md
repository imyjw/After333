# 스턴·빙결 상태 VFX

2026-10-01 사용자 요청. 스턴은 소환물 머리 위에서 금색 별이 타원 궤도로 돌고, 빙결은 본체가 비치는 큰 얼음 결정으로 표시한다. 기존 PNG → AnimationClip → Animator Controller → 카드 Board Animator Controller 연결을 유지한다.

## 현재 규칙과 표시 범위 — 2026-10-01

스턴은 공격·반격·액티브, 빙결은 이에 이동·전열 차단까지 막는다. 지속 시간은 부여 카드/효과에서 지정하며 양쪽 플레이어의 승인된 턴 종료마다 양쪽 전장의 남은 시간을 각각 1 줄인다. 부여한 턴의 종료도 세며 재적용은 기존/새 기간 중 긴 값을 사용한다. 패시브·주문력·방어력·기존 버프는 상태만으로 억제하지 않는다. 현재 카드에 스턴/빙결 부여 효과를 자동으로 추가한 것은 아니다.

`OccupantState`/`EndTurnService`가 지속·해제를 담당하고 `ActionDebuffStatusVfx`/`TileTextView`는 그 현재 상태만 표시한다. 남은 기간·전체 규칙은 [전투 규칙](battle_rules.md#stun-and-freeze-2026-10-01)을 따른다.

## 표시와 애니메이션

- 현재 `OccupantState.IsStunned` / `IsFrozen`를 읽는 별도 UI Image 두 개를 재사용한다. 살아 있고 표시 중인 소환물에만 나오며, 적의 은신을 노출하지 않는다. 스턴과 빙결이 함께 있으면 두 효과를 함께 표시한다. 봉인 중에도 이미 남아 있는 스턴·빙결은 유지된다.
- 스턴 별 8컷을 8fps로 순환한다. 최대 알파는 0.86이다. 실제 그림 영역 위에 배치하므로 원본 프레임 상단의 큰 빈 여백을 머리 위치로 사용하지 않는다.
- 빙결 결정은 한 장의 반투명 픽셀 아트다. 실제 그림 영역보다 폭 1.16배·높이 1.12배로 감싸며, Image 알파는 0.68~0.72를 2초 주기로 부드럽게 조절한다. 원본 결정의 내부 알파가 낮아서 유닛이 비친다.
- 이펙트는 소환물 시각 오브젝트의 이동·크기·좌우 반전을 따라가며 스탯 아래에 표시한다. 두 Image 모두 `raycastTarget=false`다. 추가 효과음은 없다.
- 별 프레임과 얼음 반짝임은 `Time.unscaledTime`을 사용한다. 빙결 중 본체가 멈춰도 이펙트는 재생된다. 같은 RuntimeId의 재접속 객체 교체는 시계를 보존하고 최신 상태를 읽는다.
- 빙결 중에는 본체 Animator의 원래 speed를 보관하고 0으로 만든다. Idle·Attack·Run·피격 등 비사망 재생 요청이 정지 포즈를 처음부터 다시 시작하지 않도록 막는다. 공통 클립 샘플러의 프레임·남은 시간도 진행하지 않는다.
- 빙결 해제 시 원래 speed와 재생 위치를 이어서 사용한다. 타일 비움·새 소환물/컨트롤러 재바인딩·비활성화·파괴에서는 정지 상태를 정리한다. 사망 연출은 정지를 해제하고 Death를 재생하며 상태 VFX를 숨긴다. 전체 게임의 timeScale은 변경하지 않는다.
- UI는 디버프를 적용·해제하거나 HP·자원·행동 제한을 변경하지 않는다. 지속·재적용 규칙은 [전투 규칙](battle_rules.md)의 Stun and Freeze를 따른다.

## 그림 교체와 조절

- 스턴: `Assets/Project333/Resources/Project333/StatusEffects/Stun/OrbitStars.png` — 2048×1024px, 4열×2행, 512×512px 8컷, 이름 `OrbitStars_0..7`.
- 빙결: `Assets/Project333/Resources/Project333/StatusEffects/Freeze/IceCrystal.png` — 512×512px 한 컷, 이름 `IceCrystal_0`.
- 두 PNG는 투명 배경과 네 방향 16px 이상 안전 여백을 확보한다. Native SpriteMultiple/FullRect/중앙 pivot/PPU100/Point/NoMip/Uncompressed/Readable 설정이다. 그림을 교체할 때 규격·프레임 순서·기존 `.meta`를 유지한다.
- 속도·알파·배치 비율은 `Assets/Project333/Runtime/Presentation/Battle/ActionDebuffStatusVfx.cs`에서 조절한다. 본체 일시정지는 `TileTextView.cs`가 관리한다.
- 기존 유닛의 PNG·클립·컨트롤러·표시 배율은 수정하지 않았다. 생성 원본이나 포장 도구로 사용자의 나중 편집본을 다시 덮어쓰지 않는다.

## 실제 그림 영역 캐시

`Assets/Project333/Resources/Project333/StatusEffects/BodyBounds.asset`은 원본 PNG에서 측정한 알파 영역의 좌표만 저장한다. 유닛 스프라이트를 직접 참조하지 않아 이 캐시를 읽는 것만으로 미사용 유닛 텍스처 전체를 로드하지 않는다. 실제 컨트롤러 이름·텍스처/스프라이트 이름·Sprite.rect 조합으로 구분하고 충돌은 제작 단계에서 검출한다. 기존 Unit 텍스처를 Readable로 바꾸지 않는다. 공유 컨트롤러를 사용하는 엘프 장로 등의 카드도 같은 그림 영역을 사용한다.

유닛 PNG/컨트롤러/카드 연결을 수정할 때 Editor의 전용 캐시 작성기가 갱신한다. 수동 캐시 재생성 메뉴와 빌드 전 검증도 제공한다. 캐시가 없는 동적 스프라이트에는 읽기 가능한 알파 또는 Sprite.bounds를 사용한다. 런타임에서는 영역 좌표만 캐시하고 매 프레임 원본 픽셀을 읽지 않는다.

## 제작 자료

- 내장 `image_gen`으로 새 그림을 생성했다. 정확한 두 프롬프트: `Tools/Art/StunFreezeVfxPrompts.json`.
- 원본 보존: `Tools/Art/Source/StunFreezeStatusVfx/OrbitStars-Generated-20261001.png`(1774×887), `IceCrystal-Generated-20261001.png`(1254×1254).
- 포장·native import 도구와 검증 출력은 `Tools/Art/` 및 `TempBuild/StunFreezeVfx_20261001/Export/`에 있다. 포장은 Assets 밖에만 출력하고 native Sprite provider로 분할한다. 원본의 alpha<16인 희미한 가장자리만 정리하며 나머지 투명도를 보존한다.
- 최신 앱 반영을 위한 클라이언트 빌드·설치는 사용자가 한다. 서버 규칙 변경은 없다.

## 검증

Unity 6000.3.12f1 격리 프로젝트에서 최종 **99/99 통과**, 실패·건너뜀·컴파일 오류 0. 새 자산 검사 3개와 실제 Play Mode 연출 검사 11개, 기존 방전·스턴/빙결 규칙·레드드래곤 연출·타일 미리보기 회귀 검사 85개를 포함한다. 네이티브 Animator의 즉시 정지·원래 custom speed/재생 위치 복원, 샘플러 프레임/남은 시간, 방전의 동시 상태 전환, 동일 ID 재접속·새 ID 교체, 양쪽 시점·스턴 단독·두 상태 동시·timeScale=0, 은신/봉인/사망/비활성/파괴와 풀 재사용을 확인했다.

원본/임포트 크기가 다른 BlueDragon도 원본 PNG 알파를 올바른 좌표로 독립 대조했다. 첫 97/99의 실패 2개는 검사 코드가 축소된 native rect를 원본에 바로 적용한 계산과 미사용 source-only 투명 프레임의 Rect.zero를 배제한 검사 조건이었다. 화면의 위치 기준이나 VFX·Unit 그림은 바꾸지 않았다. 실제 카드/컨트롤러 행은 양수 경계·현재 PNG 대조를 유지하며, 투명 Rect.zero는 미사용 unit:* 커버리지 행에만 허용한다.

Native import 두 번에서 PNG·GUID·sprite localID·meta·BodyBounds 데이터가 유지됐다. BodyBounds 1,598행은 Sprite/Texture 참조가 없으며 PNG dependency 0이다. 기존 Unit PNG/import 설정·클립·컨트롤러를 보존했다. 최종 현재 입력 해시와 보호 파일 대조는 `TempBuild/StunFreezeVfx_20261001/verification.json`에 기록한다. 최초 격리 staging의 Editor 중복 사본 컴파일 문제는 사본을 보관 폴더로 옮겨 해결했고, 최종 검사에는 중복 코드가 없다.

실제 네이티브 모션 캡처 9장과 4형상×2owner의 위치 로그는 `Tools/Art/Preview/StunFreezeStatusVfx/`에 있다. 증거: `TempBuild/StunFreezeVfx_20261001/{unity-results.xml,unity-summary.json,unity-tests.log,native-import.json,final-input-hashes.json,verification.json}`. 휴대폰 실기기/전체 온라인 경기 검사는 아니며 클라이언트 빌드·설치, 운영 서버 재시작·배포, 커밋·푸시는 하지 않았다.