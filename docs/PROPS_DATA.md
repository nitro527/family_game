# 맵 소품 데이터 (props.json) — 2026-10-10

맵에 놓는 물건은 코드가 아니라 `unity/GirlAdventure/Assets/Resources/Data/props.json` 에서 관리한다.
그림은 `Resources/Tiles/Props/<Id>.png` (원본 `assets/props/gen`), 읽는 코드는 `Sim/PropCatalog.cs` · `View/MapPainter.cs` · `View/GameRoot.Objects.cs`.

| 칸 | 하는 일 | 예 |
|---|---|---|
| `Props` | 소품 목록: 그리는 설명(Prompt)·맵에서의 폭(Width)·시드. `tools/props_gen.py` 가 이걸로 Qwen 그림을 만든다 | oak0, castle, streetlamp |
| `Themes[].Scatter` | 맵 종류마다 바닥 장식 (어느 칸 글자에, 어떤 소품을, 얼마나) | 초원 들꽃·이끼 바위 |
| `Themes[].Tiles` | 칸 글자 → 소품 규칙 (둘 중 고르기·흔들림·밑 그림자·여러 자리·덩어리 왼쪽 위·그리는 차례 Pass) | 숲 'T' = oak0/oak1, 사막 'Y' = 선인장 |
| `Themes[].Secret` | 숨은 빈터('m') 반짝이 | 사막 모래 별 |
| `Placed` | 한 번만 놓는 장식 (칸 좌표, Block 이면 그 칸을 막음) | 눈꽃 마을 가로등·썰매 |
| `Objects` | 상태에 따라 바뀌는 오브젝트 (겹 Layers, 조건 ShowIf/TintIf/AnimIf) | 동굴 철창·마법진의 엄마 |

## 새 장식 넣기
1. `Props` 에 한 줄 (Id, Width, Seed, Prompt) → ComfyUI 켜고 `python tools/props_gen.py <Id>`
2. `Placed` 에 한 줄 (Map, Prop, X, Y, Dx 24, Dy 46, Block, Under 그림자) — 칸 좌표는 `MapExport.Run` 의 `unity/playtest/maps/*.txt`
3. `SmokeTest.RunAll` (목록·그림·테마·조건 검사) → `MapExport.Run` 으로 그림 확인

조건 이름: BrotherJoined, MomJoined, BossDown, Boss2Down~Boss7Down, DoorOpen, `Opened:아이디`, 앞에 `!`, 여럿은 `&`.
움직임(Anim): sob(훌쩍), cheer(통통·흔들), bob(둥실).
코드에 남은 것: 집·천막·우물·표지판처럼 칸 좌표가 게임 규칙(WorldMap)에 묶인 것, 통나무 쌍·성벽처럼 모양이 특별한 것, 열린 성문·보물상자 그림.
