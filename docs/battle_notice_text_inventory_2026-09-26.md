# 전투 씬 안내 문구 검토 목록

2026-09-26 최초 조사. 2026-09-29 사용자 지정 54개 항목의 본문을 반영했다. 적용 표시가 없는 항목은 최초 조사 기준이다. 2026-09-29 추가 요청으로 공통 화면 접두어도 제거했다.

## 범위와 읽는 방법

- 전투의 실패/상태 토스트, 재접속 안내, 매칭 안내창을 조사했다. 카드 툴팁·전투 기록·데미지 숫자·일반 HUD/턴 타이머·설정 확인창은 별개다.
- A~I는 수정 의견을 받을 문구 목록이다. 같은 조건의 서버/클라이언트 문구도 별도로 기록했다. 일부 서버 방어 검사는 클라이언트 선검사 때문에 정상 조작에서 표시되지 않을 수 있다. 모든 항목을 실기기에서 재현한 목록은 아니다.
- 2026-09-29: 서버 오류·명령 실패·연결 상태·성공 안내 모두 접두어 없이 본문만 표시한다. 재접속 카운트다운·결과 토스트·매칭 안내창도 본문만 표시한다.
- `{cardId}`, `{coord}`, `{남은 초}`, `{ex.Message}` 등은 실행 중 바뀐다. `cardId`는 표시 이름이 아니라 내부 ID가 그대로 나오는 경우다.
- 서버는 Program.cs의 `command_rejected` 응답에 예외 Message를 그대로 싣고, 클라이언트는 기본 분기에서 그대로 보여준다. 따라서 고정 목록 외 DB/JSON/네트워크/미등록 카드/인자 검증 예외도 표시될 수 있다. 이 가변 오류는 가능한 모든 문자열을 유한한 목록으로 나열할 수 없다.
- H1/H2는 첫 전투 상태 수신 전 특정 분기에서는 토스트 없이 덱 화면으로 복귀한다. `battle_cancelled`, `battle_reconnect_expired`, 일부 골드 던전 만료 오류도 복귀 처리가 우선한다. 아래 서버 원문 부록은 화면 표시로 연결될 수 있는 원문/등록 현황이며, 무조건 그대로 노출되는 문구 목록은 아니다.

## A. 공격

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| A1 | 이번 턴 공격 횟수 소진 | 이 유닛은 이미 공격했습니다. | 2026-09-29 적용 |
| A2 | 소환한 턴에 속공 없이 공격 | 소환한 턴에는 공격할 수 없습니다. | 2026-09-29 적용 |
| A3 | 방전 등 공격 불가 상태 | 이 유닛을 공격 불가능한 상태입니다. | 2026-09-29 적용 |
| A4 | 공격력이 0 이하 | 공격력이 0이하인 유닛은 공격할 수 없습니다. | 2026-09-29 적용 |
| A5 | 공격할 수 없는 건물 | 이 유닛은 공격할 수 없습니다. | 2026-09-29 적용 |
| A6-1 | 은신한 대상 공격 시도 | 은신한 유닛은 지정할 수 없습니다. | 2026-10-12 적용 |
| A6-2 | 봉인된 대상 공격 시도 | 봉인된 대상은 지정할 수 없습니다. | 2026-10-12 적용 |
| A6-3 | 전열 소환물이 보호하는 같은 열의 후열 대상에 근접 공격 시도 | 대상은 전열의 소환물에 의해 보호받고 있습니다. | 2026-10-12 적용 |
| A6-4 | 위 조건 외의 대상 지정 불가 | 대상은 지정 불가 상태입니다. | 2026-10-12 적용 |
| A7 | 공격자 타일이 비어 있음 | 공격자 타일이 비어있습니다. | 2026-09-29 적용 |
| A8 | 공격 대상 타일이 비어 있음 | 공격 대상 타일이 비어있습니다. | 2026-09-29 적용 |
| A9 | 상대 소환물로 공격 시도 | 상대 소환물로 공격할 수 없습니다. | 2026-09-29 적용 |
| A10 | 환술 공격의 무작위 대상 없음 | 공격 가능한 대상이 없습니다. | 2026-09-29 적용 |

A6는 공유 `TargetingService.GetTargetRejectionMessage`에서 현재 공격 가능 여부를 먼저 확인한다. 실제 은신·봉인 상태를 전열 보호보다 우선해서 설명하며, 봉인으로 은신이 비활성화되어 있으면 봉인 문구를 표시한다. 원거리·활성 비행 공격, 비어 있거나 전열 차단을 하지 않는 앞칸 때문에 정상적으로 가능한 후열 공격은 거절하지 않는다. A7/A8 등 앞선 공격 검사는 기존 문구를 유지한다. 서버는 같은 공유 코드를 컴파일하고 `command_rejected`의 Message로 전달하며, 클라이언트는 그 본문을 그대로 표시한다.

## B. 이동·자리 교환

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| B1 | 이동할 타일에 소환물이 없음 | 대상 타일이 비어있습니다. | 2026-09-29 적용 |
| B2 | 상대 소환물 이동 시도 | 상대 유닛은 이동시킬 수 없습니다. | 2026-09-29 적용 |
| B3 | 상태 이상으로 이동 불가 | 이 유닛은 이동 불가 상태입니다. | 2026-09-29 적용 |
| B4 | 이동 불가능한 소환물 | 이 유닛은 이동 불가능합니다. | 2026-09-29 적용 |
| B5 | 보드 밖으로 이동 | 비정상적인 이동입니다. | 2026-09-29 적용 |
| B6 | 상대 소환물 위로 이동 | 상대 소환물 위로 이동할 수 없습니다. | 2026-09-29 적용 |
| B7 | 교환 상대가 상태 이상으로 이동 불가 | 대상 유닛은 이동 불가 상태입니다. | 2026-09-29 적용 |
| B8 | 교환 상대가 이동 불가 | 대상 유닛은 이동 불가 상태입니다. | 2026-09-29 적용 |

## C. 카드·소환 공통

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| C1 | 자원 부족: 드래그 단계 | 자원이 부족합니다. | 2026-09-29 적용 |
| C2 | 자원 부족: 서버 카드/마법 사용 검증 | 자원이 부족합니다. | 2026-09-29 적용 |
| C3 | 카드 미선택 | 카드가 선택되지 않았습니다. | 2026-09-29 적용 |
| C4 | 손패에 카드 없음: 클라이언트 | 해당 카드가 핸드에 없습니다. | 2026-09-29 적용 |
| C5 | 손패에 카드 없음: 서버 일반 카드 | 해당 카드가 핸드에 없습니다. | 2026-09-29 적용 |
| C6 | 손패에 카드 없음: 서버 마법 | 해당 카드가 핸드에 없습니다. | 2026-09-29 적용 |
| C7 | 점유된 타일에 소환 | 해당 타일에는 소환물이 이미 존재합니다. | 2026-09-29 적용 |
| C8 | 보드 밖에 카드 사용 | 해당 위치에 카드를 사용할 수 없습니다. | 2026-09-29 적용 |
| C9 | 보드 밖에 소환 | 해당 위치에 소환할 수 없습니다. | 2026-09-29 적용 |
| C10 | 마스터 유닛 소환 시도 | 마스터 유닛은 소환 불가능합니다. | 2026-09-29 적용 |
| C11 | 다른 소유자로 소환 시도 | Cannot summon an occupant for a different owner. | |
| C12 | 카드 정의 없음 | Card '{cardId}' definition was not found. | |
| C13 | 현재 전장 조건에서 카드 사용 불가(기본 문구) | 해당 카드를 사용할 수 없는 상태입니다. | 2026-09-29 적용 |
| C14 | 자기 자신에게 사용하는 마법이 아님 | 사용 가능한 대상이 아닙니다. | 2026-09-29 적용 |
| C15 | 카드 사용 실패(기본 문구) | 카드 사용이 실패했습니다. | 2026-09-29 적용 |

## D. 마법·로봇 합체

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| D1 | 피해 마법의 대상 타일이 비어 있음 | 대상 타일이 비어있습니다. | 2026-09-29 적용 |
| D2 | 특수 마법의 대상 타일이 비어 있음 | 대상 타일이 비어있습니다. | 2026-09-29 적용 |
| D3 | 봉인된 대상에 마법 | 봉인된 유닛은 대상이 될 수 없습니다. | 2026-09-29 적용 |
| D4 | 은신한 적에게 단일 대상 마법 | 은신한 유닛은 대상이 될 수 없습니다. | 2026-09-29 적용 |
| D5 | 천라지망 대상이 마스터이거나 유닛이 아님 | 이 대상에게는 사용할 수 없습니다. | 2026-09-29 적용 |
| D6 | 파이어월 대상 지정 오류 | Firewall requires an enemy row target. | |
| D7 | 파이어월 행이 보드 밖 | Firewall target row is outside the opponent board. | |
| D8 | 생화학폭탄 영역 지정 오류 | Biochemical Bomb requires an enemy 4x2 area target. | |
| D9 | 생화학폭탄을 상대 보드 외에 사용 | Biochemical Bomb can only target the opponent board. | |
| D10 | 생화학폭탄 영역이 좌/우 4×2가 아님 | Biochemical Bomb target must be the left or right 4x2 area. | |
| D11 | 고독: 아군 빈 타일 없음(클라이언트) | 고독을 사용하려면 내 필드에 빈 타일이 필요합니다. | |
| D12 | 고독: 아군 빈 타일 없음(서버) | 고독을 사용하려면 내 필드에 빈 타일이 필요합니다. | 2026-09-29 적용 |
| D13 | 고독: 복종시킬 상대 유닛 없음 | 복종시킬 수 있는 상대방 유닛이 없습니다. | |
| D14 | 고독: 대상이 적 유닛이 아님 | 대상이 적 유닛이 아닙니다. | 2026-09-29 적용 |
| D15 | 고독: 사망·봉인·은신 등 유효하지 않은 대상 | 유효한 대상이 아닙니다. | 2026-09-29 적용 |
| D16 | 환술: 적용할 상대 유닛 없음 | 환술을 부여할 수 있는 상대방 유닛이 없습니다. | |
| D17 | 환술: 대상이 적 유닛이 아님 | 대상이 적 유닛이 아닙니다. | 2026-09-29 적용 |
| D18 | 환술: 사망·봉인·은신 등 유효하지 않은 대상 | 유효한 대상이 아닙니다. | 2026-09-29 적용 |
| D19 | 로봇 합체: 시작 조건 부족(클라이언트) | 살아 있는 아군 로봇 유닛이 2기 이상 필요합니다. | |
| D20 | 로봇 합체: 선택 개수 부족(클라이언트) | 합체할 살아 있는 아군 로봇 유닛을 2기 이상 선택하세요. | |
| D21 | 로봇 합체: 시작 조건 부족(서버) | Robot Fusion requires at least two living Robot units on the caster's field. | |
| D22 | 로봇 합체: 선택 개수 부족(서버) | Robot Fusion requires at least two living Robot units. | |
| D23 | 로봇 합체: 같은 로봇 중복 선택 | Robot Fusion cannot select the same Robot more than once. | |
| D24 | 로봇 합체: 잘못된 소환물 선택 | Robot Fusion target {coord} must be a living Robot unit on the caster's field. | |
| D25 | 로봇 합체: 선택을 끝내기 전 다른 행동 | Complete the pending Robot Fusion selection before performing another action. | |
| D26 | 로봇 합체: 선택이 이미 진행 중 | A Robot Fusion selection is already pending. | |
| D27 | 로봇 합체: 선택 시작 전 확정 요청 | Robot Fusion must enter selection mode before it can resolve. | |

## E. 액티브

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| E1 | 액티브 비용 부족 | 자원이 부족합니다. | 2026-09-29 적용 |
| E2 | 자기 턴 아님·이미 사용함·상태 이상 등 사용 조건 미충족 | 지금은 액티브 효과를 사용할 수 없습니다. | |
| E3 | 버튼 선택 후 소환물이 교체됨 | 선택한 소환물이 변경되었습니다. | |

## F. 전투 단계·멀리건

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| F1 | 상대 턴에 행동 명령 | 자신의 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F2 | 행동 단계 외 명령 | 자신의 행동 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F3 | 공격 단계 제한 | 자신의 행동 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F4 | 공격 주체 턴 제한 | 자신의 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F5 | 카드 사용 단계 제한(클라이언트) | 자신의 행동 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F6 | 카드 사용 단계 제한(서버) | 자신의 행동 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F7 | 카드 사용 주체 턴 제한 | 자신의 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F8 | 마법 사용 단계 제한 | 자신의 행동 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F9 | 마법 사용 주체 턴 제한 | 자신의 턴에만 행동할 수 있습니다. | 2026-09-29 적용 |
| F10 | 행동 단계 외 턴 종료 | 자신의 턴에만 턴을 종료할 수 있습니다. | 2026-09-29 적용 |
| F11 | 전투 종료 후 명령 | Commands cannot be executed after the battle has ended. | |
| F12 | 연출 처리 중 카드 사용 | Battle is still resolving animations. | |
| F13 | 멀리건 단계가 아님 | 멀리건 단계가 아닙니다. | 2026-09-29 적용 |
| F14 | 이미 멀리건 확정 | 멀리건이 이미 확정되었습니다. | 2026-09-29 적용 |
| F15 | 선택한 멀리건 카드가 손패에 없음 | Selected mulligan card does not exist in the player's current hand. | |
| F16 | 멀리건 교체 카드를 뽑을 덱 부족 | Deck does not contain enough cards to complete the mulligan redraw. | |
| F17 | 전투 없이 항복 시도 | 진행 중인 전투가 없습니다. | |
| F18 | 종료된 전투에 항복 시도 | 이미 종료된 전투입니다. | |

## G. 연결·매칭·재접속

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| G1 | 서버 연결 시작 | 표시하지 않음 | 2026-09-29 숨김 |
| G2 | 매칭 시작 토스트 | 상대를 찾는 중입니다. | |
| G3 | 매칭 대기창 | 상대를 찾는 중입니다... | |
| G4 | 매칭 성공 | 상대를 찾았습니다! | |
| G5 | 매칭 취소 진행 | 매칭을 취소하는 중입니다... | |
| G6 | 전투 준비 완료 | 표시하지 않음 | 2026-09-29 숨김 |
| G7 | 재접속 시작 | 전투에 재접속하는 중입니다. | |
| G8 | 내 재접속 남은 시간 | 전투에 재접속하는 중... ({남은 초}초) | |
| G9 | 상대 재접속 대기 | 상대의 재접속을 기다리는 중...{남은 초} | |
| G10 | 내 재접속 완료 | 전투에 재접속했습니다. | |
| G11 | 상대 재접속 완료 | 상대 연결이 끊겼다가 재접속했습니다. | |
| G12 | 상대 재접속 시간 만료로 승리 | 상대가 도망쳤습니다 | |
| G13 | 서버 연결 실패 | 서버 연결에 실패했습니다. 나중에 다시 시도해주세요. | |
| G14 | 연결 시간 초과(매칭 실패창) | 서버 연결 시간이 초과되었습니다. 나중에 다시 시도해주세요. | |
| G15 | 연결 실패창의 저장된 기본 문구 | 서버 연결에 실패했습니다.<br>PowerShell에서 서버를 켠 뒤 다시 시도해 주세요. | |
| G16 | 연결 종료 원문 | Server connection closed: {closeReason} | |
| G17 | 연결 오류 원문 | Server connection error: {error} | |
| G18 | 연결/좌석 미완료 상태에서 행동 | Cannot send online battle command until this client is connected and seated. | |
| G19 | 명령 전송 실패 | Online command send failed: {ex.Message} | |
| G20 | 연결/좌석 미완료 상태에서 항복 | 항복하려면 서버 연결과 좌석 배정이 완료되어야 합니다. | |
| G21 | 항복 전송 실패 | 항복 요청 전송 실패: {ex.Message} | |
| G22 | 연결/좌석 미완료 상태에서 멀리건 | 멀리건을 확정하려면 서버 연결과 좌석 배정이 완료되어야 합니다. | |
| G23 | 멀리건 전송 실패 | 멀리건 전송에 실패했습니다: {ex.Message} | |

## H. 서버 오류의 한글 치환

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| H1 | battle_reconnect_required | 진행 중인 전투가 있습니다. 덱 화면에서 재접속해 주세요. | |
| H2 | already_in_pve_match | 이미 진행 중인 PVE 전투가 있습니다. 기존 전투 창을 닫은 뒤 재접속해 주세요. | |
| H3 | already_in_pvp_match | 이미 이 계정으로 진행 중인 PVP 전투 연결이 있습니다. 기존 전투 창을 닫거나 잠시 후 다시 시도해 주세요. | |
| H4 | battle_deck_required | PVP에 사용할 서버 저장 덱이 없습니다.<br>드래프트를 완료한 뒤 다시 시도해 주세요. | |
| H5 | battle_deck_not_found | 서버에서 현재 드래프트 덱을 찾지 못했습니다.<br>드래프트 화면으로 돌아가 덱 상태를 다시 확인해 주세요. | |
| H6 | invalid_battle_deck_size | PVP 전투 덱은 정확히 33장이어야 합니다.<br>드래프트를 완료한 뒤 다시 시도해 주세요. | |
| H7 | client_version_mismatch | 서버 버전과 현재 클라이언트 버전이 맞지 않습니다.<br>최신 게임 클라이언트로 다시 실행해 주세요. | |
| H8 | account_required / invalid_session | 서버 계정 로그인이 필요합니다.<br>시작 화면에서 다시 로그인한 뒤 시도해 주세요. | |
| H9 | db_not_configured | 서버 DB 연결이 설정되지 않았습니다.<br>PowerShell 서버의 PROJECT333_DB_CONNECTION 설정을 확인해 주세요. | |
| H10 | match_full | 매칭 방이 이미 가득 찼습니다.<br>잠시 후 다시 시도해 주세요. | |
| H11 | 본문 없는 미분류 오류 | 서버에서 전투 요청을 처리하지 못했습니다. | |

## I. 클라이언트 준비·지원 오류

| 번호 | 발생 조건 / 종류 | 현재 본문 | 바꿀 문구 |
|---|---|---|---|
| I1 | 전투 연결 담당 컴포넌트 미설정 | Battle bootstrapper is not assigned. | |
| I2 | 전투 미준비 | Battle is not ready. | |
| I3 | 미지원 마법 종류 | Card '{cardId}' uses an unsupported spell type in the current slice. | |
| I4 | 타일 사용 미지원 카드 | Card '{cardId}' cannot be used on a board tile in the current slice. | |
| I5 | 로봇 합체가 아닌 카드로 합체 시작 | Card '{cardId}' is not Robot Fusion. | |
| I6 | 비어 있는 전투 명령 | Cannot send an empty online battle command. | |
| I7 | 로그인 없이 PVP 매칭 | PVP matchmaking requires a server account login. | |

## J. 서버·공통 서비스 원문 대조표

중복 호출 위치와 내부 방어 검사를 포함한다. 한글 치환/자동 복귀가 있는 코드는 A~I 및 처리 경로 설명을 우선한다. `Hero stat selector…`, `Unsupported…`, 카드 타입/명령 불일치 등은 일반적인 플레이 안내가 아니라 내부 오류다. 아래 번호로도 변경 의견을 줄 수 있다.

| 번호 | 원문 | 코드 또는 발생 위치 |
|---|---|---|
| J1 | Attacks can only be declared during the main phase. | [AttackService.cs:48](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J2 | Only the active player can attack. | [AttackService.cs:53](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J3 | Attacker tile is empty. | [AttackService.cs:63](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J4 | Defender tile is empty. | [AttackService.cs:69](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J5 | Target is not legal for this attacker. | [AttackService.cs:76](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J6 | Hero stat selector returned an invalid index. | [AttackService.cs:124](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J7 | HuanShu attack has no legal random target. | [AttackService.cs:164](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J8 | HuanShu random target selector returned an invalid index. | [AttackService.cs:170](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J9 | Cannot attack with an enemy occupant. | [AttackService.cs:226](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J10 | This occupant cannot attack while drained. | [AttackService.cs:231](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J11 | This occupant cannot attack on the turn it was summoned. | [AttackService.cs:236](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J12 | This occupant has no attacks remaining this turn. | [AttackService.cs:241](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J13 | Occupants with 0 attack cannot attack. | [AttackService.cs:246](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J14 | This building cannot attack. | [AttackService.cs:251](../Assets/Project333/Runtime/Application/Services/AttackService.cs) |
| J15 | Cannot move from an empty tile. | [MoveService.cs:21](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J16 | Cannot move an enemy occupant. | [MoveService.cs:26](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J17 | This occupant cannot move in its current state. | [MoveService.cs:31](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J18 | This occupant cannot move. | [MoveService.cs:36](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J19 | Cannot move outside the board. | [MoveService.cs:41](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J20 | Cannot move onto an enemy occupant. | [MoveService.cs:53](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J21 | Cannot swap with an occupant in its current state. | [MoveService.cs:58](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J22 | Cannot swap with an immobile occupant. | [MoveService.cs:63](../Assets/Project333/Runtime/Application/Services/MoveService.cs) |
| J23 | The specified card is not a unit card. | [PlayCardService.cs:47](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J24 | The specified card is not a building card. | [PlayCardService.cs:62](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J25 | The selected hand card could not be consumed. | [PlayCardService.cs:110](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J26 | The selected hand card could not be consumed. | [PlayCardService.cs:167](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J27 | Cards can only be played during the main phase. | [PlayCardService.cs:210](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J28 | Only the active player can play cards. | [PlayCardService.cs:215](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J29 | The specified card is not in the player's hand. | [PlayCardService.cs:221](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J30 | Cannot play a card outside the board. | [PlayCardService.cs:227](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J31 | The player cannot afford this card. | [PlayCardService.cs:235](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J32 | Cannot summon onto an occupied tile. | [PlayCardService.cs:244](../Assets/Project333/Runtime/Application/Services/PlayCardService.cs) |
| J33 | Cannot summon an occupant for a different owner. | [SummonService.cs:24](../Assets/Project333/Runtime/Application/Services/SummonService.cs) |
| J34 | Master units cannot be summoned. | [SummonService.cs:29](../Assets/Project333/Runtime/Application/Services/SummonService.cs) |
| J35 | Cannot summon outside the board. | [SummonService.cs:35](../Assets/Project333/Runtime/Application/Services/SummonService.cs) |
| J36 | Cannot summon onto an occupied tile. | [SummonService.cs:40](../Assets/Project333/Runtime/Application/Services/SummonService.cs) |
| J37 | Scripted spell effect '{definition.EffectId}' does not accept ordered Robot targets. | [SpellService.cs:174](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J38 | Robot Fusion must enter selection mode before it can resolve. | [SpellService.cs:183](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J39 | Spells can only be cast during the main phase. | [SpellService.cs:200](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J40 | Only the active player can cast spells. | [SpellService.cs:205](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J41 | Damage spell target tile is empty. | [SpellService.cs:232](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J42 | This scripted spell does not use a board target. | [SpellService.cs:332](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J43 | This scripted spell requires a target. | [SpellService.cs:341](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J44 | Firewall requires an enemy row target. | [SpellService.cs:357](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J45 | Magic Amplification Circle does not use a board target. | [SpellService.cs:372](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J46 | Nuclear does not use a board target. | [SpellService.cs:384](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J47 | Timed Bomb does not use a board target. | [SpellService.cs:393](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J48 | Biochemical Bomb requires an enemy 4x2 area target. | [SpellService.cs:407](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J49 | Power Bank does not use a board target. | [SpellService.cs:423](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J50 | Mana Stone does not use a board target. | [SpellService.cs:438](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J51 | Mana Stone Bundle does not use a board target. | [SpellService.cs:453](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J52 | Robot Fusion uses ordered friendly Robot targets. | [SpellService.cs:468](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J53 | Gu requires an enemy Unit target. | [SpellService.cs:477](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J54 | HuanShu requires an enemy Unit target. | [SpellService.cs:493](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J55 | Unsupported scripted spell effect id '{definition.EffectId}'. | [SpellService.cs:507](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J56 | A Robot Fusion selection is already pending. | [SpellService.cs:521](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J57 | Robot Fusion requires at least two living Robot units on the caster's field. | [SpellService.cs:526](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J58 | Scripted spell target tile is empty. | [SpellService.cs:568](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J59 | Cheonra Jimang can only target non-Master units. | [SpellService.cs:573](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J60 | Gu requires an empty tile on the caster's field. | [SpellService.cs:603](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J61 | Gu can only target a living, non-Sealbound, non-Hiding enemy Unit. | [SpellService.cs:608](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J62 | HuanShu can only target a living, non-Sealbound, non-Hiding enemy Unit. | [SpellService.cs:645](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J63 | Firewall can only target a row on either player's board. | [SpellService.cs:670](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J64 | Firewall target row is outside the opponent board. | [SpellService.cs:676](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J65 | Biochemical Bomb can only target the opponent board. | [SpellService.cs:734](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J66 | Biochemical Bomb target must be the left or right 4x2 area. | [SpellService.cs:739](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J67 | Sealbound occupants cannot be targeted by spells. | [SpellService.cs:785](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J68 | Hiding units cannot be targeted by enemy single-target spells. | [SpellService.cs:790](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J69 | The spell card is not in the caster's hand. | [SpellService.cs:869](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J70 | The player cannot afford this card. | [SpellService.cs:874](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J71 | The selected spell card could not be consumed. | [SpellService.cs:880](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J72 | A card definition provider is required for definition-based spell casting. | [SpellService.cs:890](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J73 | The specified spell card does not match the expected definition type. | [SpellService.cs:894](../Assets/Project333/Runtime/Application/Services/SpellService.cs) |
| J74 | Robot Fusion requires at least two living Robot units. | [RobotFusionRules.cs:58](../Assets/Project333/Runtime/Application/Services/RobotFusionRules.cs) |
| J75 | Robot Fusion cannot select the same Robot more than once. | [RobotFusionRules.cs:69](../Assets/Project333/Runtime/Application/Services/RobotFusionRules.cs) |
| J76 | Robot Fusion target {coord} must be a living Robot unit on the caster's field. | [RobotFusionRules.cs:75](../Assets/Project333/Runtime/Application/Services/RobotFusionRules.cs) |
| J77 | 지금은 액티브 효과를 사용할 수 없습니다. | [ActiveEffectService.cs:43](../Assets/Project333/Runtime/Application/Services/ActiveEffectService.cs) |
| J78 | 선택한 소환물이 변경되었습니다. | [ActiveEffectService.cs:46](../Assets/Project333/Runtime/Application/Services/ActiveEffectService.cs) |
| J79 | Commands cannot be executed after the battle has ended. | [BattleCommandProcessor.cs:46](../Assets/Project333/Runtime/Application/Services/BattleCommandProcessor.cs) |
| J80 | Only the active player can execute commands. | [BattleCommandProcessor.cs:51](../Assets/Project333/Runtime/Application/Services/BattleCommandProcessor.cs) |
| J81 | Complete the pending Robot Fusion selection before performing another action. | [BattleCommandProcessor.cs:77](../Assets/Project333/Runtime/Application/Services/BattleCommandProcessor.cs) |
| J82 | Unsupported battle command type '{command.GetType().Name}'. | [BattleCommandProcessor.cs:176](../Assets/Project333/Runtime/Application/Services/BattleCommandProcessor.cs) |
| J83 | This command can only be executed during the main phase. | [BattleCommandProcessor.cs:194](../Assets/Project333/Runtime/Application/Services/BattleCommandProcessor.cs) |
| J84 | Mulligan can only be applied during the mulligan phase. | [MulliganService.cs:76](../Assets/Project333/Runtime/Application/Services/MulliganService.cs) |
| J85 | This player has already used their mulligan. | [MulliganService.cs:84](../Assets/Project333/Runtime/Application/Services/MulliganService.cs) |
| J86 | Selected mulligan card does not exist in the player's current hand. | [MulliganService.cs:96](../Assets/Project333/Runtime/Application/Services/MulliganService.cs) |
| J87 | Deck does not contain enough cards to complete the mulligan redraw. | [MulliganService.cs:121](../Assets/Project333/Runtime/Application/Services/MulliganService.cs) |
| J88 | Turns can only end during the Main phase. | [EndTurnService.cs:39](../Assets/Project333/Runtime/Application/Services/EndTurnService.cs) |
| J89 | Target tile is empty. | [ShielderService.cs:50](../Assets/Project333/Runtime/Application/Services/ShielderService.cs) |
| J90 | Sealbound occupants cannot be targeted. | [ShielderService.cs:54](../Assets/Project333/Runtime/Application/Services/ShielderService.cs) |
| J91 | JoinMatch requires a MatchId. | missing_match_id · [Program.cs:1493](../Server/Project333.PvpServer/Program.cs) |
| J92 | JoinMatch requires a valid login session. | missing_session_token · [Program.cs:1513](../Server/Project333.PvpServer/Program.cs) |
| J93 | PVP matchmaking requires PROJECT333_DB_CONNECTION for account authentication. | db_not_configured · [Program.cs:1522](../Server/Project333.PvpServer/Program.cs) |
| J94 | An authenticated connection cannot change its account. | battle_participant_mismatch · [Program.cs:1543](../Server/Project333.PvpServer/Program.cs) |
| J95 | An existing battle connection cannot change its authenticated account. | battle_participant_mismatch · [Program.cs:1555](../Server/Project333.PvpServer/Program.cs) |
| J96 | PVP matchmaking requires an authenticated account. | account_required · [Program.cs:1561](../Server/Project333.PvpServer/Program.cs) |
| J97 | Return to the existing PvP battle before starting another battle. | battle_reconnect_required · [Program.cs:1570](../Server/Project333.PvpServer/Program.cs) |
| J98 | Return to the existing PvE battle before starting another battle. | battle_reconnect_required · [Program.cs:1590](../Server/Project333.PvpServer/Program.cs) |
| J99 | This account already has an active PvE connection. Wait for disconnect detection or reconnect to that battle. | already_in_pve_match · [Program.cs:1597](../Server/Project333.PvpServer/Program.cs) |
| J100 | The previous battle is no longer active. Return to the run screen. | battle_cancelled · [Program.cs:1627](../Server/Project333.PvpServer/Program.cs) |
| J101 | This account is already connected to PvP match {connectedPvpSession.MatchId}. Close the existing connection or wait for reconnect grace. | already_in_pvp_match · [Program.cs:1635](../Server/Project333.PvpServer/Program.cs) |
| J102 | A challenge admission can only join its AI battle. | ai_challenge_request_invalid · [Program.cs:1649](../Server/Project333.PvpServer/Program.cs) |
| J103 | This challenge admission does not match the account, battle, run and deck. | ai_challenge_not_available · [Program.cs:1660](../Server/Project333.PvpServer/Program.cs) |
| J104 | This challenge is no longer active. Refresh its recorded result. | battle_cancelled · [Program.cs:1669](../Server/Project333.PvpServer/Program.cs) |
| J105 | Reconnect using the existing challenge admission. | ai_challenge_required · [Program.cs:1690](../Server/Project333.PvpServer/Program.cs) |
| J106 | Finish the reserved Gold Dungeon before starting normal matchmaking. | ai_challenge_pending · [Program.cs:1701](../Server/Project333.PvpServer/Program.cs) |
| J107 | PVP matchmaking requires a saved draft run and deck. | battle_deck_required · [Program.cs:1712](../Server/Project333.PvpServer/Program.cs) |
| J108 | PVP matchmaking failed. Please try again. | matchmaking_failed · [Program.cs:1794](../Server/Project333.PvpServer/Program.cs) |
| J109 | The previous PVE battle has ended or was canceled. Return to the run screen. | battle_reconnect_expired · [Program.cs:1815](../Server/Project333.PvpServer/Program.cs) |
| J110 | The requested battle purpose differs from its server admission. | battle_participant_mismatch · [Program.cs:1827](../Server/Project333.PvpServer/Program.cs) |
| J111 | PVP matchmaking state could not be saved. Please try again. | matchmaking_persistence_failed · [Program.cs:1934](../Server/Project333.PvpServer/Program.cs) |
| J112 | Only JoinMatch and ClientCommand messages are supported by the test server. | unsupported_message · [Program.cs:2030](../Server/Project333.PvpServer/Program.cs) |
| J113 | Join this match with a valid login session before sending battle commands. | join_required · [Program.cs:2037](../Server/Project333.PvpServer/Program.cs) |
| J114 | Command actor {session.FormatSeatForLog(command.ActorId)} does not match assigned seat {connection.AssignedOnlineSeatId}. | actor_mismatch · [Program.cs:2043](../Server/Project333.PvpServer/Program.cs) |
| J115 | The ServerAI seat is controlled by the server in this prototype. | ai_seat_server_controlled · [Program.cs:2055](../Server/Project333.PvpServer/Program.cs) |
| J116 | Command player token does not match this connection. | player_token_mismatch · [Program.cs:2066](../Server/Project333.PvpServer/Program.cs) |
| J117 | Command account id does not match this authenticated connection. | account_mismatch · [Program.cs:2078](../Server/Project333.PvpServer/Program.cs) |
| J118 | Battle commands cannot resolve until all required players have joined and the battle has started. | battle_not_started · [Program.cs:2088](../Server/Project333.PvpServer/Program.cs) |

## 토스트로 오인하기 쉬운 문구

- `전투가 종료되었거나 취소되어 덱 화면으로 돌아갑니다.`는 SetStatus 후 즉시 화면 전환하는 경로다. 현재 별도 토스트를 띄우지 않는다.
- `액티브: 골드 -1, 전력 +2`, `로봇 합체 대상을 선택하세요.`, `로봇 합체가 완료되었습니다.`, `항복했습니다.`, `Sent online…` 성공 문구는 인터랙션 상태/기록에 저장하며 같은 실패 토스트의 직접 호출이 아니다.
- 적 필드에 유닛 카드를 놓기, 자기 시전 마법을 타일에 드롭하기 등은 FailPlayerCardUseSilently 또는 ShouldShowCardUseFailureToast에 의해 토스트 없이 무효화되는 경로가 있다. 현재 표시되지 않는 문구를 새 안내로 추가하려면 별도 동작 변경이다.
- 연결 복구 중에는 개별 연결 오류 토스트 대신 G8의 재접속 카운트다운을 계속 표시한다.

## 주요 소스

- [BattleBootstrapper.cs](../Assets/Project333/Runtime/Presentation/Battle/BattleBootstrapper.cs): 토스트 접두어, 매칭 안내, 카드 실패 표시.
- [OnlineBattleConnectionTester.cs](../Assets/Project333/Runtime/Presentation/Online/OnlineBattleConnectionTester.cs): 서버 오류 한글 치환, 연결/재접속 안내 및 복귀 우선 분기.
- [BattlePlayerInputController.cs](../Assets/Project333/Runtime/Presentation/Battle/BattlePlayerInputController.cs): 드래그 시작 실패와 조건별 토스트 억제.
- [AttackService.cs](../Assets/Project333/Runtime/Application/Services/AttackService.cs), [SpellService.cs](../Assets/Project333/Runtime/Application/Services/SpellService.cs), [ActiveEffectService.cs](../Assets/Project333/Runtime/Application/Services/ActiveEffectService.cs): 실제 행동 거부 사유.
- [Program.cs](../Server/Project333.PvpServer/Program.cs): WebSocket Error 응답과 command_rejected 전달.

다음 단계는 사용자가 지정한 문구에 맞춰 표시를 수정하는 것이다. 이 문서 작성 시점에는 코드/앱/운영 서버에 문구 변경을 적용하지 않았다.

- 2026-09-29: 초기 접속의 연결 시작·연결 완료 후 복원 중·서버 전투 준비 완료 토스트는 표시하지 않는다. 실제 자동 재접속 카운트다운과 연결 실패 안내는 유지한다.
