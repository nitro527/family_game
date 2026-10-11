"""stories.json 만들기 (이야기 내용은 모두 ✏️ 임시 — docs/EVENTS_PLAN.md).  python tools/stories_gen.py unity/GirlAdventure/Assets/Resources/Data/stories.json"""
import json, sys

stories, errands, photos, talks = [], [], [], []

def T(id, *lines):
    """대사: 문자열 또는 (글, 얼굴)."""
    talks.append({"Id": id, "Lines": [{"Text": l, "Face": "smile"} if isinstance(l, str) else {"Text": l[0], "Face": l[1]} for l in lines]})
    return id

def S(id, on, steps, need="", ch=0, repeat=False, pri=0):
    stories.append({"Id": id, "On": on, "Need": need, "Chapter": ch, "Repeat": repeat, "Priority": pri, "Steps": steps})

def st(do, a=None, b=None, n=0):
    d = {"Do": do}
    if a is not None: d["A"] = a
    if b is not None: d["B"] = b
    if n: d["N"] = n
    return d

# ---------------------------------------------------------------- 사냥 부탁 (받기 → 다 혼내 주기 → 알리기)
def hunt(id, ch, giver, who, enemy, goal, ask, why, thanks, rewards, title, need=""):
    T(f"st_{id}_ask", (f"{who}: {ask}", "surprised"), f"{who}: {why}")
    T(f"st_{id}_no", f"{who}: 괜찮아, 나중에 시간 나면 부탁할게!")
    T(f"st_{id}_progress", f"{who}: {enemy}, 지금 {{cnt:{id}}}마리 혼내 줬구나! {goal}마리만 부탁해.")
    T(f"st_{id}_done", (f"{who}: 와, {enemy} {goal}마리를 다 혼내 줬구나! {thanks}", "laugh"), ("고마움의 선물을 받았다!", "laugh"))
    base = f"flag_{id}_on,!flag_{id}_done"
    S(f"{id}_ask", f"talk:{giver}", [st("ask", f"st_{id}_ask", "좋아, 맡겨 줘!|다음에"), st("flag", f"{id}_on"),
        st("note", f"새 부탁: {title}"), st("end"), st("label", "no"), st("talk", f"st_{id}_no")],
      need=f"!flag_{id}_on" + ("," + need if need else ""), ch=ch, repeat=True)
    S(f"{id}_win", f"win:{enemy}", [st("count", id), st("need", f"cnt_{id}>={goal}", "more"),
        st("note", f"{title} 다 했어요! {who}에게 알려 주자"), st("end"), st("label", "more"), st("note", f"{title} ({{cnt:{id}}}/{goal})")],
      need=base + f",!cnt_{id}>={goal}", ch=ch, repeat=True)
    S(f"{id}_report", f"talk:{giver}", [st("sfx", "chest"), st("talk", f"st_{id}_done")] + rewards
      + [st("flag", f"{id}_done"), st("note", f"부탁 끝: {title}")],
      need=base + f",cnt_{id}>={goal}", ch=ch)
    S(f"{id}_wait", f"talk:{giver}", [st("talk", f"st_{id}_progress")], need=base + f",!cnt_{id}>={goal}", ch=ch, repeat=True)
    errands.append({"Id": id, "Kind": "사냥", "Title": title, "Show": f"flag_{id}_on", "Done": f"flag_{id}_done", "Count": id, "Goal": goal})

# ---------------------------------------------------------------- 배달 부탁 (받기 → 전하기 → 답례)
def deliver(id, ch, giver, gname, to, toname, thing, ask, given, thanks, rewards, title, need=""):
    T(f"st_{id}_ask", (f"{gname}: {ask}", "surprised"), f"{thing}을(를) 받았다! {toname}에게 전해 주자.")
    T(f"st_{id}_no", f"{gname}: 그래, 바쁘면 다음에 부탁할게.")
    T(f"st_{id}_wait", f"{gname}: {toname}에게 {thing} 전해 줬어? 부탁해!")
    T(f"st_{id}_give", f"{toname}에게 {thing}을(를) 건넸다.", (f"{toname}: {given}", "laugh"), f"{toname}: {gname}에게 고맙다고 전해 줘!")
    T(f"st_{id}_thanks", (f"{gname}: 잘 전해 줬구나! {thanks}", "laugh"), ("답례를 받았다!", "laugh"))
    S(f"{id}_ask", f"talk:{giver}", [st("ask", f"st_{id}_ask", "응, 전해 줄게!|다음에"), st("flag", f"{id}_on"),
        st("note", f"새 부탁: {title}"), st("end"), st("label", "no"), st("talk", f"st_{id}_no")],
      need=f"!flag_{id}_on" + ("," + need if need else ""), ch=ch, repeat=True)
    S(f"{id}_give", f"talk:{to}", [st("sfx", "select"), st("talk", f"st_{id}_give"), st("flag", f"{id}_given"),
        st("note", f"{gname}에게 알려 주자")], need=f"flag_{id}_on,!flag_{id}_given", ch=ch, pri=2)
    S(f"{id}_wait", f"talk:{giver}", [st("talk", f"st_{id}_wait")], need=f"flag_{id}_on,!flag_{id}_given", ch=ch, repeat=True)
    S(f"{id}_thanks", f"talk:{giver}", [st("sfx", "chest"), st("talk", f"st_{id}_thanks")] + rewards
      + [st("flag", f"{id}_done"), st("note", f"부탁 끝: {title}")], need=f"flag_{id}_given,!flag_{id}_done", ch=ch)
    errands.append({"Id": id, "Kind": "배달", "Title": title, "Show": f"flag_{id}_on", "Done": f"flag_{id}_done"})

R = lambda *s: list(s)
gold = lambda n: st("gold", None, None, n)
give = lambda i, n=0: st("give", i, None, n)

# 챕터 1 — 초원·별빛 마을
hunt("rabbit", 1, "tips", "리본 젤리", "뿔토끼", 3, "꽃밭에 뿔토끼들이 와서 꽃을 마구 뜯어 먹어!",
     "뿔토끼 세 마리만 살짝 혼내 줄래? 초원 멀리에 가끔 나와.", "이제 꽃들이 다시 활짝 필 거야!", R(give("potion", 2), gold(40)), "뿔토끼 혼내 주기")
deliver("seed", 1, "chief", "촌장 젤리", "guide", "밀짚모자 젤리", "꽃씨 주머니",
        "초원의 밀짚모자 젤리가 꽃씨를 기다리고 있어. 이 주머니 좀 전해 줄래?",
        "와, 꽃씨다! 길가에 심으면 별 모양 꽃이 핀대!", "이건 별빛 마을 사탕이야.", R(give("candy", 3), gold(30)), "꽃씨 전하기")
# 챕터 2 — 동굴 (광부 젤리: 찾기 부탁을 받은 뒤에)
hunt("mole", 2, "miner", "광부 젤리", "두더지", 3, "두더지들이 광산 굴을 여기저기 막 파 놨어!",
     "두더지 세 마리만 혼내 줘. 그래야 길이 안 무너지지.", "이제 굴이 튼튼해!", R(give("potion", 2), gold(80)), "두더지 혼내 주기",
     need="opened_q_crystals")
# 챕터 3 — 사막·오아시스
hunt("scorpion", 3, "wellkeeper", "우물지기 젤리", "전갈", 4, "전갈들이 우물 근처까지 와서 물 길으러 못 가겠어!",
     "전갈 네 마리만 혼내 줄래? 사막에 나가면 있어.", "이제 우물물을 마음껏 길을 수 있어!", R(give("moon", 1), gold(120)), "전갈 혼내 주기")
deliver("tea", 3, "tentlady", "천막 장수 젤리", "elder", "마을 어른 젤리", "시원한 박하차",
        "마을 어른 젤리가 더위를 먹었대. 이 시원한 박하차 좀 갖다 드릴래?",
        "아이고 시원하다! 기운이 번쩍 나는구나.", "덕분에 장사가 잘될 것 같아!", R(give("potion", 3), gold(60)), "박하차 갖다 드리기")
# 챕터 4 — 용암 땅·온천 마을
hunt("lizard", 4, "springkeeper", "온천지기 젤리", "불도마뱀", 3, "불도마뱀들이 온천물을 너무 뜨겁게 데워 놔!",
     "불도마뱀 세 마리만 혼내 줘. 그럼 딱 좋은 온도가 될 거야.", "아, 딱 좋은 온천물이다!", R(give("moon", 2), gold(150)), "불도마뱀 혼내 주기")
deliver("lunch", 4, "lava_kid", "꼬마 숯 젤리", "smith", "대장장이 젤리", "숯불 도시락",
        "우리 아빠(대장장이 젤리)가 점심을 깜빡했어. 이 도시락 좀 갖다줄래?",
        "오, 우리 꼬맹이가 싸 준 도시락! 힘이 불끈 솟는다!", "아빠가 맛있게 먹었대? 헤헤!", R(give("candy", 3), gold(80)), "숯불 도시락 배달")
# 챕터 5 — 눈 들판·눈꽃 마을
hunt("snowrabbit", 5, "furrier", "털옷 가게 젤리", "눈토끼", 3, "장난꾸러기 눈토끼들이 털실 뭉치를 물고 달아났어!",
     "눈토끼 세 마리만 혼내 주면 털실을 돌려받을 수 있을 거야.", "털실이 돌아왔어! 이걸로 목도리를 뜰게.", R(give("moon", 2), gold(180)), "눈토끼 혼내 주기")
deliver("sled", 5, "skater", "스케이트 젤리", "snow_elder", "눈꽃 마을 어른 젤리", "썰매 대회 초대장",
        "눈꽃 마을 어른께 썰매 대회 초대장을 드려야 하는데, 내가 지금 연습 중이라… 부탁해!",
        "썰매 대회라! 허허, 나도 젊었을 땐 제일 빨랐지.", "어른께서 오신대? 신난다!", R(give("potion", 3), gold(120)), "썰매 대회 초대장")
# 챕터 6 — 도서관
hunt("ink", 6, "reader", "책벌레 젤리", "잉크 악마", 3, "잉크 악마들이 그림책에 낙서를 해 놨어!",
     "잉크 악마 세 마리만 혼내 줘. 낙서가 지워질 거야.", "그림책이 다시 깨끗해졌어!", R(give("moon", 2), gold(220)), "잉크 악마 혼내 주기")
# 챕터 7 — 숲
hunt("mushroom", 7, "spirit", "자연의 정령", "버섯 젤리", 4, "버섯 젤리들이 숲길에 포자를 퐁퐁 뿌리고 다녀.",
     "버섯 젤리 네 마리만 얌전하게 해 줄래?", "숲 공기가 다시 맑아졌어.", R(give("moon", 3), gold(260)), "버섯 젤리 혼내 주기")

# ---------------------------------------------------------------- 장기 퀘스트 1: 가족 앨범 (챕터마다 경치 좋은 곳에서 찰칵)
album = [
    ("ch1", 1, "field", (8, 21), "꽃밭 언덕", "초원 남쪽 꽃밭", "와, 꽃이 한가득! 여기서 사진 찍으면 예쁘겠다."),
    ("ch2", 2, "cave", (17, 16), "반딧불 호수", "동굴 속 반짝이는 호수", "호수에 반딧불이 비쳐서 별밤 같아!"),
    ("ch3", 3, "desert", (24, 14), "오아시스", "사막 한가운데 초록 샘", "사막 한가운데 이렇게 예쁜 샘이 있다니!"),
    ("ch4", 4, "lava", (27, 14), "용암 강 전망", "붉게 빛나는 용암 강", "용암 강이 노을처럼 붉게 반짝여! 조금 뜨겁지만 멋지다!"),
    ("ch5", 5, "snow", (16, 7), "얼음 호수", "반짝반짝 얼음 호수", "얼음 호수가 거울처럼 반짝여!"),
    ("ch6", 6, "library", (17, 4), "책 숲", "끝없는 책장 앞", "책이 하늘까지 쌓여 있어! 책 숲이다!"),
    ("ch7", 7, "forest", (10, 10), "숲속 빈터", "햇살 내리는 숲속 빈터", "햇살이 나뭇잎 사이로 반짝반짝 내려와."),
]
T("st_photo_no", "다음에 다시 와서 찍자!")
T("st_album_full", ("앨범이 가득 찼다! 일곱 장의 가족 사진이 모두 모였어요.", "laugh"),
  ("사진첩 사이에서 작은 목걸이가 떨어졌다… 가족 사진 로켓이다!", "surprised"))
for id, ch, m, (x, y), title, cap, wow in album:
    photos.append({"Id": id, "Title": title, "Caption": cap, "Chapter": ch})
    T(f"st_photo_{id}", (wow, "surprised"), "다 같이 사진 찍을까?")
    T(f"st_photo_{id}_done", ("찰칵! 가족 앨범에 사진을 붙였다. ({photos}/7)", "laugh"))
    S(f"photo_{id}", f"step:{m}:{x},{y}", [
        st("ask", f"st_photo_{id}", "찰칵! 찍자|다음에"), st("photo_shot"), st("photo", id), st("talk", f"st_photo_{id}_done"),
        st("note", f"가족 앨범: {title} ({{photos}}/7)"),
        st("need", "photos>=7,!flag_album_done"), st("sfx", "lvup"), st("cutin", "Cards/ch6_clear", "가족 앨범 완성!"), st("talk", "st_album_full"), give("family_locket"),
        st("flag", "album_done"), st("note", "부탁 끝: 가족 앨범 완성!"), st("end"),
        st("label", "no"), st("talk", "st_photo_no")], need=f"!photo_{id}", ch=ch, repeat=True)
errands.append({"Id": "album", "Kind": "앨범", "Title": "가족 앨범 모으기", "Show": "photos>=1", "Done": "flag_album_done",
                "Count": "", "Goal": 0, "Reward": "family_locket"})

# ---------------------------------------------------------------- 장기 퀘스트 2: 떠돌이 우체부 젤리 (마을 4곳, 마을 안 배달)
post = [
    ("post1", 1, "village", "kid", "꼬마 젤리", "멀리 사는 할머니 젤리",
     "할머니다! \"우리 꼬마, 밥 잘 먹고 있지? 겨울에 놀러 갈게.\" …헤헤, 보고 싶다!"),
    ("post2", 3, "oasis", "elder", "마을 어른 젤리", "어릴 적 친구",
     "오래된 친구 녀석이구먼! \"모래바람 조심하게, 다음 달에 낙타 타고 들르지.\" 허허."),
    ("post3", 4, "lavatown", "lava_elder", "온천 마을 어른 젤리", "눈꽃 마을 어른 젤리",
     "눈꽃 마을 친구로군. \"우리 마을은 너무 추워! 온천에 몸 녹이러 갈게.\" 허허, 기다리겠네."),
    ("post4", 5, "snowtown", "snow_kid", "꼬마 눈송이 젤리", "온천 마을 꼬마 숯 젤리",
     "숯 젤리 친구다! \"눈사람 만드는 법 알려 줘! 나는 따뜻한 돌멩이 줄게.\" 와, 친구 생겼다!"),
]
T("st_post_hello", ("우체부 젤리: 헉헉… 편지가 너무 많아서 길을 잃었어!", "surprised"),
  "우체부 젤리: 나는 마을마다 떠도는 우체부야. 편지 하나만 대신 전해 줄래?")
T("st_post_no", "우체부 젤리: 그래… 다른 집부터 돌고 올게. 헉헉.")
T("st_post_wait", "우체부 젤리: 편지 전해 줬어? 받는 사람 이름이 봉투에 적혀 있어!")
T("st_post_thanks", ("우체부 젤리: 고마워! 편지 받은 얼굴, 좋았지? 그게 우체부의 기쁨이야.", "laugh"),
  ("우체부 젤리: 이건 배달 도장이야. 마을 네 곳 도장을 모으면 좋은 걸 줄게! ({cnt:post}/4)", "smile"))
T("st_post_full", ("우체부 젤리: 와, 배달 도장 네 개를 다 모았구나!", "laugh"),
  ("우체부 젤리: 이건 내가 아끼던 우체부 가방이야. 이제 너도 어엿한 배달부야!", "laugh"))
T("st_post_idle", "우체부 젤리: 오늘도 편지 배달! 다음 마을에서 또 만나!")
for id, ch, town, to, toname, sender, read in post:
    T(f"st_{id}_letter", f"우체부 젤리: 이건 {toname}에게 온 편지야. {sender}가 보냈대!")
    T(f"st_{id}_give", f"{toname}에게 편지를 건넸다.", (f"{toname}: {read}", "laugh"))
    S(f"{id}_ask", f"talk:{id}", [st("flag", "post_met"), st("talk", "st_post_hello"), st("talk", f"st_{id}_letter"),
        st("ask", None, "응, 전해 줄게!|다음에"), st("flag", f"{id}_on"), st("note", f"새 부탁: {toname}에게 편지"), st("end"),
        st("label", "no"), st("talk", "st_post_no")], need=f"!flag_{id}_on", ch=ch, repeat=True)
    S(f"{id}_wait", f"talk:{id}", [st("talk", "st_post_wait")], need=f"flag_{id}_on,!flag_{id}_given", ch=ch, repeat=True)
    S(f"{id}_give", f"talk:{to}", [st("sfx", "select"), st("talk", f"st_{id}_give"), st("flag", f"{id}_given"),
        st("note", "우체부 젤리에게 알려 주자")], need=f"flag_{id}_on,!flag_{id}_given", ch=ch, pri=2)
    S(f"{id}_thanks", f"talk:{id}", [st("count", "post"), st("sfx", "coin"), st("talk", "st_post_thanks"), gold(50 * ch),
        st("flag", f"{id}_done"), st("note", "배달 도장 ({cnt:post}/4)"),
        st("need", "cnt_post>=4,!flag_post_gift"), st("sfx", "lvup"), st("talk", "st_post_full"), give("mail_bag"),
        st("flag", "post_gift"), st("note", "부탁 끝: 떠돌이 우체부의 편지")],
      need=f"flag_{id}_given,!flag_{id}_done", ch=ch)
errands.append({"Id": "post", "Kind": "편지", "Title": "떠돌이 우체부의 편지", "Show": "flag_post_met", "Done": "flag_post_gift",
                "Count": "post", "Goal": 4, "Reward": "mail_bag"})

# ---------------------------------------------------------------- 맵 반응 (작은 장면)
T("st_wish_ask", "마을 한가운데 오래된 우물이다. 동전을 던지고 소원을 빌까?")
T("st_wish_yes", ("퐁당! …{name:은/는} 눈을 꼭 감고 소원을 빌었다.", "smile"), ("우물 바닥에서 반짝! 무언가 떠올랐다. 사탕이다!", "laugh"))
T("st_wish_no", "다음에 빌어야지.")
S("wish", "bump:village:18,10", [st("ask", "st_wish_ask", "10골드 던지기|그만두기"), st("need", "gold>=10", "no"),
    st("gold", None, None, -10), st("sfx", "coin"), st("talk", "st_wish_yes"), give("candy", 2), st("sparkle"), st("flag", "wished"), st("end"),
    st("label", "no"), st("talk", "st_wish_no")], need="!flag_wished", ch=1, repeat=True)

# ---------------------------------------------------------------- 이어지는 부탁 (챕터 4 대장장이 젤리: 찾기 부탁을 끝낸 뒤 세 단계)
T("st_smith1_ask", ("대장장이 젤리: 용암석 고마웠어! 그런데 하나 더… 불새 깃털이 있으면 더 좋은 걸 만들 수 있거든.", "surprised"),
  "대장장이 젤리: 불새 세 마리만 혼내 주면 깃털을 떨어뜨릴 거야. 부탁해!")
T("st_smith1_wait", "대장장이 젤리: 불새 깃털, 지금 {cnt:firebird}개야. 세 개면 돼!")
T("st_smith2_ask", ("대장장이 젤리: 깃털 세 개! 좋아, 이걸로 새 국자를 만들었어.", "laugh"),
  "대장장이 젤리: 온천지기 젤리가 국자를 부러뜨렸대. 이 새 국자 좀 갖다줄래?")
T("st_smith2_give", "온천지기 젤리에게 반짝반짝 새 국자를 건넸다.", ("온천지기 젤리: 우와, 불새 깃털 무늬 국자! 온천물이 더 따뜻해질 것 같아!", "laugh"))
T("st_smith2_wait", "대장장이 젤리: 국자는 온천지기 젤리에게 전해 줬어?")
T("st_smith3", ("대장장이 젤리: 다 해 줬구나! 너희 가족은 정말 든든해.", "laugh"),
  ("대장장이 젤리: 답례로 불꽃 망치 브로치를 만들었어. 뜨거운 불길도 끄떡없을 거야!", "laugh"), ("불꽃 망치 브로치를 받았다!", "laugh"))
S("smith1_ask", "talk:smith", [st("talk", "st_smith1_ask"), st("flag", "smith1_on"), st("note", "새 부탁: 불새 깃털 세 개")],
  need="opened_q_lavastones_done,!flag_smith1_on", ch=4)
S("smith1_win", "win:불새", [st("count", "firebird"), st("need", "cnt_firebird>=3", "more"), st("note", "불새 깃털 세 개! 대장장이 젤리에게 가자"), st("end"),
  st("label", "more"), st("note", "불새 깃털 ({cnt:firebird}/3)")], need="flag_smith1_on,!cnt_firebird>=3", ch=4, repeat=True)
S("smith1_wait", "talk:smith", [st("talk", "st_smith1_wait")], need="flag_smith1_on,!cnt_firebird>=3", ch=4, repeat=True)
S("smith2_ask", "talk:smith", [st("talk", "st_smith2_ask"), st("flag", "smith2_on"), st("note", "새 부탁: 온천지기 젤리에게 새 국자")],
  need="flag_smith1_on,cnt_firebird>=3,!flag_smith2_on", ch=4)
S("smith2_give", "talk:springkeeper", [st("sfx", "select"), st("talk", "st_smith2_give"), st("flag", "smith2_given"), st("note", "대장장이 젤리에게 알려 주자")],
  need="flag_smith2_on,!flag_smith2_given", ch=4, pri=2)
S("smith2_wait", "talk:smith", [st("talk", "st_smith2_wait")], need="flag_smith2_on,!flag_smith2_given", ch=4, repeat=True)
S("smith3", "talk:smith", [st("sfx", "lvup"), st("talk", "st_smith3"), give("smith_charm"), st("flag", "smith_done"), st("note", "부탁 끝: 대장장이 젤리의 세 가지 부탁")],
  need="flag_smith2_given,!flag_smith_done", ch=4)
errands.append({"Id": "smith", "Kind": "이어짐", "Title": "대장장이 젤리의 세 가지 부탁", "Show": "flag_smith1_on", "Done": "flag_smith_done",
                "Count": "", "Goal": 0, "Reward": "smith_charm"})

# ---------------------------------------------------------------- 맵 반응 (작은 장면)
T("st_snowman_ask", "쉼터 눈사람이 모자도 없이 추워 보인다. 털모자를 씌워 줄까?")
T("st_snowman_yes", ("눈사람에게 털모자를 씌워 줬다! 눈사람이 방긋 웃는 것 같다.", "laugh"), ("…눈사람 발밑에서 반짝! 누가 두고 간 달님 사탕이다!", "surprised"))
S("snowman_hat", "bump:snow:3,18", [st("ask", "st_snowman_ask", "씌워 주자!|그냥 두자"), st("sfx", "star"), st("talk", "st_snowman_yes"), give("moon", 1), st("sparkle"),
  st("flag", "snowman_hat"), st("end"), st("label", "no")], need="!flag_snowman_hat", ch=5, repeat=True)
T("st_shelfnote", ("책장 틈에 쪽지가 끼어 있다.", "surprised"), "\"악마들은 촛불을 무서워한다. 마법진의 주인이 오면 문이 열릴 것이다.\" …엄마 이야기일까?",
  ("쪽지 뒤에 동전이 붙어 있었다!", "laugh"))
S("shelf_note", "bump:library:9,7", [st("sfx", "peek"), st("talk", "st_shelfnote"), st("gold", None, None, 100)], ch=6)
T("st_hotspring", ("온천물에 손을 담가 보았다. 따끈따끈~ 몸이 사르르 녹는다.", "laugh"), ("모두 기운이 났다!", "laugh"))
S("hotspring_hands", "bump:lavatown:19,9", [st("sfx", "heal"), st("talk", "st_hotspring"), st("heal")], ch=4)

# ---------------------------------------------------------------- 지하실 통 속 젤리 (싸우기 차례)
T("st_barrel", ("통 뚜껑이 들썩들썩… 앗, 통 속에서 젤리가 튀어나왔다!", "surprised"))
T("st_barrel_won", ("장난꾸러기 큰 분홍 젤리가 통에서 몰래 사과를 먹고 있었나 봐.", "smile"), ("통 바닥에 할머니가 숨겨 둔 비상금이 있었다!", "laugh"))
S("cellar_barrel", "bump:cellar:9,15", [st("talk", "st_barrel"), st("flag", "cellar_barrel"), st("battle", "큰 분홍 젤리")], need="!flag_cellar_barrel")
S("cellar_barrel_won", "win:큰 분홍 젤리", [st("talk", "st_barrel_won"), st("gold", None, None, 80), st("flag", "cellar_barrel_won")],
  need="flag_cellar_barrel,!flag_cellar_barrel_won")

T("st_jam", ("오래된 잼 단지다. 뚜껑에 \"우리 손주들 몰래 먹지 말 것\"이라고 적혀 있다…", "surprised"), ("…한 숟가락만! 달콤해서 기운이 났다. 사탕도 하나 숨겨져 있었다!", "laugh"))
S("cellar_jam", "bump:cellar:30,10", [st("sfx", "coin"), st("talk", "st_jam"), st("heal"), give("candy", 1), st("sparkle")])

data = {"_doc": "이야기 장면·부탁 수첩·가족 앨범 (Sim/Stories.cs 가 읽는다, 2026-10-11). 내용은 모두 임시 — docs/EVENTS_PLAN.md. tools/stories_gen.py 로 만든다 — 손으로 고치지 말고 스크립트를 고쳐서 다시 만들기.",
        "Stories": stories, "Errands": errands, "Photos": photos, "Talks": talks}
json.dump(data, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(stories), "stories", len(errands), "errands", len(photos), "photos", len(talks), "talks")
