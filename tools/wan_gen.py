"""로컬 Wan 2.2 (5B, 그림 → 영상)로 짧은 애니메이션 만들기 (ComfyUI 서버 API, tools/qwen_gen.py 와 같은 서버).

  python tools/wan_gen.py 첫장면.png "움직임 설명" 출력폴더 [--w 640 --h 832 --length 49 --keep 30 --seed 7 --steps 20] [--big]
  --big = Wan 2.2 I2V 14B (GGUF Q4_K_M + lightx2v 4스텝, 16fps) — 5B 보다 얼굴·눈이 안정적

첫 장면 그림에서 시작하는 영상(length 프레임, 24fps)을 만들고, 그중 keep 장을 고르게 뽑아
출력폴더/0001.png … 로 저장한다 (게임 컷인: tools/cutins_video.py 가 assets/cutins/<이름>_<번호>.png 로).
"""
import argparse, json, os, random, shutil, sys, time, urllib.request, uuid

SERVER = "http://127.0.0.1:8188"
COMFY = r"C:\ComfyUI\ComfyUI_windows_portable\ComfyUI"
NEG = ("blurry, low quality, distorted face, extra limbs, extra fingers, deformed hands, flickering, morphing face, "
       "text, watermark, static image, still frame, scene cut, camera cut, transition, cross fade, whiteout, overexposed, "
       "character leaves the frame, cropped face")


def call(path, data=None):
    req = urllib.request.Request(SERVER + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        return r.read()


def build(image, prompt, w, h, length, seed, steps):
    return {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": "wan2.2_ti2v_5B_fp16.safetensors", "weight_dtype": "default"}},
        "shift": {"class_type": "ModelSamplingSD3", "inputs": {"model": ["unet", 0], "shift": 8.0}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": "umt5_xxl_fp8_e4m3fn_scaled.safetensors", "type": "wan", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": "wan2.2_vae.safetensors"}},
        "img": {"class_type": "LoadImage", "inputs": {"image": image}},
        "pos": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": prompt}},
        "neg": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": NEG}},
        "lat": {"class_type": "Wan22ImageToVideoLatent", "inputs": {"vae": ["vae", 0], "width": w, "height": h, "length": length,
                                                                     "batch_size": 1, "start_image": ["img", 0]}},
        "ks": {"class_type": "KSampler", "inputs": {"model": ["shift", 0], "positive": ["pos", 0], "negative": ["neg", 0], "latent_image": ["lat", 0],
                                                    "seed": seed, "steps": steps, "cfg": 5.0, "sampler_name": "uni_pc", "scheduler": "simple", "denoise": 1.0}},
        "dec": {"class_type": "VAEDecode", "inputs": {"samples": ["ks", 0], "vae": ["vae", 0]}},
        "save": {"class_type": "SaveImage", "inputs": {"images": ["dec", 0], "filename_prefix": "game/wan"}},
    }


def build14(image, prompt, w, h, length, seed, steps=4):
    """Wan 2.2 I2V A14B (GGUF Q4_K_M, 사용자 2026-10-05 "14B 를 양자화해서"): 고노이즈 → 저노이즈 두 모델을 반씩, lightx2v 4스텝 LoRA, CFG 1."""
    half = steps // 2
    g = {
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": "umt5_xxl_fp8_e4m3fn_scaled.safetensors", "type": "wan", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": "wan_2.1_vae.safetensors"}},
        "img": {"class_type": "LoadImage", "inputs": {"image": image}},
        "pos": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": prompt}},
        "neg": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": NEG}},
        "i2v": {"class_type": "WanImageToVideo", "inputs": {"positive": ["pos", 0], "negative": ["neg", 0], "vae": ["vae", 0], "start_image": ["img", 0],
                                                         "width": w, "height": h, "length": length, "batch_size": 1}},
        "dec": {"class_type": "VAEDecode", "inputs": {"samples": ["lo_ks", 0], "vae": ["vae", 0]}},
        "save": {"class_type": "SaveImage", "inputs": {"images": ["dec", 0], "filename_prefix": "game/wan14"}},
    }
    for k, name in (("hi", "HighNoise"), ("lo", "LowNoise")):
        g[k + "_unet"] = {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": f"Wan2.2-I2V-A14B-{name}-Q4_K_M.gguf"}}
        g[k + "_lora"] = {"class_type": "LoraLoaderModelOnly", "inputs": {"model": [k + "_unet", 0], "strength_model": 1.0,
                          "lora_name": f"wan2.2_i2v_lightx2v_4steps_lora_v1_{'high' if k == 'hi' else 'low'}_noise.safetensors"}}
        g[k + "_shift"] = {"class_type": "ModelSamplingSD3", "inputs": {"model": [k + "_lora", 0], "shift": 5.0}}
    g["hi_ks"] = {"class_type": "KSamplerAdvanced", "inputs": {"model": ["hi_shift", 0], "add_noise": "enable", "noise_seed": seed, "steps": steps, "cfg": 1.0,
                  "sampler_name": "euler", "scheduler": "simple", "positive": ["i2v", 0], "negative": ["i2v", 1], "latent_image": ["i2v", 2],
                  "start_at_step": 0, "end_at_step": half, "return_with_leftover_noise": "enable"}}
    g["lo_ks"] = {"class_type": "KSamplerAdvanced", "inputs": {"model": ["lo_shift", 0], "add_noise": "disable", "noise_seed": seed, "steps": steps, "cfg": 1.0,
                  "sampler_name": "euler", "scheduler": "simple", "positive": ["i2v", 0], "negative": ["i2v", 1], "latent_image": ["hi_ks", 0],
                  "start_at_step": half, "end_at_step": 10000, "return_with_leftover_noise": "disable"}}
    return g


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("image")
    ap.add_argument("prompt")
    ap.add_argument("out")
    ap.add_argument("--w", type=int, default=640)
    ap.add_argument("--h", type=int, default=832)
    ap.add_argument("--length", type=int, default=49)
    ap.add_argument("--keep", type=int, default=30)
    ap.add_argument("--seed", type=int, default=None)
    ap.add_argument("--steps", type=int, default=None)
    ap.add_argument("--big", action="store_true", help="Wan 2.2 14B (GGUF) — 얼굴·눈이 더 안정적")
    a = ap.parse_args()

    name = "game_" + uuid.uuid4().hex[:8] + os.path.splitext(a.image)[1]
    shutil.copy(a.image, os.path.join(COMFY, "input", name))
    seed = a.seed if a.seed is not None else random.randrange(2 ** 31)
    graph = build14(name, a.prompt, a.w, a.h, a.length, seed, a.steps or 4) if a.big else build(name, a.prompt, a.w, a.h, a.length, seed, a.steps or 20)
    pid = json.loads(call("/prompt", {"prompt": graph}))["prompt_id"]
    t0 = time.time()
    while True:
        time.sleep(5)
        h = json.loads(call("/history/" + pid))
        if pid in h:
            break
        if time.time() - t0 > 3600:
            sys.exit("시간 초과")
    st = h[pid].get("status", {})
    if st.get("status_str") != "success":
        sys.exit("실패: " + json.dumps(st, ensure_ascii=False)[:3000])
    imgs = [i for o in h[pid]["outputs"].values() for i in o.get("images", [])]
    os.makedirs(a.out, exist_ok=True)
    for old in os.listdir(a.out):
        if old.endswith(".png"):
            os.remove(os.path.join(a.out, old))
    n = len(imgs)
    keep = min(a.keep, n)
    pick = sorted({round(k * (n - 1) / max(1, keep - 1)) for k in range(keep)})      # 첫 장·끝 장 포함, 고르게
    for k, idx in enumerate(pick, 1):
        im = imgs[idx]
        shutil.copy(os.path.join(COMFY, "output", im["subfolder"], im["filename"]), os.path.join(a.out, f"{k:04d}.png"))
    for im in imgs:
        os.remove(os.path.join(COMFY, "output", im["subfolder"], im["filename"]))
    os.remove(os.path.join(COMFY, "input", name))
    print(f"ok {a.out} frames {n} -> {len(pick)} seed {seed} {time.time() - t0:.0f}s")


if __name__ == "__main__":
    main()
