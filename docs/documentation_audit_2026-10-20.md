# 프로젝트 문서 동기화 기록 — 2026-10-20

## 범위와 권한

저장소의 직접 작성 문서(루트 소개/지침, docs 최상위·아트 프롬프트, 서버 README, Tools/Art·Audio 안내 및 Art/Concepts 프롬프트)를 목록화하고 현재 소스/데이터·이전 검증·사용자 운영 결과와 대조했다. `docs/archive`, `Tools/Art/Archive`, 외부 패키지/서드파티 README, Library·TempBuild의 생성 결과는 현재 안내로 재작성하지 않는다. 문서만 수정하며 소스·씬·카드 자산·서비스·DB·키·예약 작업·배포·앱 빌드/설치를 변경하지 않는다.

## 대조 기준

- `cards.json`60개, CardUpgradeRules/CardLevelStatRules, 서버RewardCardEligibility, StarterTenCardCatalog54개 및 연결 자산의 비용/ATK/HP/피해 메타데이터. 현재 비교 대상 필드에서 JSON/네이티브 불일치0건. 카드 상세 원본JSON13곳을 갱신하고 전체60개 레코드의 구조적 동일성을 확인했다.
- 카드 목록60행의 기본 스탯·드래프트/최종 보상, 집계60/54/43플래그/41보상/44강화. 기록된 카드와 강화 규칙을 문서에 반영하며 새로운 밸런스나 강화 규칙을 만들지 않았다.
- 현재 연결된 유닛 Controller→clip 메타데이터를 추출했다. 미연결 옛 파일은 현재 동작으로 세지 않고, 모든 모션/픽셀을 실행·검수했다고 주장하지 않는다.
- 튜토리얼Session/실습 소스와 대본, 5장 멀리건, 최신 기본 자원, 보상 Canvas 수정/기존 검증 기록. 사용자 대사 본문은 임의로 변경하지 않았다.
- Lightsail 이전의 공개 작업/사용자 결과, 운영 HTTPS/WSS·Google/AI/광고,34테이블 격리 복원,57개 서버파일 외부 백업/웹 다운로드, 입력 평문 정리와21시 예약 실행 기록. 오늘 운영 서버를 재조회하거나 전 기능을 재시험한 것은 아니다.

## 확인과 남겨 둔 기록

신규/변경 링크의 로컬 대상을 확인했다. 기존 과거 증거 링크의 누락은 별도 결과에 보존하며 경로가 없다고 검증 성공을 새로 만들지 않는다. 날짜별 실패/미배포·과거 비용은 역사로 명확히 표시하고 현재 요약·운영 안내로 연결했다. 공통 카드 수·비용과 무관한 참고/음향/원본 프롬프트는 날짜만 바꾸려고 수정하지 않았다.

새로 게임/Unity 테스트를 실행한 작업이 아니다. 문서의 소스 대조·링크 검사와 과거의 실행 시험을 구분한다. 기존 보상UI 수정과 사용자 GameStart 씬 변경을 보존했다. 버전 관리 commit/push는 수행하지 않았다.

## 검토 목록 — 기존 직접 작성 문서 117개

| 문서 | 처리 | 내용 |
|---|---|---|
| `AGENTS.md` | 유지 | 작업 지침 보존 |
| `Art/Concepts/After333StartBackground_v2.prompt.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Art/Concepts/After333StartBackground_v3.prompt.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `README.md` | 갱신 | 골드 던전 현재 횟수; 트랩 수; 낙진 범위; 상점 범위; 최신 프로젝트 요약 |
| `Server/Project333.AccountOperationClientChecks/README.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `Server/Project333.LoadChecks/README.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `Server/Project333.PvpServer/README.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; 서버README의 과거 미배포 문구·노트북 명령 구분 |
| `Server/Project333.RunAuthorityChecks/README.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `Tools/Art/A212SawAttackAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/A301LaserAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/BlueDragonAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/GaebangChiefCoinPunchAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/GwangmaAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/ManaCannonAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/NuclearExplosionAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/RedDragonAndUnitHitAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/Source/README.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Art/TimedBombExplosionAudio.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `Tools/Audio/After333Music.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `docs/README.md` | 갱신 | 인덱스의 최신 집계·튜토리얼·운영 링크; 애니메이션 현재값 색인 |
| `docs/a212_presentation.md` | 갱신 | 현재 실제 controller 참조와 clip 수치 갱신 |
| `docs/a301_presentation.md` | 갱신 | 현재 실제 controller 참조와 clip 수치 갱신 |
| `docs/account_operation_retries.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/account_persistence_db_design.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; 로그인 외부 설정 완료 기록 |
| `docs/active_effects.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/art_prompts/spell_damage_staff.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `docs/art_prompts/spell_damage_staff_hex.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `docs/art_prompts/spell_damage_staff_hex_200x240.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `docs/art_prompts/spell_damage_staff_hex_256x256.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `docs/art_prompts/spell_damage_staff_hex_256x256_thin_border.md` | 유지 | 제작 프롬프트/음향 원본·참고 절차 보존 |
| `docs/background_music.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/battle_flow_stabilization_checklist.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분 |
| `docs/battle_modes.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/battle_notice_text_inventory_2026-09-26.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/battle_rules.md` | 갱신 | 개방 분타 비용; 상단 비용; 마정석 꾸러미 비용; 파이어스피어 피해; 파이어스피어 강화 결과; 파이어월 피해; 뉴클리어 비용; 기본 수치 동기화 범위 명시 |
| `docs/biochemical_gas_vfx.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/blue_dragon_art_prompts.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/blue_dragon_presentation.md` | 갱신 | 현재 실제 controller 참조와 clip 수치 갱신 |
| `docs/card_authoring_checklist.md` | 갱신 | 카드 제작 기준 집계; 제작 행렬의 피해·비용·강화 최종값 |
| `docs/card_catalog.md` | 갱신 | 60카드의 비용·기본 스탯·등장 플래그와 네이티브 수 갱신 |
| `docs/card_details.md` | 갱신 | 현재 카드 JSON 13곳, 기본 수치/비용/등장 및 Lv.13 결과 동기화; 간략 마법 표의 현재 비용 포함 |
| `docs/checkpoint_2026-09-05.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/client_build_identity.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/client_readiness_2026-09-13.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/codex_handoff.md` | 갱신 | 이후 사용자 테스트 사실 반영; 짧은 최신 인수인계 진입점; 문서 동기화 인수인계 결과 |
| `docs/codex_harness_cleanup_2026-09-21.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/common_hit_sound.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/compact_battle_hand.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/conversation_decisions.md` | 갱신 | 최신 색인 날짜; 과거 일정 보류와 이후 실제 CBT 구분; 최신 결정으로 라우팅 |
| `docs/conversation_recovery_before_2026-07-17_0939_ko.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/deck_building_pixel_ui.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/deployment_readiness_checklist.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분 |
| `docs/development_audit_2026-09-09.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/development_audit_followup_2026-09-11.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/device_and_load_check_2026-09-06.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/draft_pixel_ui.md` | 갱신 | 실제 보상상자 흐름·100×100 수정 |
| `docs/draft_rules.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/drained_status_vfx.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/earthquake_presentation.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/fire_spear_presentation.md` | 갱신 | 파이어스피어 현재 피해; 파이어스피어 강화 결과 |
| `docs/firewall_presentation.md` | 갱신 | 현재 파이어월 피해·공개 상태 |
| `docs/full_regression_2026-09-12.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/gaebang_chief_presentation.md` | 갱신 | 현재 실제 controller 참조와 clip 수치 갱신 |
| `docs/game_design.md` | 갱신 | 콘텐츠 집계; 트랩 집계; 튜토리얼·운영 개요 |
| `docs/gift_inbox.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/google_auth_setup.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; OAuth 기존 설정·실제 접속URL 후속 해결 |
| `docs/graduation_progress_ppt_ko.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/gwangma_art_prompts.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/gwangma_presentation.md` | 갱신 | 현재 실제 controller 참조와 clip 수치 갱신 |
| `docs/https_wss_reverse_proxy_setup.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; Caddy 현행 경로와 구Tunnel 명령 분리 |
| `docs/implementation_contract.md` | 갱신 | 현재 문서와 역사 계약 경계 |
| `docs/iron_dome.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/lan_pvp_demo_checklist.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/magic_amplification_circle.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/mana_cannon_presentation.md` | 갱신 | 현재 실제 controller 참조와 clip 수치 갱신 |
| `docs/mana_shield_presentation.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/meta_rules.md` | 갱신 | 네이티브 집계; 파이어스피어 피해·강화 결과; 최신 소스 대조 날짜; 경제 소스와 실제 운영 기록 분리 |
| `docs/meteor_presentation.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/mobile_secure_credential_storage.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/mulligan_presentation.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/mybox_database_backup.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; 일일 백업 원본·실제 예약·복원 확인 갱신 |
| `docs/notice_ui.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/nuclear_fallout.md` | 갱신 | 현재 뉴클리어 비용 |
| `docs/nuclear_presentation.md` | 갱신 | 현재 비용과 낙진 툴팁 |
| `docs/occupant_status_tooltips.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/online_pvp_prototype.md` | 갱신 | 현행 공개 인증 정책; 운영 이전 전 현재/미완료 설명과 최신 운영을 구분 |
| `docs/owned_cards_pixel_ui.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/owned_cards_ui.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/plasma_shield_presentation.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/postgres_restore_runbook.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; 최신 실제 복원·내용 검증·시험 자원/평문 정리 |
| `docs/postgresql_db_implementation_plan.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; 실행 스키마 범위 |
| `docs/public_pvp_smoke_test_checklist.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분 |
| `docs/public_pvp_tester_quickstart_ko.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분 |
| `docs/pve_ai.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/pve_ai_decks.md` | 갱신 | 고정 덱 JSON 재대조 날짜; 정적 덱 대조와 실행 검증 구분 |
| `docs/pve_reconnect_2026-09-13.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/reactive_armour_presentation.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/red_dragon_presentation.md` | 갱신 | 현재 실제 controller 참조와 clip 수치 갱신 |
| `docs/rewarded_ads.md` | 갱신 | 운영 이전 전 현재/미완료 설명과 최신 운영을 구분; 광고 운영 환경 파일과 과거 예제 구분 |
| `docs/run_reward_chest.md` | 갱신 | 실제 보상 대상 수 |
| `docs/semester1_final_report_ko.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/server_interruption_cancellation_2026-09-13.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/server_load_check_2026-09-11.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/server_restart_recovery_audit_2026-09-13.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/server_soak_check_2026-09-12.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/shop_card_packs.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/shop_pixel_ui.md` | 갱신 | 상점 카드팩·상품 디자인 통일; 최신 상점 스타일; 실제3열 상품 배치 |
| `docs/special_effects.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/stability_check_2026-09-06.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/start_menu_pixel_ui.md` | 갱신 | 완성된 실제 UI 튜토리얼 |
| `docs/stun_freeze_status_vfx.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/summon_display_scales.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/tile_click_investigation_2026-09-22.md` | 갱신 | 과거 검증/보고서의 읽기 기준 |
| `docs/timed_bomb_presentation.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/tutorial_script_ko.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |
| `docs/unit_animation_authoring.md` | 갱신 | 현재 메타데이터의 단일 조회 경로 |
| `docs/unit_races.md` | 유지 | 현재 소스·규칙과 모순되는 갱신 대상 없음; 날짜별 검증은 해당 시점 기록 |

## 새 안내

- [current_project_status.md](current_project_status.md)
- [documentation_audit_2026-10-20.md](documentation_audit_2026-10-20.md)
- [lightsail_operations.md](lightsail_operations.md)
- [tutorial_implementation.md](tutorial_implementation.md)
- [unit_animation_snapshot.md](unit_animation_snapshot.md)

기계 대조 결과·변경 전 사본·패치·기존 누락 링크 목록은 세션 시각화 `DocsRefresh_20261020`에 보관한다. [현재 상태](current_project_status.md)가 다음 작업의 짧은 진입점이다.
