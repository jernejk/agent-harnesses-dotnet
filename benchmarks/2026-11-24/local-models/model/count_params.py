"""Counts inkling-2-mini's parameters from Ollama's local copy and prints model/inkling-2-mini.json.

Usage: python3 -I benchmarks/2026-11-24/local-models/model/count_params.py > benchmarks/2026-11-24/local-models/model/inkling-2-mini.json
Needs `ollama pull inkling-2-mini` (Ollama 0.40.17) in ~/.ollama.

Every tensor blob in the model's manifest is a small safetensors file whose header gives the dtype
and shape. The experts are packed: a U32 holds eight 4-bit or four 8-bit weights. Their logical
size comes from the config (the gate and up projections read hidden_size inputs, the down
projection reads intermediate_size), so the packing factor is fan_in / packed width. Quantization
metadata (`*_weight_scale`, `*_amax`, `*_outer_scale_denominator`) is not counted as parameters.
"""
import collections
import json
import struct
from pathlib import Path

base = Path.home() / ".ollama/models"
man = json.loads((base / "manifests/registry.ollama.ai/library/inkling-2-mini/latest").read_text())
blob = lambda digest: base / "blobs" / digest.replace(":", "-")

cfg = None
for layer in man["layers"]:
    if layer["mediaType"] == "application/vnd.ollama.image.json":
        try:
            c = json.loads(blob(layer["digest"]).read_text())
        except (ValueError, UnicodeDecodeError):
            continue
        if "Inkling2LoopForConditionalGeneration" in json.dumps(c.get("architectures", "")):
            cfg = c
tc = cfg.get("text_config", cfg)
H, I, E, K = tc["hidden_size"], tc["intermediate_size"], tc["n_routed_experts"], tc["num_experts_per_tok"]

params, bits = collections.Counter(), collections.Counter()
meta_elems = disk = tensors = 0
for layer in man["layers"]:
    if layer["mediaType"] != "application/vnd.ollama.image.tensor":
        continue
    disk += layer["size"]
    with open(blob(layer["digest"]), "rb") as f:
        header = json.loads(f.read(struct.unpack("<Q", f.read(8))[0]))
    for name, meta in header.items():
        if name == "__metadata__":
            continue
        tensors += 1
        numel = 1
        for d in meta["shape"]:
            numel *= d
        if name.endswith(("_weight_scale", "_amax", "_outer_scale_denominator")):
            meta_elems += numel
            continue
        if meta["dtype"] == "U32" and ".experts." in name:
            experts, out, packed = meta["shape"]
            fan_in = H if name.endswith("w13_weight") else I
            numel = experts * out * fan_in
            bits["4-bit" if fan_in // packed == 8 else "8-bit"] += 1
        part = ("experts" if ".experts." in name else
                "embed+unembed" if name in ("model.llm.embed.weight", "model.llm.unembed.weight") else
                "text, other" if name.startswith("model.llm.") else
                "vision" if name.startswith("model.visual.") else
                "draft (MTP)" if name.startswith("draft.") else "other")
        params[part] += numel

total = sum(params.values())
print(json.dumps({
    "source": "Ollama 0.40.17 model inkling-2-mini:latest; counted from the safetensors header of every tensor blob in its manifest",
    "config": {k: tc[k] for k in ("num_hidden_layers", "hidden_size", "intermediate_size", "n_routed_experts",
                                  "num_experts_per_tok", "n_shared_experts", "num_attention_heads", "num_key_value_heads",
                                  "head_dim", "vocab_size", "sliding_window_size", "n_layers_per_attn_res_block") if k in tc}
              | {"architectures": cfg.get("architectures")},
    "tensors": tensors, "tensor_bytes_on_disk": disk, "tensor_GiB_on_disk": round(disk / 2**30, 2),
    "expert_weight_tensors_by_precision": dict(bits),  # two per layer: w13 and w2
    "parameters": {k: params[k] for k in sorted(params)},
    "parameters_total": total,
    "text_model_total": params["experts"] + params["embed+unembed"] + params["text, other"],
    "active_per_token_text": params["embed+unembed"] + params["text, other"] + params["experts"] * K // E,
    "quantization_metadata_elements_excluded": meta_elems,
}, indent=1))
