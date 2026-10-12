"""큰 이벤트 짧은 애니메이션(tools/event_movies.py)에 쓸 배경 그림 — 로컬 Qwen (2026-10-13).

  python tools/event_art_gen.py              # 아직 없는 것만
  python tools/event_art_gen.py clear_ch1    # 그것만 다시

ComfyUI(127.0.0.1:8188)가 떠 있어야 한다. 결과: assets/movie/art/<이름>.png (1344×768, 사람 없는 배경).
"""
import os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'assets', 'movie', 'art')
STYLE = ("An empty environment background plate (scenery only) for an animated film: {0} "
         "Hand-painted storybook illustration style, soft warm light, gentle colors, simple shapes. "
         "Completely empty scene: absolutely no people, no children, no animals, no creatures, no faces, no figures, no text.")
ART = {
    'clear_ch1': 'a cozy little fantasy village at night under a sky full of twinkling stars and golden starlight streams rising up from the meadow.',
    'clear_ch2': 'a big peaceful underground crystal cave glowing with blue and pink crystals, soft light beams from above, a small calm pond.',
    'clear_ch3': 'a desert oasis where sparkling blue water is flowing again into a stream between palm trees and sand dunes, bright blue sky.',
    'clear_ch4': 'a calm sleeping volcano at sunset with gentle steam, warm hot spring pools and little stone houses at its foot.',
    'clear_ch5': 'a snowy mountain valley after a snowstorm, clear night sky with a beautiful green and purple aurora, snowy pine trees.',
    'clear_ch6': 'a grand magical library hall with tall bookshelves, glowing open books floating in the air and warm golden candle light.',
    'clear_ch7': 'a gigantic ancient friendly tree in a forest glade covered with pink blossoms, fireflies and soft sun rays.',
    'join_bro': 'a cave tunnel opening to bright daylight, a broken open iron cage on the side, sparkles in the air.',
    'join_dad': 'a winding country road over green hills at sunrise toward a far volcano land, morning mist and sunbeams.',
    'join_mom': 'a mysterious library floor with a huge glowing golden magic circle on the wooden floor, floating sparkles and books.',
    # 게임 시작 애니메이션 (tools/family_movie.py) 배경 — 사용자 "이미지 모델로 퀄리티 업그레이드"
    'intro_storm': 'a wide grassy plain under a dark stormy night sky with heavy purple clouds and rain, dramatic but not too scary.',
    'intro_clear': 'a wide grassy plain after the rain, bright blue sky with fluffy white clouds, fresh shiny grass and a few wildflowers.',
    'intro_sunset': 'a wide open grassy plain at sunset with an orange pink and purple sky and the sun touching the horizon.',
    'intro_night': 'a quiet meadow at night under a deep blue starry sky with a crescent moon, dark silhouettes of hills.',
    'intro_twilight': 'a dreamy twilight sky with soft pink and purple clouds and twinkling stars over gentle rolling hills.',
}


def main():
    only = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    os.makedirs(OUT, exist_ok=True)
    for name, what in ART.items():
        out = os.path.join(OUT, name + '.png')
        if only and name not in only: continue
        if not only and os.path.exists(out): continue
        subprocess.run([sys.executable, os.path.join(here, 'qwen_gen.py'), STYLE.format(what), '-o', out, '--seed', '9', '--w', '1344', '--h', '768'], check=True)
        print(name, 'ok', flush=True)


if __name__ == '__main__':
    main()
