"""보스 밸런스 탐색 도구 (백로그 33).

Unity 전투 규칙(Sim/Battle.cs)과 SmokeTest 밸런스 시뮬레이션의 AI 를 파이썬으로 흉내 내서,
보스 체력·공격력 후보를 Unity 를 켜지 않고 빠르게 훑는다. 수치는 게임 데이터(Resources/Data/*.json)를 그대로 읽는다.

AI (SmokeTest.Fight 와 같음): HP 35% 이하면 사탕(없으면 --heal 일 때 반짝 힐), 보스면 MP 있을 때 별빛 폭풍, 아니면 리본 파이어. 60턴 넘으면 패배.
보스 앞 상태: 해당 레벨·HP/MP 가득·사탕 3개 (SmokeTest 도 레벨업 중 샘물로 사탕을 3개까지만 채우므로 늘 3개).

사용 예:
  python tools/balance.py                       # 세 보스 현재 승률 (장비 없음/있음)
  python tools/balance.py king --hp 120:160:10 --atk 9:11   # 킹 젤리 체력·공격 후보 표
  python tools/balance.py general --lv 7 --gear             # 젤리 장군, Lv7·동굴 장비
  python tools/balance.py --enemy "큰 돌멩이 젤리" --lv 6    # 아무 적이나 (보스 아닌 적은 리본 파이어만)
목표 (SmokeTest): 장비 없이 85%~98.5%, 장비 사면 99% 이상.
"""
import argparse
import json
import os
import random
import sys

DATA = os.path.join(os.path.dirname(__file__), "..", "unity", "GirlAdventure", "Assets", "Resources", "Data")

# PlayerState 상수
START_HP, START_MP, START_ATK = 22, 10, 5
HP_PER_LV, MP_PER_LV, ATK_PER_LV = 6, 3, 2
START_CANDY = 3
DODGE = 0.10        # BattleSystem.DodgeRate
MAX_TURNS = 60

# 보스별 기본 상황 (SmokeTest BalanceSimulation / BalanceSimulation2 와 같음)
SCENARIOS = {
    "king":    {"key": "Boss",    "lv": 5, "shop": "village"},
    "guard":   {"key": "Guard",   "lv": 7, "shop": None},       # 동굴에 막 들어온 Lv7, 장비 검사 없음
    "general": {"key": "General", "lv": 8, "shop": "cave"},
}


def load(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return json.load(f)


ENEMIES = load("enemies.json")
SKILL_TABLE = load("skills.json")
SKILLS = {s["Cmd"]: s for s in SKILL_TABLE["Skills"]}
ITEMS = {i["Id"]: i for i in load("items.json")["Items"]}


def find_enemy(name):
    for k in ("Boss", "Guard", "General"):
        if ENEMIES.get(k, {}).get("Name") == name:
            return ENEMIES[k]
    for k in ("Normal", "Cave"):
        for e in ENEMIES.get(k, []):
            if e["Name"] == name:
                return e
    raise SystemExit(f"모르는 적: {name}")


def gear_bonus(shop):
    """그 가게 장비를 다 사서 낀 경우 (공격, 방어)."""
    if not shop:
        return 0, 0
    atk = dfn = 0
    for it in ITEMS.values():
        if it.get("Kind") in ("weapon", "armor", "acc") and shop in it.get("Shops", "").split(","):
            atk += it.get("Atk", 0)
            dfn += it.get("Def", 0)
    return atk, dfn


def skill_power(level):
    """스킬 레벨 → 위력 배율 (skills.json PowerPerLevel, Unity SkillLevels.Power 와 같은 식)."""
    return 1 + SKILL_TABLE.get("PowerPerLevel", 0) * (max(1, min(5, level)) - 1)


def fight(lv, enemy, gear=(0, 0), heal=False, rng=random, skill=1):
    """한 판. (이겼나, 턴 수, 쓴 사탕 수). skill: 모든 주문의 스킬 레벨 (1~5)."""
    pw = skill_power(skill)
    scale = lambda v: max(1, round(v * pw))
    max_hp = START_HP + HP_PER_LV * (lv - 1)
    hp, mp = max_hp, START_MP + MP_PER_LV * (lv - 1)
    atk, dfn = START_ATK + ATK_PER_LV * (lv - 1) + gear[0], gear[1]
    candy, used = START_CANDY, 0
    ehp, boss = enemy["Hp"], enemy.get("Boss", False)
    fire, star, hl = SKILLS["Fire"], SKILLS["Star"], SKILLS["Heal"]
    candy_hp = ITEMS["candy"]["Hp"]
    for turn in range(1, MAX_TURNS + 1):
        low = hp <= max_hp * .35
        if low and candy > 0 and hp < max_hp:
            candy -= 1
            used += 1
            hp = min(max_hp, hp + candy_hp)
        elif heal and low and lv >= hl.get("Lv", 0) and mp >= hl["Mp"] and hp < max_hp:
            mp -= hl["Mp"]
            hp = min(max_hp, hp + scale(hl["Heal"] + rng.randrange(max(1, hl["Rand"]))))
        elif boss and mp >= star["Mp"]:
            mp -= star["Mp"]
            ehp -= scale(atk * star["AtkMul"] + star["Bonus"] + rng.randrange(max(1, star["Rand"])))
        else:
            mp -= fire["Mp"]
            if not (fire.get("Miss", 0) > 0 and rng.random() < fire["Miss"]):
                ehp -= scale(atk * fire["AtkMul"] + fire["Bonus"] + rng.randrange(max(1, fire["Rand"])))
        if ehp <= 0:
            return True, turn, used
        if rng.random() >= DODGE:
            hp -= max(1, enemy["Atk"] + rng.randrange(3) - dfn)
            if hp <= 0:
                return False, turn, used
    return False, MAX_TURNS, used


def win_rate(lv, enemy, gear=(0, 0), heal=False, runs=20000, seed=1, skill=1):
    rng = random.Random(seed)
    wins = turns = candies = 0
    for _ in range(runs):
        w, t, c = fight(lv, enemy, gear, heal, rng, skill)
        wins += w
        turns += t
        candies += c
    return wins / runs, turns / runs, candies / runs


def span(text, default):
    """"120:160:10" → [120,130,...,160], "11" → [11], None → [default]."""
    if not text:
        return [default]
    parts = [int(x) for x in text.split(":")]
    if len(parts) == 1:
        return parts
    step = parts[2] if len(parts) > 2 else 1
    return list(range(parts[0], parts[1] + 1, step))


def verdict(rate, gear):
    if gear:
        return "OK" if rate >= .99 else "↓ 장비 사도 불안"
    return "OK" if .85 <= rate <= .985 else ("↑ 너무 어려움" if rate < .85 else "↓ 너무 쉬움")


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")      # 윈도우 콘솔(cp949)에서 한글이 깨지지 않게
    ap = argparse.ArgumentParser(description="보스 밸런스 탐색 (Unity 전투 규칙 흉내)")
    ap.add_argument("boss", nargs="?", choices=list(SCENARIOS), help="king / guard / general (없으면 셋 다 요약)")
    ap.add_argument("--enemy", help="보스 대신 이 이름의 적 (enemies.json Name)")
    ap.add_argument("--lv", type=int, help="주인공 레벨 (기본: 보스별 SmokeTest 레벨)")
    ap.add_argument("--hp", help="적 체력 후보 (예: 120:160:10)")
    ap.add_argument("--atk", help="적 공격력 후보 (예: 9:12)")
    ap.add_argument("--gear", action="store_true", help="그 지역 가게 장비를 다 낀 상태")
    ap.add_argument("--heal", action="store_true", help="사탕이 떨어지면 반짝 힐도 씀")
    ap.add_argument("--skill", type=int, default=1, help="모든 주문의 스킬 레벨 1~5 (백로그 54, 위력 +PowerPerLevel/레벨)")
    ap.add_argument("--runs", type=int, default=20000)
    a = ap.parse_args()

    if not a.boss and not a.enemy:
        print("보스        레벨  장비없음  장비있음   (목표: 85~98.5% / 99%+)")
        for name, sc in SCENARIOS.items():
            e = ENEMIES[sc["key"]]
            r0 = win_rate(sc["lv"], e, runs=a.runs, heal=a.heal, skill=a.skill)[0]
            line = f"{e['Name']:<10} Lv{sc['lv']:<3} {r0:7.1%}"
            if sc["shop"]:
                line += f"  {win_rate(sc['lv'], e, gear_bonus(sc['shop']), a.heal, a.runs, skill=a.skill)[0]:8.1%}"
            print(line)
        return

    if a.enemy:
        enemy, lv, shop = find_enemy(a.enemy), a.lv or 5, None
    else:
        sc = SCENARIOS[a.boss]
        enemy, lv, shop = ENEMIES[sc["key"]], a.lv or sc["lv"], sc["shop"]
    gear = gear_bonus(shop) if a.gear else (0, 0)
    print(f"{enemy['Name']} (지금 HP {enemy['Hp']} 공격 {enemy['Atk']}) vs 서진 Lv{lv}"
          f"{' 장비 공+%d 방+%d' % gear if a.gear else ''}{' +반짝 힐' if a.heal else ''}, {a.runs}판")
    print(f"{'HP':>5} {'공격':>4} {'승률':>7} {'평균 턴':>7} {'사탕':>5}  판정")
    for hp in span(a.hp, enemy["Hp"]):
        for atk in span(a.atk, enemy["Atk"]):
            e = dict(enemy, Hp=hp, Atk=atk)
            rate, turns, candies = win_rate(lv, e, gear, a.heal, a.runs, skill=a.skill)
            mark = " ← 지금" if (hp, atk) == (enemy["Hp"], enemy["Atk"]) else ""
            print(f"{hp:>5} {atk:>4} {rate:7.1%} {turns:7.1f} {candies:5.2f}  {verdict(rate, a.gear)}{mark}")


if __name__ == "__main__":
    main()
