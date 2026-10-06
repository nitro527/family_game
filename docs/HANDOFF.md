# 다시 시작하기 (인수인계)

마지막 갱신: 2026-10-05 밤 — **궁극기·기술 티어(Q-39), 기술 장면 영상(라이트 15 + 궁극 ult_ 15, Wan 14B), 협동 공격 11가지, 챕터 6 도서관(엄마 합류), 전투 배경·맵 소품·마을 사람 41명 그림(Qwen), 지훈 한 버튼 모드, 레이아웃 정리**. 진행 중: 보물상자·동굴 샘물 소품 연결 후 전체 프로브·빌드 확인, 다음 후보 Q-42 웹 빌드(태블릿). 사용자 지시: "할 일 찾아서 계속 일해라 (백그라운드만 돌리지 말고)". 그 전: 2026-10-05 오후 — **챕터 6 도서관(엄마 합류)·동료 회복·자동 모드·기술 장면 애니메이션·협동 공격 3종**. 그 전: **로컬 Qwen 그림으로 맵 소품 34종·챕터 1 카드 교체**, 호박 젤리. 이전: **챕터 5(눈 덮인 곳·예티)까지 완성**, 시험 플레이 메모 1차(P-1~6) 반영, 별빛 마을 집 안, 타이틀 "챕터 고르기". 빌드 `unity/Build/GirlAdventure.exe`(148MB), 메모 양식 [PLAYTEST_NOTE.md](PLAYTEST_NOTE.md).
사용자 지시 (10-03): "당분간 나로부터 받을 정보 없을 테니 알아서 전반적인 게임 퀄리티를 향상시켜" → 🙋 질문에 막히지 말고 ✏️ 임시로 진행.
주말 회고·다음 할 일·확장 검토·**작업 방식 비교와 사용자 체크리스트(§6)** 는 → [REVIEW_2026-09-27.md](REVIEW_2026-09-27.md)

## 다시 시작할 때 (순서대로)
1. Unity Hub 로그인 확인 (배치 모드 검증에 필요)
2. 예약 루틴 `girl-adventure-improve` 는 **꺼 둔 채로** — 이 대화에서 사용자 명령 / 알아서 퀄리티 개선
3. `docs/BACKLOG.md` 맨 위부터 (P·C5·C4·U 는 모두 완료, **Q 퀄리티 Q-1~15 완료 2026-10-04**: 이름표·모든 마을 집 안·선택 창 스크롤(대장간 9줄 버그)·상태 창·눈 효과음·용암/눈 구멍·열린 상자 그림·✏️ 표시 정리·도감 사는 곳·편지 퀘스트·어두운 상자·성장 곡선). 다음 후보: Q 계속(화면 점검), PLAN_NEXT 62(지훈 기술 🙋 이름)·75(태블릿)·76(소리 평가), 🙋 답 반영
   - **2026-10-05**: Q-24 지훈 "큰 번개" ✏️(Lv14, 모두에게), Q-25 눈꽃 마을 곡. 챕터 1 완료 카드 그림은 PC 잠금으로 클립보드가 막혀 못 함 → 다음에 (`scratchpad` 대신 `family_party.png` 에서 서진만 잘라 참고)
   - **기술 티어 (2026-10-05, Q-39)**: 기본 기술 = 라이트 장면 (`python tools/cutins_video.py`), 궁극기 = 화려한 장면 (`--ult`, ult_<이름>). 궁극기는 기본 기술 스킬 Lv3(30번)에 배우고 궁극 게이지로 쓴다. 미리보기 MP4: `C:/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/cutin_preview.py` → assets/cutins/preview
   - **로컬 영상 (2026-10-05, Q-35)**: Wan 2.2 14B GGUF Q4_K_M (+ ComfyUI-GGUF 확장, lightx2v 4스텝) — `python tools/cutins_video.py [이름] [--b]` → `python tools/prepare_cutins.py`. 사용자는 5B 보다 14B 선호. 사용자 지시 (10-05 저녁): "애니메이션 역동적으로, 이미지 파일 퀄리티 업그레이드, 나머지는 알아서 전반적인 퀄리티 개선" → BACKLOG Q-38 부터
   - **로컬 그림 (2026-10-05, Q-26~30)**: ComfyUI(`C:\ComfyUI\ComfyUI_windows_portable`)를 127.0.0.1:8188 로 띄우고 `python tools/qwen_gen.py "프롬프트" -o out.png [--ref 참고.png]`. 맵 소품은 `tools/props_gen.py` 목록에 한 줄 넣고 실행 → `MapPainter` 해당 함수 맨 앞에 `if (Prop(R, "이름", 가운데x, 바닥y)) return;` → `MapExport.Run` 으로 맵 그림 확인. 사용자 지시 "퀄리티 낮은 애들부터" → 그림은 Q-30. Q-31~34(동료 회복·자동 모드·기술 장면 애니메이션·협동 공격 3종) 완료. C6 챕터 6 도서관 완료(엄마 합류, LastChapter 6, 세이브 v16). **다음**: Q-30 그림(동굴 기지 성벽·옥좌 등), 엄마 협동 공격(누나+엄마 등 — 사용자 요청 범위 밖이라 🙋), 챕터 6 🙋(악마 모습·이름, 엄마 기술 이름)
   - **Gemini 그림 (2026-10-04, Q-18~23)**: 지하세계 5종(조금 무섭게)·용암/눈 젤리·화산 대장·동굴 보스 2·무서운 예티·얼음 골렘 → 모든 적이 Gemini 그림(`enemies.json` Art). 챕터 완료 카드 `Resources/Cards/chN_clear` (4·5 있음, 1~3 은 아직 기본 그림 → 다음 후보). 순서: 기준 그림 한 장으로 합쳐 `tools/clip_image.py` → Gemini 새 채팅·이미지 만들기(버튼은 find 로 ref 찾기가 좌표보다 안전) → 다운로드 → `assets/monsters/gen` + `docs/monsters/monsters.json` → `python tools/monster_catalog.py`(`clear_holes`) → `python tools/prepare_unity_art.py monsters`
   - 화면 점검 요령: 프로브 스크린샷 여러 장을 PIL 로 한 장에 모아 보기 (scratchpad), SmokeTest 그림 내보내기(`ExportSheet`·map_snow·chests)
   - 동료는 `party.json` (지훈 jihun·아빠 dad), 화면은 동료 배열(`AllyView`·`Follower`·`allyHuds`), 동료 공용 대사 `ally_*`, 지훈은 예전 `bro_*`
   - 챕터 추가 자리: `WorldMap.LastChapter`(지금 6)·`ForChapter`·`HasTown`·`ForPlayer`, `Quest.StartChapterN`·`QuickStart`(챕터 고르기 레벨·장비), View `SurfaceMap`·`TravelRoutine`·`StartNextChapter`·`ChapterNRoutine`·`ShowEnding`
   - 맵 종류: Field·Cave·Village·Desert·Oasis·Underworld·Lava·LavaTown·Home(집 안)·Snow·SnowTown. 얼음 미끄럼 `WorldMap.Slippery/SlideEnd`
   - 밸런스: SmokeTest `[BALANCE SNOW]`·`[BALANCE BOSS5]`·`FightDuo`(셋 AI), `python tools/balance.py --skill N`. 프로브 난수는 `GameRoot.SeedOverride`
   - 세이브 버전 올리면 SmokeTest `SaveSafetyTests` 에 예시 JSON 추가 (지금 v16, 예시는 v15 까지)
   - 그림 확인: SmokeTest 가 `unity/playtest/under_monsters.png`·`snow_jellies.png`·`map_snow.png` 를 만든다 (`ExportSheet`)
4. 🙋 답을 기다리는 것 (PLAYTEST_NOTE 맨 아래): 새 주문(56), 지훈·아빠 기술 이름, 마을·보스 이름, 엄마 합류 시점, 용암·눈 괴물 디자인, 예티 이름·성격
5. 그림(Gemini) 작업은 **대화 세션에서만** (자동 루틴 불가). PC 잠금 상태면 클립보드가 막혀 못 함

## 다음에 할 일 (2026-10-05 밤, 사용자 "멈추라고 할 때까지 알아서 계속")
000. **멈춘 지점 (2026-10-06 밤, 사용자 "이번 주 사용량 거의 다 씀")**: 마지막 커밋 = 보스 화남(5116d47) 다음 문서 커밋. 그 뒤 추가: 설정 "조작법 보기"·패드 안내 띠, Tab 이름 바꾸기, 보스 화남(HP 절반 ×1.1). 실제 닌텐도 패드 확인은 사용자 🙋. 다음 후보: 패드 실제 확인 결과 반영, 서진이 피드백(이름 🙋 목록 docs/lore/open_questions.md), 동굴·도서관 숨은 길
00. **2026-10-06 저녁 사용자 요청 처리**: ① 지훈 번개 장면 영상(bolt) ② 궁극기 공용 게이지(가족 하나, 0~400, 행동 +14·맞으면 +8, 배우지 않아도 참, 쓰면 100, `BattleSystem.Pool`) ③ 키보드만으로 다 되게(엔딩 화면 위아래 선택) + 닌텐도 프로 컨트롤러(새 입력 시스템 1.19 — `Packages/com.unity.inputsystem-1.19.0.tgz`, 편집기에 들어 있던 것, activeInputHandler=Both, `Keys.ReadNewPad`: A 확인·B 취소(X 키)·X/+ 메뉴(Esc)). Joy-Con 은 새 입력 시스템이 직접 지원 안 함 → 실제 패드로 확인 필요 🙋
0. **사용자 방향 (2026-10-06)**: "당분간 새 챕터보다 기존 작업 퀄리티" → BACKLOG G-1~G-4. G-1 전투 기믹·G-2 몬스터 12종·G-4 퀘스트·비밀 완료, G-3 맵 오브젝트(나무 흔들기·물방울·풀숲·항아리)·맵 이벤트 9개(quests.json Events)·바닥 장식(MapPainter.Scatter, dec_* 소품 9종), 표지판 기믹 힌트, 부하 부르기(킹 젤리·카우보이 대장·악마 대장), 도감 글 겹침·상태 창 궁극기 줄 넘침 고침, 초원·마을 장식, 전투 상태 이상 색(StatusTint), 마을 사람 29명 돌아가며 하는 말(npcs.json More), 특별 장신구 상태 이상 막기(Resist)·특기(Perk), 자동 모드 기믹 대응·AutoTarget, 숲·도서관 분위기 효과, 숨은 길 5곳(초원·사막·용암·눈·숲, '?' 칸, docs/ideas/quests_g4.md), 이긴 뒤 떨어뜨린 물건(RollDrops), 지훈 번개 장면 영상(bolt, 사용자 2026-10-06 "번개 쓸 때 애니메이션이 안 나온다" — 원래 번개엔 장면이 연결돼 있지 않았음). 다음: 나머지 맵(초원·마을·숲·도서관) 장식 점검, 스크린샷으로 화면 점검 계속, 퀘스트·몬스터 이름 🙋, 챕터 8 은 보류(C8)
1. ~~열린 성문·리본 편지 그림, 도서관 보물상자·구멍~~ (완료 Q-43). 구멍 그림은 땅 색에 맞춘 코드 그림이 충분해서 그대로
2. ~~챕터 7 숲~~ (완료 C7, 커밋 848290d) · ~~가족 두 번째 스킬~~ (완료 Q-44, 6b956c8: 튼튼 함성·힘내라 응원·엄마의 빛 지속 회복·엄마의 포옹). 새 스킬 라이트 장면 완료(92ef91e, 응원 B 는 하늘 배경으로 다시) + 전투 HUD 버프 남은 차례(서진 MP 줄 끝). 보스 6 은 엄마 회복이 세져 HP 5800·공 70, 보스 7 HP 7200·공 82 로 재조정
3. 다음 후보: 챕터 8 (설정집 적의 성·군인 젤리·이단 킹 젤리 — 🙋 또는 ✏️), 상태 창/전투 HUD 에 버프 남은 차례 표시, 숲 장비 밸런스 확인, 소리 평가는 사용자
4. 웹판(Q-42)은 사용자 "나중에" — 한글 글꼴(나눔고딕 OFL) 다운로드 승인 필요
5. 🙋 따님과 정할 이름: 궁극기·협동 공격·도서관 악마·두 번째 스킬·숲 이야기 (docs/lore/open_questions.md)
6. 백업(PLAN 78): 이 PC 에 드라이브가 C: 하나뿐 → 외장 디스크나 비공개 원격 저장소는 사용자 결정

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
| `prepare_unity_art.py` | 에셋 → Unity Resources (`girl`, `boy`, `dad`, `mom`, `monsters`) |
| `balance.py` | 전투 밸런스 빠른 탐색 |
| `qwen_gen.py` | 로컬 Qwen-Image 2.1 그림 (ComfyUI, `--ref` 로 화풍 맞추기) |
| `props_gen.py` · `make_prop.py` | 맵 소품 그림 목록 → 배경 지우기·크기 → `Resources/Tiles/Props` (`MapPainter.Prop`) |
| `battle_bg_gen.py` | 전투 배경을 게임 화풍으로 (`--seed` 로 다시) |
| `wan_gen.py` | 로컬 Wan 2.2 영상 (`--big` = 14B GGUF, 사용자 선호) |
| `cutins_video.py` | 기술 장면 A(얼굴)·B(쏟아짐) 영상 → 30장씩 (기본 = 라이트, `--ult` = 궁극) |
| `prepare_cutins.py` · `cutin_preview.py` | 장면 → `Resources/Cutin` (높이 752, crunch) · 미리보기 MP4 (ComfyUI 파이썬으로). ⚠ 돌린 뒤 Unity 가져오기에서 `.meta` 가 첫 줄만 남은 채로 잘린 적 있음 (10-05, 30개) → 커밋 전 `grep -L "^guid:" Resources/Cutin/*.meta` 로 확인, 있으면 `git checkout HEAD -- <meta>` |

## 지켜 온 규칙
- 한국어로, 폴더 깔끔하게(임시 파일은 끝나면 삭제), `사진/`·원본 에셋 삭제 금지, git push 금지(원격 없음)
- 소리 없이 테스트, 루틴은 한 번에 하나(lock 파일 `.autoimprove.lock`), 사용량 95%에서 멈춤 (2026-10-05 사용자)
- PowerShell 은 막혀 있음 → Bash 만
