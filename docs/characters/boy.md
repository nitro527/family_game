# 막내 (아들) — 용사 (Hero)

> 넥타이를 맨 세상에서 제일 작은 용사. 파티의 리더이자 모두가 지키고 싶은 존재.

레퍼런스: [`reference/boy_ref.jpg`](reference/boy_ref.jpg)

## 닮게 만들 핵심 특징
| 부위 | 특징 (반드시 유지) |
|---|---|
| 헤어 | **눈썹 위 일자로 자른 바가지 머리**, 검은 머리 — 가장 중요한 식별 포인트 |
| 얼굴 | **아주 동그랗고 통통한 볼**, 약간 튀어나온 귀 |
| 눈·입 | 작은 아몬드형 눈, 도톰하게 삐죽 내민 입술 |
| 표정 | 진지하고 뚱한 표정 (웃을 땐 활짝) |
| 나이감 | 3살 정도의 아기 (약 2.5~3등신) |

## 의상 & 소품 (최종 — 넥타이는 캐릭터화하니 어색해서 제외)
- 금색 테두리에 사자 문양이 들어간 **하늘색 용사 튜닉**, 짧은 퍼프 소매
- 금색 버클 가죽 벨트 + 파우치, 가죽 장갑
- 남색 레깅스, 갈색 모험가 부츠
- 어깨에 둥근 금색 고정 장식이 달린 **주황빛 금색 망토**
- **빨간 보석 금색 서클릿** — 반드시 앞머리 위에 (바가지 앞머리가 보이게)
- 무기: 검 + 둥근 나무 방패

## 컬러 팔레트
| 용도 | 색 |
|---|---|
| 머리 | `#1E1A24` |
| 튜닉 | `#5FB0E0` |
| 레깅스 | `#2C3A70` |
| 망토 | `#F0B030` |
| 서클릿 보석 | `#E8404A` |

## 게임 설정 (초안)
- **RPG**: 모든 능력치 균형 · 번개 계열 전용 기술 · 성장 속도 최고
- **스킬 콘셉트**: `아기 용사 베기`(기본 공격) · `떼쓰기`(적 전체 혼란) · `막내의 번개`(용사 전용 번개 마법)
- **카드 게임**: 리더 카드 — 파티 전체에 효과를 주는 핵심 카드

---

## Gemini 프롬프트

### ① 메인 일러스트 — ✅ 확정: `assets/characters/gen/boy/boy_main.png`
확정까지의 과정 (중간 시안 파일은 정리하며 삭제됨):
1. v1: 레퍼런스 사진 + DQ 화풍 프롬프트 → **얼굴이 가장 좋았음** (진지한 눈썹, 삐죽 입, 통통한 볼)
2. v2: 같은 대화에서 의상 교체 (넥타이 제거 → 하늘색 용사 튜닉)
3. v4: **새 대화에 v1 + v2 이미지 두 장을 올리고 합성** ↓
```
Combine these two images into one final character. Use IMAGE 1 (first image) for the FACE, head, expression,
body proportions and art style - keep his face exactly as in image 1: the determined serious eyes and eyebrows,
pouty lips, chubby cheeks, sticking-out ears, bold Dragon Quest manga linework. Use IMAGE 2 (second image) for
the OUTFIT only: sky-blue hero tunic with gold trim and lion emblem, short puffy sleeves, brown leather belt with
gold buckle and pouch, dark blue leggings, brown boots, leather gloves, orange-gold cape with round gold shoulder
clasps. No white shirt, no necktie. One change to the head: move the gold circlet with the red gem slightly higher
so his straight blunt bowl-cut bangs are clearly visible across the forehead below it. He holds the sword and
round wooden shield. Full body, brave hero pose, plain white background, no text.
```
> 교훈: 한 대화에서 계속 고치면 이전 그림에 끌려감 → **좋은 부분이 있는 시안들을 새 대화에 같이 올려 합성**하는 게 효과적.

### ② 턴어라운드 (확정된 메인 일러스트 업로드)
```
Create a character turnaround sheet of this exact same character: front view, side view, and back view,
standing side by side in a neutral pose, same outfit and colors, same art style.
Plain white background, no text.
```

### ③ 표정 시트 (확정된 메인 일러스트 업로드)
```
Create an expression sheet of this exact same character: 6 head-and-shoulders portraits in a 3x2 grid —
serious pout, huge happy laugh, angry tantrum, surprised, crying loudly, sleepy.
Same face, bowl cut, circlet and art style. Plain white background, no text.
```

### ④ 카드 일러스트 (확정된 메인 일러스트 업로드)
```
Draw this exact same character raising his sword to the sky as lightning strikes it,
cape flying, heroic determined face. Stormy sky over a fantasy castle in the background.
Vertical 2:3 aspect ratio card illustration, same art style, no text.
```

### ⑤ 얼굴 아이콘 (확정된 메인 일러스트 업로드)
```
Draw a bust portrait of this exact same character, facing forward with a serious little pout,
centered for a square RPG status icon. Same art style, plain white background, no text.
```

### 수정 요청 문구 (닮지 않았을 때)
- `Make the bowl cut rounder with perfectly straight bangs above the eyebrows.`
- `Make his face rounder and chubbier, like a 3-year-old toddler.`
- `Make his lips fuller and slightly pouting.`
