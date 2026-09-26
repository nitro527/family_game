# 작업 인수인계 (토큰 한도로 중단 시 이어서 진행용)

마지막 갱신: 2026-09-26 12:30 (KST) — 남은 작업 없음 (재개 예약 취소함)

## 프로젝트 개요
- 가족(특히 딸) 기반 드래곤 퀘스트 풍 캐릭터 에셋 + Unity 미니 RPG "꼬마 마법사 첫째의 모험"
- Unity 6 (6000.5.3f1) 프로젝트: `unity/GirlAdventure` — 코드 우선 구조
  - `Assets/Scripts/Sim/` 순수 규칙, `Assets/Scripts/View/` 화면·연출, `Assets/Editor/` 자동화
- 게임 데이터(적·명령 수치·대사): `unity/GirlAdventure/Assets/Resources/Data/*.json` — 대사 수정은 코드 말고 여기서
- 에셋 원본: `assets/characters/gen/girl/` → Unity 반영: `python tools/prepare_unity_art.py`
- 사용자는 한국어로 소통, 폴더가 지저분한 걸 싫어함 (임시 파일은 끝나면 정리)

## 검증 명령 (Git Bash, 작업 디렉터리 unity/)
```
U="/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe"; P="C:\\code_work\\new_game_project\\unity\\GirlAdventure"
"$U" -batchmode -quit -projectPath "$P" -executeMethod GirlAdventure.EditorTools.SmokeTest.RunAll -logFile smoke.log      # SMOKETEST_RESULT
"$U" -batchmode -projectPath "$P" -executeMethod GirlAdventure.EditorTools.PlaytestProbe.Run -logFile playtest.log          # PLAYTEST_RESULT, "PT:" 줄, unity/playtest/*.png
"$U" -batchmode -quit -projectPath "$P" -executeMethod GirlAdventure.EditorTools.PlayerBuilder.BuildWindows -logFile player.log  # unity/Build/GirlAdventure.exe
```
(배치 모드는 Unity Hub 로그인 상태여야 동작)

## 완료
- 걷기 방향별 캐릭터 크기 불일치 수정 (모든 걷기 프레임 키 240px 통일)
- 타이틀 Enter 가 첫 대사를 건너뛰던 버그, 전투 중 주인공이 HUD 와 겹치던 문제,
  패배 후 부활 대사가 전투 화면에 뜨던 문제, 옆걸음 애니메이션 밋밋함 수정
- 자동 플레이테스트(PlaytestProbe) 구축: 가상 키 입력으로 타이틀→필드 4방향→표지판/샘물→랜덤전투→명령 4종→패배/부활→보스→엔딩

## 남은 작업 (순서대로)
0. [x] (완료 → docs/research_source_games.md) 사용자 요청: https://en.wikipedia.org/wiki/List_of_commercial_video_games_with_available_source_code 에서
       최근 릴리즈 게임 위주로 우리 프로젝트(Unity 2D DQ풍 RPG / 카드게임)에 쓸만한 것 조사 → docs/research_source_games.md
1. [x] (완료 12:25 — 타이틀~엔딩 전 과정 통과, exe 재빌드) PlaytestProbe 재실행 → 보스전·엔딩까지 가는지 확인 (디버그 보스전을 Lv5 이상으로 시작하게 방금 수정함),
       스크린샷(unity/playtest)을 직접 보고 새 문제 있으면 수정 → SmokeTest 통과 확인 → Windows 빌드 재생성
2. [x] (완료 → docs/research_game_graphics.md) "유명 프로젝트 조사": Claude Code 등 AI 로 만든 유명 게임 프로젝트들이 그래픽(2D/3D)을 어떻게 만드는지 웹 조사
       (예: 코드로 도형·셰이더, 무료 에셋팩 Kenney/Quaternius, AI 3D 생성 Meshy/Tripo/Hyper3D, Blender MCP, Unity MCP 등)
       → 결과를 `docs/research_game_graphics.md` 로 정리하고, 우리 프로젝트에 적용할 점 제안
3. [x] (완료) 끝나면 임시 로그(unity/*.log), unity/_walk_sheet.jpg, unity/_battle_sheet.jpg 정리하고 이 파일의 체크박스 갱신
