"""로컬 Qwen-Image 2.1 + Viggle Turbo(6스텝)로 그림 한 장 그리기 (ComfyUI 서버 API).

화면이 꺼져 있어도(Gemini 클립보드 불가) 그릴 수 있게 하려는 도구.
먼저 C:\\ComfyUI\\ComfyUI_windows_portable 의 ComfyUI 를 127.0.0.1:8188 에 띄워 둔다.

  python tools/qwen_gen.py "프롬프트" -o out.png [--ref a.png --ref b.png] [--w 1024 --h 1024] [--seed 7]

--ref 를 주면 편집(참고 그림) 모드: 결과 크기는 첫 참고 그림 비율을 따른다.
"""
import argparse, json, os, random, shutil, sys, time, urllib.request, uuid

SERVER = "http://127.0.0.1:8188"
COMFY = r"C:\ComfyUI\ComfyUI_windows_portable\ComfyUI"


def call(path, data=None):
    req = urllib.request.Request(SERVER + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        return r.read()


def build(prompt, refs, w, h, seed):
    g = {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": "qwen_image_2.1_int8_convrot.safetensors", "weight_dtype": "default"}},
        "lora": {"class_type": "ViggleTurboLora", "inputs": {"model": ["unet", 0], "strength": 1.0,
                 "lora_name": "Qwen-Image-2.1-viggle-turbo-v0.3-6step-lora-r128.safetensors"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen3vl_8b_int8_convrot.safetensors", "type": "qwen_image", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": "qwen_image_2.1_vae_bf16.safetensors"}},
        "enc": {"class_type": "TextEncodeQwenImage21", "inputs": {"clip": ["clip", 0], "vae": ["vae", 0], "prompt": prompt,
                "negative_prompt": "", "resolution": 1024}},
        "guider": {"class_type": "BasicGuider", "inputs": {"model": ["lora", 0], "conditioning": ["enc", 0]}},
        "noise": {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}},
        "sampler": {"class_type": "KSamplerSelect", "inputs": {"sampler_name": "euler"}},
        "run": {"class_type": "SamplerCustomAdvanced", "inputs": {"noise": ["noise", 0], "guider": ["guider", 0],
                "sampler": ["sampler", 0], "sigmas": ["sigmas", 0], "latent_image": None}},
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["run", 0], "vae": ["vae", 0]}},
        "save": {"class_type": "SaveImage", "inputs": {"images": ["decode", 0], "filename_prefix": "game/qwen"}},
    }
    if refs:
        g["cache"] = {"class_type": "QwenImage21Cache", "inputs": {"model": ["lora", 0], "device": "auto", "dtype": "default"}}
        g["guider"]["inputs"]["model"] = ["cache", 0]
        for i, name in enumerate(refs, 1):
            g[f"ref{i}"] = {"class_type": "LoadImage", "inputs": {"image": name}}
            g["enc"]["inputs"][f"images.image_{i}"] = [f"ref{i}", 0]
        latent = ["enc", 2]
    else:
        g["empty"] = {"class_type": "EmptyLatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}}
        latent = ["empty", 0]
    g["sigmas"] = {"class_type": "ViggleTurboSigmas", "inputs": {"latent": latent, "nodes": "1.0, 0.9375, 0.875, 0.75, 0.5, 0.25"}}
    g["run"]["inputs"]["latent_image"] = latent
    return g


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prompt")
    ap.add_argument("-o", "--out", required=True)
    ap.add_argument("--ref", action="append", default=[])
    ap.add_argument("--w", type=int, default=1024)
    ap.add_argument("--h", type=int, default=1024)
    ap.add_argument("--seed", type=int, default=None)
    a = ap.parse_args()

    names = []
    for p in a.ref:     # 참고 그림은 ComfyUI input 폴더로 복사
        n = "game_" + uuid.uuid4().hex[:8] + os.path.splitext(p)[1]
        shutil.copy(p, os.path.join(COMFY, "input", n))
        names.append(n)
    seed = a.seed if a.seed is not None else random.randrange(2 ** 31)
    pid = json.loads(call("/prompt", {"prompt": build(a.prompt, names, a.w, a.h, seed)}))["prompt_id"]
    t0 = time.time()
    while True:
        time.sleep(2)
        h = json.loads(call("/history/" + pid))
        if pid in h:
            break
        if time.time() - t0 > 1800:
            sys.exit("시간 초과")
    st = h[pid].get("status", {})
    if st.get("status_str") != "success":
        sys.exit("실패: " + json.dumps(st, ensure_ascii=False)[:2000])
    img = [i for o in h[pid]["outputs"].values() for i in o.get("images", [])][0]
    src = os.path.join(COMFY, "output", img["subfolder"], img["filename"])
    shutil.move(src, a.out)
    for n in names:
        os.remove(os.path.join(COMFY, "input", n))
    print(f"ok {a.out} seed {seed} {time.time() - t0:.0f}s")


if __name__ == "__main__":
    main()
