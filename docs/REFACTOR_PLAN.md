# 코드 정리 계획 (2026-10-10, 사용자 "재사용 가능하게 캡슐화·리팩토링 — 확장을 위한 첫걸음")

## 지금 상태 (조사)
- 스크립트 약 2만 줄. `GameRoot`(화면) 한 클래스가 partial 18개 파일 6,500줄 — 상태·연출·입력이 모두 한 곳.
- **챕터가 코드 곳곳에 박혀 있음**: 보스 처치 기록 `Boss2Down~Boss7Down` 111곳, 챕터 시작 루틴 6개·완료 루틴 6개·`Quest.StartChapterN` 6개가 거의 복사본,
  보스 이긴 뒤 분기·완료 카드·지금 할 일·맵 고르기·보스 칸 밟기가 모두 `if 챕터7 … else if 챕터6 …` 사슬. → 챕터 8 하나 넣으려면 10여 파일 20여 곳.
- **맵 종류(MapKind) 비교 290곳**: 배경음악·그리는 함수·분위기 효과·불빛이 모두 삼항 사슬.
- 테스트 파일이 한 덩어리(SmokeTest 2,900줄, PlaytestProbe 1,900줄).

## 원칙
- 한 단계씩, 매 단계 **동작이 똑같은지** 확인: 스모크 테스트 + 맵 그림 픽셀 비교 + 전체 플레이 프로브.
- 세이브 파일 모양(BossNDown 필드)은 그대로 — 옛 세이브가 그대로 열려야 한다.
- 이야기·대사 데이터는 그대로, 코드 구조만.

## 단계
| 단계 | 할 일 | 효과 |
|---|---|---|
| A. 챕터 표 | `Sim/Chapters.cs` — 챕터마다 보스·시작 맵/자리·합류하는 가족·시작/완료 대사·완료 카드·지금 할 일. `PlayerState.Beaten(ch)`/`SetBeaten(ch)` 로 Boss1~7Down 감싸기. 시작·완료 루틴 12개 → 1개씩, `StartChapterN` 6개 → 1개, 보스 이긴 뒤 분기·완료 카드·지금 할 일·보스 칸 밟기를 표로 | 새 챕터 = 표에 한 줄 + 맵 |
| B. 맵 종류 표 | `Sim/MapKinds.cs` — 맵 종류마다 배경음악·동굴인지·마을인지·필드 분위기·불빛. 삼항 사슬 → 표 조회 | 새 맵 종류 = 표에 한 줄 |
| C. GameRoot 쪼개기 | 다시 쓰는 부품으로 떼어 내기: ① 필드 분위기(떠다니는 것·나비·불빛) ② 대화창(타자기·얼굴·넘기기) ③ 고르기 메뉴. 각자 필요한 것만 받는 클래스로 | 다른 화면·모드에서 재사용 |
| D. 테스트 정리 | SmokeTest 를 영역별 partial 파일로 (전투·맵·퀘스트·세이브·데이터) | 찾기·고치기 쉽게 |

각 단계 끝에 커밋, 이 문서에 결과를 적는다.

## 결과
- **A. 챕터 표 (2026-10-10 완료)**: `Sim/Chapters.cs` (ChapterDef: 맵·마을·출발 자리·합류·보스·대사·완료 카드·지금 할 일),
  `PlayerState.BossBeaten(n)/SetBossBeaten(n)`, `WorldMap.Build(kind)`. 없앤 것: StartChapter2~7, Chapter2~7Routine, Chapter2~7ClearRoutine·EndingRoutine,
  Boss2~7Routine·BossGateRoutine, 보스 이긴 뒤 7갈래·완료 카드·지금 할 일·맵 고르기(SurfaceMap·ForChapter·ForPlayer·HasTown)·보스 칸 밟기 사슬,
  맵 변수 12개 → `worldMaps` 사전. 대화 조건 boss2~7 → bossN 일반화. 스모크 ChapterTableTests 추가. 프로브 통과.
  **새 챕터 넣는 법**: Chapters.cs 에 ChapterDef 한 줄 + WorldMap 맵 함수 + MapKind + dialogue.json 대사 + (세이브에 BossNDown 칸 하나, BossBeaten switch 한 줄).
- **B. 맵 종류 표 (2026-10-10 완료)**: `Sim/MapKinds.cs` (MapKindInfo: 노래·전투 배경·표지판 대사·동굴/마을·맵 밖 칸·겁 레벨·몬스터 구역·가게·출발/샘물/표지판/가게 칸),
  MapPainter `painters` 사전. 정리한 사슬: SpawnPoint·Spring·SignPos·ShopPos·IsCaveLike·IsTown·맵 밖 칸(At)·MapSong·전투 배경·표지판 대사·TimidLevel·ZoneMap/ZoneKind·가게 이름·RenderBase.
  스모크 MapKindTableTests 추가. 맵 그림 픽셀 비교 같음, 프로브 통과.
  **새 맵 종류 넣는 법**: MapKind 에 이름 + MapKinds.cs 한 줄 + WorldMap 맵 함수(+Build) + MapPainter painters 한 줄 + (있으면) props.json 테마.
- **C. GameRoot 쪼개기 (2026-10-10 완료)**: 다시 쓰는 부품 5개 —
  `ParticleLayer`(필드·전투 공용 입자, 사라지는 모양 옵션), `FieldAmbience`(맵마다 떠다니는 것 표 + 보스 뒤 바뀌는 것 + 나비 수),
  `MessageWindow`(대화창: 띄우기·얼굴·한 글자씩), `ChoiceMenu`(선택창: ↑↓·스크롤·클릭·취소), `GameRoot.Objects`(상태 오브젝트, 소품 데이터화 5단계).
  GameRoot 는 "어디에·언제"만 정하고 그리는 일은 부품이. 프로브 통과.
- **D. 테스트 정리 (2026-10-10 완료)**: SmokeTest 2,950줄 → 핵심(680) + Chapters·Battle·Data·Save partial 파일. 검사 305개 그대로.

## 다음에 하면 좋은 것
- 퀘스트·이벤트 루틴(Quests·Holes·Npcs)의 공통 "대사 → 연출 → 보상" 흐름을 하나로
- 챕터 표·맵 종류 표를 JSON 으로 (지금은 C# 표 — 좌표가 WorldMap 코드에 묶여 있어서)
- **E. 전투 연출 정리 (2026-10-11 완료)**: GameRoot.Battle 1,640줄 → Battle(뼈대 540)·BattleTurns(우리 편 차례)·BattleEnemy(적 차례·기믹·상태 이상)·BattleHud(HP 막대·이름표)·BattleFx(이펙트).
  순수 계산 부품: `MonsterMotion`(필드 걸음새·전투 등장/공격 모션·걸음새별 공격 모습·소리), `BattleLayout`(인원수별 자리·크기). 스모크 BattlePresentationTests 추가,
  TownTests 안에서 이어 부르던 테스트 35개를 RunAll 로. 프로브·빌드 통과.
