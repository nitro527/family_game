# 다시 시작하기 (인수인계)

마지막 갱신: 2026-10-03 밤 — **챕터 4 까지 완성, 서진이 시험 플레이를 위해 멈춤** (사용자: "챕터 4 끝까지 만들고 한번 멈춰 딸 플레이 하게"). 빌드 `unity/Build/GirlAdventure.exe`(145MB), 메모 양식 [PLAYTEST_NOTE.md](PLAYTEST_NOTE.md). 다시 시작하면 메모를 백로그로 옮기는 것부터.
주말 회고·다음 할 일·확장 검토·**작업 방식 비교와 사용자 체크리스트(§6)** 는 → [REVIEW_2026-09-27.md](REVIEW_2026-09-27.md)

## 주말에 다시 시작할 때 (순서대로)
1. Unity Hub 로그인 확인 (배치 모드 검증에 필요)
2. (2026-10-03 부터) 예약 루틴 `girl-adventure-improve` 는 **꺼 둔 채로** — 사용자가 이 대화에서 명령하면 백로그 항목을 하나씩 처리
3. 다음 개발 항목 (Claude 가 계획하며 진행 — 사용자 지시 10-03 "앞으로 뭘할지도 니가 계속 계획하면서 진행해봐"): `docs/BACKLOG.md` 맨 위 **C4 챕터 4** 의 남은 것
   - C4-1~9 모두 완료 (C4-8 빌드·메모도). 다음은 시험 플레이 메모 → 백로그, 그 뒤 PLAN_NEXT / 🙋 답
   - 동료는 `party.json` (지훈 jihun·아빠 dad), 화면은 동료 배열(`AllyView`·`Follower`·`allyHuds`), 동료 공용 대사 `ally_*`(얼굴 `ally_brave`), 지훈은 예전 `bro_*`
   - 마을 드나들기: `WorldMap.HasTown(chapter)`(지금 1·3), `ForPlayer`, View `SurfaceMap`·`TravelRoutine`(챕터 3 = 사막) — 챕터 4 마을을 넣을 때 여기들
   - 밸런스: SmokeTest `[BALANCE LAVA]`·`FightDuo`(셋 AI), `python tools/balance.py --skill N`. 프로브 난수는 `GameRoot.SeedOverride` 로 고정
   - 세이브 버전 올리면 SmokeTest `SaveSafetyTests` 에 예시 JSON 추가 (지금 v13)
4. 그 뒤: PLAN_NEXT.md (58 편지 퀘스트 🙋, 62 지훈 기술 🙋, 엄마 합류 시점 🙋…) · 🙋 답을 기다리는 것: 지하세계·용암 괴물 디자인(서진이), 새 주문(56), 기술·마을·보스 이름
5. 그림(Gemini) 작업은 **대화 세션에서만** (자동 루틴 불가). PC 잠금 상태면 클립보드가 막혀 못 함

## 프로젝트 한눈에
| 무엇 | 어디 |
|---|---|
| 게임 (테스트 버전) | `unity/GirlAdventure` — Unity 6 (6000.5.3f1), 코드 우선. `Scripts/Sim` 규칙, `Scripts/View` 화면, `Editor` 자동화 |
| 게임 데이터 | `unity/GirlAdventure/Assets/Resources/Data/*.json` (적·출현·아이템·주문·NPC·대사) |
| **세계관 설정집 (기준)** | `docs/lore/` — 게임과 다르면 설정집이 맞음 |
| 서진이 아이디어 노트 | `docs/ideas/daughter_note_NN.jpg/.md` |
| 몬스터 도감 | `docs/monsters/` (monsters.json, dogam.jpg) + `assets/monsters/` |
| 캐릭터 에셋 | `assets/characters/gen/<girl|boy|mom|dad|family>/`, 요약 `assets/characters/overviews/` |
| 할 일 | `docs/BACKLOG.md` (위에서부터), 큰 그림 `docs/ROADMAP.md` |
| 외부 에셋·라이선스 | `docs/thirdparty_assets.md` |

## 검증 명령 (Git Bash, 작업 디렉터리 `unity/`)
```
U="/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe"; P="C:\\code_work\\new_game_project\\unity\\GirlAdventure"
"$U" -batchmode -quit -projectPath "$P" -executeMethod GirlAdventure.EditorTools.SmokeTest.RunAll -logFile smoke.log      # SMOKETEST_RESULT
"$U" -batchmode -projectPath "$P" -executeMethod GirlAdventure.EditorTools.PlaytestProbe.Run -logFile playtest.log          # PLAYTEST_RESULT, unity/playtest/*.png
"$U" -batchmode -quit -projectPath "$P" -executeMethod GirlAdventure.EditorTools.PlayerBuilder.BuildWindows -logFile player.log  # unity/Build/GirlAdventure.exe
```
- 배치 모드는 소리가 나지 않게 되어 있음 (`BatchModeMute`). 테스트로 exe 를 직접 켜지 않음
- 보스 수치는 Unity 없이 먼저 `python tools/balance.py king --hp 120:160:10`

## 도구 (tools/)
| 도구 | 하는 일 |
|---|---|
| `clip_image.py` | 그림을 클립보드로 (Gemini 붙여넣기용) |
| `postprocess.py` | 흰 배경 제거, 표정 시트 → 아이콘, 포즈 시트 분할 |
| `build_character_set.py` | 캐릭터 4명: 투명 메인·icons·poses·걷기 gif·요약 페이지 한 번에 |
| `card_mockup.py` | 카드 목업 (4명 × 2장, 숫자는 임시) |
| `monster_catalog.py` | 도감: 몬스터 잘라내기 + dogam.jpg |
| `prepare_unity_art.py` | 에셋 → Unity Resources (`girl`, `boy`, `monsters`) |
| `balance.py` | 전투 밸런스 빠른 탐색 |

## 지켜 온 규칙
- 한국어로, 폴더 깔끔하게(임시 파일은 끝나면 삭제), `사진/`·원본 에셋 삭제 금지, git push 금지(원격 없음)
- 소리 없이 테스트, 루틴은 한 번에 하나(lock 파일 `.autoimprove.lock`), 사용량 85~90%에서 멈춤
- PowerShell 은 막혀 있음 → Bash 만
