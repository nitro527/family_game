"""맵 물건 그림 목록(props.json) → Qwen 으로 그리고 → 맵 소품으로 (tools/qwen_gen.py + tools/make_prop.py).

  python tools/props_gen.py            # 아직 없는 것만
  python tools/props_gen.py well sign  # 이름을 주면 그것만 다시 (원본 assets/props/gen 덮어씀)

목록은 unity/GirlAdventure/Assets/Resources/Data/props.json (2026-10-10 소품 데이터화 — 게임과 같은 목록).
새 소품은 그 파일 "Props" 에 한 줄 넣고 실행. ComfyUI 서버(127.0.0.1:8188)가 떠 있어야 한다.
From 이 있으면 그 그림을 참고(--ref)로 고쳐 그리고, Derived 는 다른 도구(tools/make_cage.py)가 만든다.
"""
import json, os, subprocess, sys

sys.path.insert(0, os.path.dirname(__file__))
from make_prop import make, GEN

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CATALOG = os.path.join(ROOT, 'unity', 'GirlAdventure', 'Assets', 'Resources', 'Data', 'props.json')


def load():
    with open(CATALOG, encoding='utf-8') as f:
        return json.load(f)


def main():
    data = load()
    only = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    for p in data['Props']:
        name = p['Id']
        if p.get('Derived'):
            continue
        raw = os.path.join(GEN, name + '.png')
        if only and name not in only:
            continue
        if not only and os.path.exists(raw):
            continue
        os.makedirs(GEN, exist_ok=True)
        args = [sys.executable, os.path.join(here, 'qwen_gen.py')]
        if p.get('From'):
            args += [p['Prompt'], '--ref', os.path.join(GEN, p['From'] + '.png')]
        else:
            args += [data['Style'].format(p['Prompt'])]
        args += ['-o', raw, '--seed', str(p.get('Seed', 2))]
        if p.get('GenW'):
            args += ['--w', str(p['GenW']), '--h', str(p['GenH'])]
        subprocess.run(args, check=True)
        if p.get('From'):
            print(name, '→ tools/make_cage.py 로 맞추기')
        else:
            print(name, make(raw, name, p['Width']))


if __name__ == '__main__':
    main()
