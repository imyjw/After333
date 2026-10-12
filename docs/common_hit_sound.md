# 공통 피격음 직접 교체하기

1. 원하는 WAV·MP3·OGG 음원을 Unity Project 창의 Assets 폴더 안에 넣는다. FLAC은 먼저 WAV로 변환한다.
2. Project 창에서 Assets/Project333/Resources/Project333/Audio/Common/HitSoundSettings.prefab을 선택한다.
3. Inspector의 Audio Source의 음원 슬롯(Audio Resource 또는 Audio Clip)에 새 음원 에셋을 드래그한다. Volume으로 피격음 크기도 조절할 수 있다. 기본값은 0.52다.
4. Play Mode를 다시 실행해 확인한다. 휴대폰 앱에는 새로 빌드·설치해야 반영된다.

파일 이름은 자유롭게 정해도 된다. 씬에 프리팹을 넣을 필요는 없다. 프리팹의 Audio Clip과 Volume만 설정으로 읽으며, 별도 AudioSource 오브젝트를 생성하지 않는다. 다른 Audio Source 항목은 사용하지 않는다. Audio Clip을 None으로 비우면 공통 피격음이 나지 않는다.

한 번 지정하면 모든 유닛·건물·마스터와 공격자가 받는 반격 피해에 함께 적용된다. 같은 렌더 프레임의 광역 피격은 공통 소리를 한 번만 재생하고, 다음 프레임의 연속 타격은 다시 재생할 수 있다. 일반공격을 시작한 유닛은 반격을 받아도 Attack 뒤에 BeAttacked 모션을 덧붙이지 않지만 공통 피격음·수치·필요한 사망 모션은 유지한다. 피해 없는 트랩: 리턴 반환에는 피격음을 만들지 않고 미러 필드의 실제 반사 피해는 공통 피격 경로를 사용한다. 기존 전체 음량 설정도 적용된다. 현재 기본 음원은 사용자가 제공한 55.flac을 변환한 UnitHit.wav다. 이 WAV를 덮어쓰는 대신 다른 파일을 지정할 수 있으며, 원래 설정으로 돌아가려면 UnitHit.wav를 다시 넣으면 된다.

HitSoundSettings 프리팹 자체가 없거나 Audio Source가 제거된 예전 콘텐츠에서는 UnitHit.wav와 음량 0.52를 사용한다. 교체 작업은 Play Mode 밖에서 하며, 프리팹 경로와 이름을 변경하지 않는다.
