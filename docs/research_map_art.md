# 맵 그래픽 품질 조사 (2026-09-26)

사용자 요청: "최신 프로젝트 참고해서 맵 디자인을 더 멋지게 — 지금은 너무 올드하고 단순"

## 요즘 2D RPG 맵이 "요즘 것"처럼 보이는 이유
| 요소 | 사례 | 우리 적용 |
|---|---|---|
| **조명** — 장면을 어둡게 깔고 빛이 있는 곳만 밝힘 | Sea of Stars(자체 렌더 파이프라인의 동적 조명), Eastward(2D 라이트 + 블룸) | 동굴: 저해상도 어둠 텍스처(알파 = 1 − 빛)로 셰이더 없이 조명 효과. 주인공 빛(일렁임) + 수정·샘물·횃불 고정 빛 + 색 번짐 |
| **타일 격자가 안 보임** — 오토타일/경계 블렌딩 | 대부분의 최신 타일셋(오토타일) | 타일 마스크를 부드럽게 보간 + 노이즈로 흔들어 흙길·물가·동굴벽 경계를 곡선으로 |
| **깊이감** — 물체 그림자, 3/4 시점 절벽면 | Eastward, 픽셀 동굴 타일셋 | 나무·성의 그림자를 오른쪽 아래로 드리움, 동굴 벽은 윗면(어두움) + 절벽면(층무늬) + 모서리 하이라이트 |
| **색 얼룩** — 한 가지 초록이 아니라 여러 겹 노이즈 | 대부분 | 풀밭·바닥을 3단 fbm 노이즈로 얼룩지게, 물 깊이별 색 |
| **살아있는 화면** — 떠다니는 입자, 구름 그림자, 비네트 | Sea of Stars, 코지 RPG 류 | 초원: 바람에 날리는 꽃잎 + 흘러가는 구름 그림자, 동굴: 빛 먼지(반딧불), 화면 가장자리 비네트 |
| **빛나는 수정 동굴** | 크리스탈 동굴 배경 에셋들 | 절벽면에 박힌 청록/분홍 수정 + 바닥에 번진 빛 |

## 구현 위치
- `Assets/Scripts/View/MapPainter.cs` — 맵 전체 렌더 (초원/동굴), `Lights()` 가 조명 위치 제공
- `Assets/Scripts/View/GameRoot.Lighting.cs` — 동굴 어둠 + 빛, 비네트
- `Assets/Scripts/View/GameRoot.FieldFx.cs` — 물결, 꽃잎/빛 먼지, 구름 그림자
- 비용: 맵 그리기 초원 ≈1.6초, 동굴 ≈1.1초 (처음 한 번). 필요하면 빠른 경로 최적화 (백로그)

## 다음에 해볼 것
- 나무 수관이 주인공을 가리는 앞/뒤 겹침 (y 정렬 + 수관 레이어 분리)
- 물 반사(주인공이 물가에 비침), 해 질 녘 색보정
- URP 2D Lights 로 전환 검토 (노멀맵 없이도 가능) — 지금 방식이 충분하면 보류

## 출처
- [Sea of Stars 프레스킷 (Sabotage Studio)](https://sabotagestudio.com/presskits/sea-of-stars/)
- [Unity Learn — 2D Lighting for Pixel Art](https://learn.unity.com/course/2d-lighting-for-pixel-art)
- [Unity Manual — Introduction to 2D lighting in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Lights-2D-intro.html)
- [Unity Discussions — 픽셀 아트의 광원 반경](https://forum.unity.com/threads/achieving-light-radius-around-light-sources-in-pixel-art.429779/)
- [itch.io 오토타일 타일셋 모음](https://itch.io/game-assets/tag-autotile/tag-tileset)
- [Crystal Cave Pixel Art Backgrounds](https://unityunreal.com/game-dev-resources/2d-textures-backgrounds/9693-crystal-cave-pixel-art-backgrounds.html)
