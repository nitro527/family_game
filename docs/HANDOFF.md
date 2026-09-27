# 다시 시작하기 (인수인계)

마지막 갱신: 2026-09-27 밤 — **주말 작업 종료, 자동 루틴 일시정지**.
주말 회고·다음 할 일·확장 검토·**작업 방식 비교와 사용자 체크리스트(§6)** 는 → [REVIEW_2026-09-27.md](REVIEW_2026-09-27.md)

## 주말에 다시 시작할 때 (순서대로)
1. Unity Hub 로그인 확인 (배치 모드 검증에 필요)
2. Claude 에게 "**루틴 켜**" → 예약 작업 `girl-adventure-improve` 를 다시 켬 (지금 `enabled: false`, 한 번 실행형 이어달리기라 켤 때 fireAt 을 몇 분 뒤로 다시 잡아야 함)
3. 다음 개발 항목: `docs/BACKLOG.md` **49 동생 합류 c (View)** — Sim 쪽(48)은 끝났음
   - 합류 컷신에서 `Quest.TryJoinBrother` 호출, 전투 화면 2인 배치·명령 차례·HUD 2줄, 필드에서 서진 뒤를 따라 걷기
   - 그림은 `Resources/Boy` (`Art.Get("Boy/...")`)
4. 그 뒤 50~53 챕터 3 (사막), 54~56 스킬 레벨·주문 바꾸기
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
