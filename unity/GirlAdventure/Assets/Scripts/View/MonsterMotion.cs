using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 몬스터 움직임 공식 모음 (2026-10-11 전투 연출 정리 — 필드 걸음새와 전투 등장·공격 모션을 화면과 떼어 한곳에, 순수 계산이라 어디서나 다시 쓴다):
    /// Gait = 그림 이름으로 걸음새 고르기, Field = 필드 한 걸음 움직임, Appear = 전투 등장, Attack = 전투 공격 모션,
    /// AttackStyle = 걸음새에 맞춘 공격 모습·길이, Sound = 공격 소리.
    /// </summary>
    public static class MonsterMotion
    {
        /// <summary>Sim 의 몬스터 목록과 화면의 젤리 스프라이트를 맞추고, 통통 튀며 걷게 한다.</summary>
        /// <summary>
        /// 필드 몬스터 걸음새 (2026-10-10 사용자 "몬스터가 단순 점프만 하는 게 아니라 다양하게"): 그림 이름으로 고른다.
        /// hop 통통(젤리·토끼) · fly 날갯짓하며 떠 있음(새·박쥐·벌) · float 둥실둥실 흔들림(유령·책·촛불) · scuttle 바닥에 붙어 다다닥(거미·전갈·벌레)
        /// · stomp 쿵쿵 무겁게(바위·골렘·예티) · waddle 뒤뚱뒤뚱(펭귄·해골·버섯) · roll 데굴데굴(돌·광석) · spin 빙글 반짝(수정·불씨)
        /// </summary>
        public static string Gait(EnemyDef def)
        {
            string a = def?.Art ?? "";
            if (a.StartsWith("jelly") || a.Contains("rabbit") || a.Contains("squirrel") || a == "cave_amber") return "hop";
            if (a.Contains("bat") || a.Contains("bee") || a.Contains("owl") || a.Contains("bird") || a.Contains("eagle") || a.Contains("firebird")) return "fly";
            if (a.Contains("ghost") || a.Contains("book") || a.Contains("candle") || a.Contains("ink") || a.Contains("flake") || a.Contains("imp")) return "float";
            if (a.Contains("spider") || a.Contains("scorpion") || a.Contains("bug") || a.Contains("salamander") || a.Contains("worm") || a.Contains("mole")) return "scuttle";
            if (a.Contains("rock") || a.Contains("ore")) return "roll";
            if (a.Contains("crystal") || a.Contains("ember") || a.Contains("icicle") || a.Contains("magma")) return "spin";
            if (a.Contains("golem") || a.Contains("yeti") || a.Contains("tree") || a.Contains("guard") || a.Contains("general") || a.Contains("beetle") || a.Contains("boss") || a.Contains("king")) return "stomp";
            if (a.Contains("penguin") || a.Contains("skeleton") || a.Contains("mushroom") || a.Contains("cactus") || a.Contains("cowboy")) return "waddle";
            return "hop";
        }

        /// <summary>걸음새 → (위로 뜸, 옆 흔들림, 기울기°, 세로 늘어남, 투명도). k = 한 칸 이동 진행(1 이면 멈춤), t = 시간, dir = 가는 쪽(-1 왼쪽).</summary>
        public static (float hop, float sway, float rot, float squash, float alpha) Field(string gait, float k, float t, int dir)
        {
            bool moving = k < 1;
            float arc = moving ? Mathf.Sin(k * Mathf.PI) : 0;
            float breathe = 1 + Mathf.Sin(t * 4) * .05f;
            switch (gait)
            {
                case "fly":                                                      // 공중에서 파닥파닥, 갈 땐 앞으로 기울어 미끄러지듯
                    return (.32f + Mathf.Sin(t * 5) * .07f + arc * .08f, 0, moving ? -10 * dir : Mathf.Sin(t * 2.3f) * 4, 1 + Mathf.Sin(t * 14) * .06f, 1);
                case "float":                                                    // 둥실둥실 천천히, 살짝 옆으로 흔들리고 반투명하게 일렁
                    return (.16f + Mathf.Sin(t * 1.7f) * .09f, Mathf.Sin(t * 1.1f) * .07f, Mathf.Sin(t * 1.3f) * 6, 1 + Mathf.Sin(t * 2.2f) * .04f, .82f + Mathf.Sin(t * 2.6f) * .12f);
                case "scuttle":                                                  // 바닥에 붙어 다다닥 — 갈 땐 좌우로 바르르, 서 있을 땐 가끔 움찔
                    return (moving ? Mathf.Abs(Mathf.Sin(k * Mathf.PI * 6)) * .03f : 0, 0, moving ? Mathf.Sin(k * Mathf.PI * 8) * 7 : (Mathf.Sin(t * 1.3f) > .9f ? Mathf.Sin(t * 30) * 4 : 0), moving ? .94f : breathe * .98f, 1);
                case "stomp":                                                    // 쿵쿵: 낮게 들썩, 내려앉을 때 납작, 몸을 좌우로 기울며
                    return (arc * .08f, 0, moving ? Mathf.Sin(k * Mathf.PI * 2) * 6 : Mathf.Sin(t * 1.5f) * 1.5f, moving && k > .8f ? .9f : 1 + Mathf.Sin(t * 2) * .03f, 1);
                case "waddle":                                                   // 뒤뚱뒤뚱 좌우로 크게 기울며 조금씩 통통
                    return (Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2)) * (moving ? .07f : 0), 0, moving ? Mathf.Sin(k * Mathf.PI * 2) * 14 : Mathf.Sin(t * 2.4f) * 4, breathe, 1);
                case "roll":                                                     // 데굴데굴 한 바퀴 굴러간다
                    return (moving ? arc * .04f : 0, 0, moving ? -360 * k * dir : Mathf.Sin(t * 1.8f) * 2, 1, 1);
                case "spin":                                                     // 제자리에서 빙글빙글 반짝, 갈 땐 살짝 뜬다
                    return (.06f + Mathf.Sin(t * 2.5f) * .05f + arc * .12f, 0, Mathf.Sin(t * 1.6f) * 14, 1 + Mathf.Sin(t * 5) * .05f, 1);
                default:                                                         // hop: 통통 튀고, 착지 직전 납작
                    return (arc * .22f, 0, 0, moving ? (k < .15f || k > .85f ? .88f : 1.08f) : breathe, 1);
            }
        }

        /// <summary>등장 진행도 k(0→1) → (크기 배율, 위아래 오프셋). 일반 젤리는 위에서 통 떨어지고, 보스는 땅에서 불쑥 커진다.</summary>
        public static Vector2 Appear(float k, bool boss)
        {
            if (k >= 1) return new Vector2(1, 0);
            k = Mathf.Max(0, k);
            if (boss)
            {
                float c = 1.7f, u = k - 1;                       // 살짝 넘쳤다 돌아오는(Back) 커짐
                return new Vector2(Mathf.Max(.05f, 1 + (c + 1) * u * u * u + c * u * u), 0);
            }
            return new Vector2(1, 4.5f * (1 - DropBounce(k)));
        }

        /// <summary>0→1: 떨어져서 한 번 통 튀고 앉는다.</summary>
        public static float DropBounce(float k)
        {
            if (k < .5f) return k * k * 4;
            float u = (k - .75f) * 4;
            return 1 - .18f * (1 - u * u);
        }

        /// <summary>모션 진행도 p(0→1) → (x, y, 찌그러짐, 회전각). x 는 주인공 쪽(왼쪽)이 음수.</summary>
        public static Vector4 Attack(float p, string name)
        {
            if (p >= 1) return Vector4.zero;
            float s = Mathf.Sin(p * Mathf.PI);
            switch (name)
            {
                case "hop":        // 통, 통 두 번 뛰어서 부딪힘
                    return new Vector4(-s * 2f, Mathf.Abs(Mathf.Sin(p * Mathf.PI * 2)) * 1.1f, p > .45f && p < .6f ? .15f : 0, 0);
                case "spin":       // 빙글빙글 돌며 돌진
                    return new Vector4(-s * 2.2f, s * .3f, 0, p * 720);
                case "slam":       // 높이 뛰었다가 쿵! (착지 순간 납작)
                {
                    float up = p < .6f ? Mathf.Sin(p / .6f * Mathf.PI * .5f) * 2.6f : Mathf.Max(0, 2.6f * (1 - (p - .6f) / .15f));
                    float squash = p > .75f ? .25f * (1 - (p - .75f) / .25f) : p < .15f ? .12f * (p / .15f) : 0;
                    return new Vector4(-s * 1.1f, up, squash, 0);
                }
                case "shard":      // 부르르 떨며 기 모으기 (조각은 ShardVolley 가 날림)
                    return new Vector4(Mathf.Sin(p * 90) * .08f * s, 0, -.08f * s, 0);
                case "charge":     // 뒤로 물러났다가 크게 돌진
                {
                    if (p < .35f) return new Vector4(.7f * (p / .35f), 0, .08f * (p / .35f), 0);
                    float u = (p - .35f) / .65f;
                    return new Vector4(.7f * (1 - u) - 3f * Mathf.Sin(u * Mathf.PI), 0, 0, -10 * Mathf.Sin(u * Mathf.PI));
                }
                case "swoop":      // 하늘 높이 솟았다가 비스듬히 내리꽂기
                {
                    float up = p < .4f ? Mathf.Sin(p / .4f * Mathf.PI * .5f) * 2.2f : 2.2f * (1 - (p - .4f) / .6f) - Mathf.Sin((p - .4f) / .6f * Mathf.PI) * .6f;
                    float fwd = p < .4f ? .5f * (p / .4f) : .5f - 3.2f * Mathf.Sin((p - .4f) / .6f * Mathf.PI);
                    return new Vector4(fwd, up, 0, p < .4f ? 15 * (p / .4f) : -35 * Mathf.Sin((p - .4f) / .6f * Mathf.PI));
                }
                case "phase":      // 스르륵 옆으로 미끄러져 다가와 휙 (작아졌다 커지며 흔들)
                    return new Vector4(-s * 2.4f + Mathf.Sin(p * Mathf.PI * 6) * .15f, Mathf.Sin(p * Mathf.PI * 2) * .4f, -.25f * s, Mathf.Sin(p * Mathf.PI * 4) * 12);
                case "zigzag":     // 바닥에 붙어 지그재그로 다다닥
                    return new Vector4(-s * 2.2f, Mathf.Abs(Mathf.Sin(p * Mathf.PI * 5)) * .25f - .05f, .06f, Mathf.Sin(p * Mathf.PI * 10) * 10);
                default:           // tackle: 몸통 박치기
                    return new Vector4(-s * 1.3f, 0, 0, 0);
            }
        }

        public static float ImpactAt(string name) => name switch { "slam" => .75f, "charge" => .68f, _ => .5f };

        /// <summary>공격 모션 길이(초).</summary>
        public static float Duration(string name) => name switch
        {
            "tackle" => .45f, "hop" => .75f, "spin" => .7f, "slam" => .85f, "shard" => .7f, "phase" => .8f, "swoop" => .7f, "zigzag" => .7f, _ => .8f,
        };

        /// <summary>
        /// 적이 실제로 보여 줄 공격 모습: 데이터의 Move 가 통통(hop)·박치기(tackle)면 걸음새에 맞춰 바꾼다 —
        /// 날짐승은 하늘에서 내리꽂기(swoop), 유령·책은 스르륵(phase), 벌레는 지그재그(zigzag). 모르는 이름은 tackle.
        /// </summary>
        public static string AttackStyle(EnemyDef d)
        {
            string name = System.Array.IndexOf(EnemyDef.Moves, d.Move) >= 0 ? d.Move : "tackle";
            if (name != "hop" && name != "tackle") return name;
            string g = Gait(d);
            return g == "fly" ? "swoop" : g == "float" ? "phase" : g == "scuttle" ? "zigzag" : name;
        }

        /// <summary>공격 소리 (해골은 삐거덕, 새 모습들은 비슷한 소리를 빌려 쓴다).</summary>
        public static string Sound(EnemyDef d, string style) =>
            d.Shape == "skeleton" ? "creak" : style == "swoop" ? "mv_charge" : style == "phase" ? "mv_shard" : style == "zigzag" ? "mv_spin" : "mv_" + style;
    }
}
