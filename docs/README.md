# After333 게임 문서 안내

2026-10-26 공통 진입점: [AGENTS.md](../AGENTS.md) · [PROGRESS.md](../PROGRESS.md) · [두 에이전트 협업](agent_collaboration.md). 현재 구현과 운영 확인을 구분해 읽고, 아래 날짜별 기록은 당시 증거로 보존한다.

최근 인계 갱신: **2026-10-26 KST**. [10-24 이후 개발 인계](antigravity_handoff.md)에서 현재 동작·수정 위치·검증/배포 구분과 새 에이전트의 시작 순서를 확인한다. 아래 전체 대조 이력은 **2026-10-20 KST**다. [현재 프로젝트 상태](current_project_status.md)에서 게임 규칙·소스 구현·실제 운영 이전/시험·백업·남은 확인을 구분한다. [전체 문서 대조 기록](documentation_audit_2026-10-20.md)에 갱신/유지 범위를 기록했다. 사용자 결정과 전투 규칙 → 게임 개요 → 드래프트 → 메타의 우선순위를 유지한다.

## 현재 기준

| 항목 | 현재 내용 |
| --- | --- |
| 카드 정의 | 60종: 유닛 27·건물 7·마법 26 |
| 네이티브 카드 자산·덱 빌딩 | 각각54종 / 네이티브 미등록 공유 정의6종 |
| 강화 | 가능 44종, 불가 16종, 최대 Lv.13; 카드별 확정 증가량 적용 |
| 강화 비용 | 목표 Lv.1/2/3/4/5는 동일 카드·계정 골드 각각 3/6/10/20/30, Lv.6~13은 각각 33. [비용표·서버 적용](meta_rules.md#card-upgrade-costs) |
| 랜덤 카드 보상 | 실제 대상 41종; includeInRewards=true 43종과 구분. 트랩: 리턴·서먼 트랩 포함, 미러 필드·반응 장갑·아이언 돔 제외 |
| 상점 카드팩 | 계정 골드 33 / 보상 대상 카드 10장 독립 추첨·중복 가능 / 커먼·언커먼·레어·유니크·레전더리 40·30·18·9·3%; [구매·복구](shop_card_packs.md) |
| 런 | 티켓 3장으로 시작, 드래프트 33장, 일반 전투 33승 또는 3패까지 유지 |
| 일반 전투 | 사람 우선 매칭, 20초 후 AI 대체 |
| 골드 던전 | 티켓 3개로 시작한 런당 최대 3회·자정 초기화 없음, 승리 계정 골드 10, 런 승패에 영향 없음 |
| 시작 손패·턴 자원 | 멀리건5장, 시작 골드3/4 선배정, 첫 턴 추가 기본 골드 없음·이후+3, 기본 드로우1 |
| 튜토리얼 | 실제 게임 UI를 재사용하는 로컬6단계, 필수 실습 완료 후 진행 |
| 운영 | 서울 Lightsail+Caddy+PostgreSQL18, 운영DB project333_live. 소스 최신0025; 명시적 운영0024 출력은 과거 기록이며 현재 release/migration 재확인 필요 |
| 재접속 | 일반 전투·골드 던전 모두 서버가 연결 종료를 감지한 뒤 70초 유예 |

최근 변경에는 오크 전사·오크 광전사 ATK 20, 비공개 트랩 5종, 스턴·빙결, 마나 대포의 공격 비용, 오크 족장 오라, 현재 골드에 따른 개방 방주 공격력, 보호막 2종과 전체 전장/열 마법 연출이 포함된다. 자세한 값과 조건은 아래 전용 문서를 따른다.

## 게임 규칙과 카드

| 찾는 내용 | 문서 |
| --- | --- |
| 프로젝트 소개·게임 흐름 | [프로젝트 소개](../README.md), [게임 개요](game_design.md) |
| 턴·타일·공격·피해·상태·트랩 | [전투 규칙](battle_rules.md), [특수효과 참고](special_effects.md), [액티브](active_effects.md) |
| 전체 카드·현재 스펙·강화 표 | [전체 목록](card_catalog.md), [카드 상세](card_details.md), [종족](unit_races.md) |
| 새 카드에 필요한 항목·등록 | [카드 제작 체크리스트](card_authoring_checklist.md) |
| 덱 빌딩·런·강화·보상 | [드래프트](draft_rules.md), [메타 규칙](meta_rules.md) |
| 일반 전투·골드 던전·재접속 | [전투 모드](battle_modes.md), [온라인 전투](online_pvp_prototype.md) |
| AI 평가·방어 배치·덱 구성 | [AI 판단](pve_ai.md), [AI 덱](pve_ai_decks.md) |
| 원거리 마스터 보호 트랩 | [아이언 돔](iron_dome.md) |
| 뉴클리어 낙진 | [낙진 규칙](nuclear_fallout.md) |

## 최근 개발을 이어받을 때

[Antigravity 인계](antigravity_handoff.md)에서 상대 카드 공개·덱 글자·개별 승패 음량·튜토리얼 이전·임시 복제·공통 스펠/트랩·덱 편집 보존·드로우/캐시·반격 복귀·턴 만료 전달을 한 번에 확인한다. 조절/검사 상세는 [손패 스펠](hand_spell_use_presentation.md), [드로우/캐시](hand_draw_presentation.md), [반격 Idle](counterattack_presentation.md), [턴 만료 서버](turn_timeout_handoff.md)에 있다.

## 화면·애니메이션·음향

| 찾는 내용 | 문서 |
| --- | --- |
| 접히는 손패·타일 점멸·턴 잠금 | [전투 손패](compact_battle_hand.md) |
| 카드 미리보기·버프/디버프·효과 | [미리보기 툴팁](occupant_status_tooltips.md) |
| 상대방 카드 사용 자동 공개 | [대기열·합체 이벤트·종료 순서](opponent_card_reveal.md) |
| 보유카드 상세·종족·뒤집는 강화표 | [보유카드 동작](owned_cards_ui.md), [레이아웃](owned_cards_pixel_ui.md) |
| 안내창·글꼴·내 턴 배너 예외 | [안내창 디자인](notice_ui.md) |
| 마스터·배경음악·효과음 볼륨 | [오디오 설정·저장·새 효과음 연결](audio_settings.md) |
| 유닛 타일 이동 효과음 | [첨부 MP3·재생 조건·음량 조정](battle_movement_audio.md) |
| 전투 종료 승리·패배 배너와 애니메이션 | [전투 결과 배너](battle_outcome_presentation.md) |
| Play 전 현재 씬 UI 편집 | [다섯 씬 자동 준비·공통 메뉴·편집 범위](editable_scene_layouts.md) |
| 시작·드래프트·덱 빌딩·상점 UI | [시작](start_menu_pixel_ui.md), [드래프트](draft_pixel_ui.md), [덱 빌딩](deck_building_pixel_ui.md), [상점](shop_pixel_ui.md) |
| 카드팩 구매·개봉·5×2 결과·희귀도 오오라 | [상점 카드팩](shop_card_packs.md) |
| 멀리건 선택·초록 오오라·붉은 X·덱에서 등장 | [멀리건 화면](mulligan_presentation.md) |
| 로컬 튜토리얼·개방 방주 대본·단계별 실습 | [튜토리얼 대본](tutorial_script_ko.md), [구현·편집 안내](tutorial_implementation.md) |
| 스프라이트 규격·실제 controller·크기 | [애니메이션 제작](unit_animation_authoring.md), [현재 클립 값](unit_animation_snapshot.md), [소환물 표시 크기](summon_display_scales.md) |
| 공통 피격음·배경 음악 | [피격음](common_hit_sound.md), [배경 음악](background_music.md) |
| 방전·스턴·빙결 VFX | [방전](drained_status_vfx.md), [스턴·빙결](stun_freeze_status_vfx.md) |
| 용·로봇·개방 방주·마나 대포 | [레드드래곤](red_dragon_presentation.md), [블루드래곤](blue_dragon_presentation.md), [A-212](a212_presentation.md), [A-301](a301_presentation.md), [광마](gwangma_presentation.md), [개방 방주](gaebang_chief_presentation.md), [마나 대포](mana_cannon_presentation.md) |
| 전체 전장·열·지속 영역 마법 | [메테오](meteor_presentation.md), [지진](earthquake_presentation.md), [뉴클리어](nuclear_presentation.md), [시한폭탄](timed_bomb_presentation.md), [파이어스피어](fire_spear_presentation.md), [파이어월](firewall_presentation.md), [독가스](biochemical_gas_vfx.md) |
| 일회성 보호막 | [플라즈마 쉴드](plasma_shield_presentation.md), [마나쉴드](mana_shield_presentation.md) |
| 트랩 발동 배리어·폭발 | [반응 장갑](reactive_armour_presentation.md) |

## 계정·운영·검증

- [현재 상태](current_project_status.md), [Lightsail 운영 명령·백업](lightsail_operations.md)
- [서버 안내](../Server/Project333.PvpServer/README.md), [계정 저장 설계](account_persistence_db_design.md), [PostgreSQL 구현](postgresql_db_implementation_plan.md)
- [한 계정의 단일 로그인](single_device_login.md): 2026-10-21 소스·0025 추가, 최신 로그인 우선·기존 전투 재접속; 운영0024 출력은 이전 당시 기록. 사용자 로그인 교체 관측과 소스0025를 구분하고 현재 운영 적용 여부는 재확인
- [구매·강화 재시도](account_operation_retries.md), [선물함](gift_inbox.md), [광고 보상](rewarded_ads.md)
- [빌드 식별](client_build_identity.md), [모바일 자격 증명 보관](mobile_secure_credential_storage.md), [Google 인증](google_auth_setup.md)
- [배포 준비](deployment_readiness_checklist.md), [전투 흐름 점검](battle_flow_stabilization_checklist.md), [비공개 테스트 안내](public_pvp_tester_quickstart_ko.md), [스모크 체크리스트](public_pvp_smoke_test_checklist.md)
- [HTTPS/WSS](https_wss_reverse_proxy_setup.md), [MYBOX 백업](mybox_database_backup.md), [DB 복원](postgres_restore_runbook.md)
- [작업 이어가기](codex_handoff.md), [사용자 결정 색인](conversation_decisions.md), [현재 구현 계약과 과거 문구의 예외](implementation_contract.md)

## 과거 기록을 읽는 방법

`docs/archive/`, 날짜가 붙은 감사·부하·회귀·보고서 문서, 각 기능 문서의 날짜별 검증 절은 당시 구현과 확인 범위를 보존한 기록이다. 과거 카드 수·수치·60초 재접속·초기 애니메이션 프레임 수가 남아 있어도 현재 규칙으로 적용하지 않는다. 현재 규칙은 해당 문서의 최신 상태 절과 원본 데이터/코드를 함께 확인한다. 과거 실패·수정·PASS 결과를 새로 실행한 결과처럼 바꾸지 않는다.

10-26 최근 인계 및10-20 전체 대조는 모두 문서 작업이다. 이번 인계는 [최근 개발 문서](antigravity_handoff.md),10-20 대조의 범위는 아래 기록을 따른다. 게임 코드·카드 자산·씬·키·DB·예약 작업은 변경하지 않았고 앱 빌드·설치/운영 재시작도 하지 않았다. 대조 목록과 근거·남은 범위는 [문서 점검 기록](documentation_audit_2026-10-20.md)을 따른다. 10월1일 증거는 당시 기록으로 보존한다.