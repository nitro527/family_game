# 외부 무료 에셋 (출처·라이선스)

2026-09-26 사용자 승인 후 다운로드했어요. 원본 압축 해제본은 `assets/thirdparty/` 에 있어요.
전투 배경은 80MB라 git에서는 제외했고, 게임에 실제로 쓰는 파일만 Unity 프로젝트로 복사해서 기록해요.

| 폴더 | 에셋 | 출처 | 라이선스 | 크레딧 |
|---|---|---|---|---|
| `pipoya_world_tileset/` | PIPOYA FREE RPG World Tileset (48×48 · 40×40 · 32×32, 월드맵용) | https://pipoya.itch.io/pipoya-free-rpg-world-tileset-32x32-40x40-48x48 | 무료 · 상업 이용 가능 · 수정 가능 · **재배포 및 판매 금지** | 선택 (넣는다면: "Pipoya https://pipoya.itch.io/") |
| `painterly_battle_backgrounds/` | Painterly Battle Backgrounds 무료판 (50장, 1920×1080, 동화풍. 제작자가 AI 보조로 만들었다고 밝힘) | https://kalponic-studio.itch.io/painterly-battle-backgrounds | 무료 · 상업 이용 가능 · 수정 가능 · **배경 단독으로 재판매·재배포 금지** | 요구 없음 |
| `kenney_ui_rpg/` | Kenney UI Pack (RPG Expansion) 87개 PNG (창·버튼·바·화살표) | https://kenney.nl/assets/ui-pack-rpg-expansion | **CC0** (완전 자유) | 필요 없음 (감사 표기 권장) |

## 사용 규칙
- 게임 엔딩 크레딧에 세 출처를 적어요 (의무는 아니지만 예의).
- 원본 에셋 파일만 따로 공유하거나 배포하지 않아요 (PIPOYA, Painterly 조건).

## 게임에 쓰는 파일
- 전투 배경 (`unity/GirlAdventure/Assets/Resources/Battle/`, 1280×720 JPG로 줄임): meadow=22번, meadow_boss=23번, cave=49번, cave_guard=36번, cave_boss=40번, desert=1번, desert_boss=20번, underworld=37번, lava=33번, lava_boss=35번 (남은 사막 후보: 오아시스 2번, 붉은 바위 27번)
- 초원 타일 (`unity/GirlAdventure/Assets/Resources/Tiles/`, 48×48 원본 그대로): pipo_sheet=pipo-map001.png, pipo_at_kusa/miti/umi/mori=[A]_type1 오토타일, 사막(챕터 3) pipo_at_sabaku·pipo_at_yama2, 용암 땅(챕터 4) pipo_at_yama3 =[A]_type1
- UI (`unity/GirlAdventure/Assets/Resources/UI/`): bar_back=barBack 3조각 합침(색 바꿈), bar_fill=barGreen 3조각 합쳐 흑백화, buttonLong_grey(_pressed) 원본
