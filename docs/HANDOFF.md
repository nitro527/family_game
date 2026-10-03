# 다시 시작하기 (인수인계)

마지막 갱신: 2026-10-04 — **챕터 5(눈 덮인 곳·예티)까지 완성**, 시험 플레이 메모 1차(P-1~6) 반영, 별빛 마을 집 안, 타이틀 "챕터 고르기". 빌드 `unity/Build/GirlAdventure.exe`(148MB), 메모 양식 [PLAYTEST_NOTE.md](PLAYTEST_NOTE.md).
사용자 지시 (10-03): "당분간 나로부터 받을 정보 없을 테니 알아서 전반적인 게임 퀄리티를 향상시켜" → 🙋 질문에 막히지 말고 ✏️ 임시로 진행.
주말 회고·다음 할 일·확장 검토·**작업 방식 비교와 사용자 체크리스트(§6)** 는 → [REVIEW_2026-09-27.md](REVIEW_2026-09-27.md)

## 다시 시작할 때 (순서대로)
1. Unity Hub 로그인 확인 (배치 모드 검증에 필요)
2. 예약 루틴 `girl-adventure-improve` 는 **꺼 둔 채로** — 이 대화에서 사용자 명령 / 알아서 퀄리티 개선
3. `docs/BACKLOG.md` 맨 위부터 (P·C5·C4·U 는 모두 완료). 다음 후보: 전반 퀄리티(아래 Q 섹션), PLAN_NEXT, 🙋 답 반영
   - 동료는 `party.json` (지훈 jihun·아빠 dad), 화면은 동료 배열(`AllyView`·`Follower`·`allyHuds`), 동료 공용 대사 `ally_*`, 지훈은 예전 `bro_*`
   - 챕터 추가 자리: `WorldMap.LastChapter`(지금 5)·`ForChapter`·`HasTown`·`ForPlayer`, `Quest.StartChapterN`·`QuickStart`(챕터 고르기 레벨·장비), View `SurfaceMap`·`TravelRoutine`·`StartNextChapter`·`ChapterNRoutine`·`ShowEnding`
   - 맵 종류: Field·Cave·Village·Desert·Oasis·Underworld·Lava·LavaTown·Home(집 안)·Snow·SnowTown. 얼음 미끄럼 `WorldMap.Slippery/SlideEnd`
   - 밸런스: SmokeTest `[BALANCE SNOW]`·`[BALANCE BOSS5]`·`FightDuo`(셋 AI), `python tools/balance.py --skill N`. 프로브 난수는 `GameRoot.SeedOverride`
   - 세이브 버전 올리면 SmokeTest `SaveSafetyTests` 에 예시 JSON 추가 (지금 v15)
   - 그림 확인: SmokeTest 가 `unity/playtest/under_monsters.png`·`snow_jellies.png`·`map_snow.png` 를 만든다 (`ExportSheet`)
4. 🙋 답을 기다리는 것 (PLAYTEST_NOTE 맨 아래): 새 주문(56), 지훈·아빠 기술 이름, 마을·보스 이름, 엄마 합류 시점, 용암·눈 괴물 디자인, 예티 이름·성격
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
