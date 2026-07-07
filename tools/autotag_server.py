"""Local garment auto-tagging service (FastAPI).

Classifies a garment image into category / colors / seasons / tags so the wardrobe
upload queue can PREFILL metadata. Suggestions only — the user always overrides, and
the backend/frontend never overwrite fields the user has touched.

All inference is LOCAL. Images never leave the machine (no external API calls).

Models (all MIT / permissive, downloaded once from Hugging Face on first run):
  * Category + tags: FashionCLIP (patrickjohncyh/fashion-clip, MIT) — a CLIP ViT-B/32
    fine-tuned on ~800K fashion products. Base checkpoint
    laion/CLIP-ViT-B-32-laion2B-s34B-b79K (MIT). Used ZERO-SHOT (image/text cosine
    similarity); nothing is trained here.
  * Colors: NO ML — k-means over the garment's non-transparent pixels (or, for an
    opaque photo, pixels that differ from the estimated border background), mapped to
    the nearest named colour.
  * Seasons: a soft category heuristic (low confidence).

Endpoints:
  GET  /health   -> readiness + device/model info.
  POST /classify -> multipart form:
                      file:        the garment image (a processed transparent cutout
                                   is preferred, but a plain photo also works).
                      known_tags:  optional, repeated form field — the user's existing
                                   wardrobe tags. These are preferred when suggesting tags.
                    Returns JSON with per-field values + confidence. Fields below their
                    confidence threshold are omitted so we never push junk.

Run (mirrors tools/rembg_server.py):
  python tools/autotag_server.py --host 127.0.0.1 --port 7100

First run downloads ~600 MB (FashionCLIP). CUDA is used automatically when available,
otherwise CPU. Pin `--revision <commit>` for reproducible model weights.
"""

from __future__ import annotations

import argparse
import io
import json
import logging
import sys
import threading
from typing import Optional

LOGGER = logging.getLogger("autotag")

# --- Category label prompts -------------------------------------------------
# Keys MUST match the backend GarmentCategory enum exactly (Top, Bottom, Dress,
# Outerwear, Shoes, Bag, Accessory). The retired "Hat" category is intentionally
# absent, and hairstyles are presets (never garments), so neither is classified.
# Head-wear prompts are deliberately omitted so a hat lands low-confidence and the
# user picks the category manually, rather than being mislabelled.
CATEGORY_PROMPTS: dict[str, list[str]] = {
    "Top": ["a t-shirt", "a shirt", "a blouse", "a sweater", "a hoodie", "a tank top", "a knit top"],
    "Bottom": ["a pair of trousers", "a pair of jeans", "a skirt", "a pair of shorts", "leggings"],
    "Dress": ["a dress", "a gown", "a jumpsuit"],
    "Outerwear": ["a coat", "a jacket", "a blazer", "a parka", "a puffer jacket", "a trench coat"],
    "Shoes": ["a pair of shoes", "a pair of sneakers", "a pair of boots", "a pair of heels", "a pair of sandals"],
    "Bag": ["a handbag", "a backpack", "a purse", "a tote bag", "a clutch bag"],
    "Accessory": ["a belt", "a scarf", "a pair of sunglasses", "a piece of jewelry", "a watch", "a pair of gloves", "a tie"],
}
CATEGORY_PROMPT_TEMPLATE = "a photo of {label}, a fashion product on a plain background"
# Softmax probability (over the 7 categories) below which no category is suggested.
CATEGORY_MIN_CONFIDENCE = 0.34

# --- Season heuristic -------------------------------------------------------
# Only emitted for categories with a defensible signal; everything else stays empty
# so we never guess a season with no basis. Values are lowercase season tokens.
SEASON_BY_CATEGORY: dict[str, list[str]] = {
    "Outerwear": ["fall", "winter"],
    "Dress": ["spring", "summer"],
}
SEASON_CONFIDENCE = 0.4

# --- Tag lexicon ------------------------------------------------------------
# General fashion descriptors used for zero-shot tagging, UNIONED with the user's
# known tags (which are preferred via a small score bonus + lower threshold).
FASHION_TAG_LEXICON: list[str] = [
    "casual", "formal", "elegant", "sporty", "streetwear", "business", "party",
    "everyday", "vintage", "minimal", "cozy", "athletic", "outdoor", "beach",
    "work", "weekend", "denim", "leather", "floral", "striped", "plaid",
    "printed", "knit", "wool", "cotton", "silk", "summery", "winter",
]
TAG_PROMPT_TEMPLATE = "a photo of {label} clothing"
TAG_MIN_SIMILARITY = 0.19          # absolute cosine floor for a general tag
KNOWN_TAG_SIMILARITY = 0.15        # lower floor for the user's own tags (preferred)
KNOWN_TAG_BONUS = 0.02             # score bonus so ties resolve toward known tags
MAX_TAGS = 5

# --- Colour naming ----------------------------------------------------------
# Curated wardrobe palette: (name, (r, g, b)). Nearest match by a perceptual
# (redmean) RGB distance. The returned hex is the ACTUAL dominant colour; the name
# is the nearest label.
NAMED_COLORS: list[tuple[str, tuple[int, int, int]]] = [
    ("black", (17, 17, 17)),
    ("white", (245, 245, 245)),
    ("gray", (128, 128, 128)),
    ("charcoal", (54, 54, 58)),
    ("silver", (192, 192, 196)),
    ("navy", (31, 42, 68)),
    ("blue", (45, 90, 200)),
    ("light blue", (135, 190, 235)),
    ("teal", (30, 130, 130)),
    ("green", (60, 140, 70)),
    ("olive", (110, 110, 60)),
    ("red", (200, 45, 50)),
    ("burgundy", (110, 30, 45)),
    ("pink", (230, 150, 175)),
    ("purple", (120, 70, 150)),
    ("orange", (225, 130, 50)),
    ("yellow", (225, 205, 70)),
    ("brown", (110, 75, 50)),
    ("beige", (215, 195, 165)),
    ("cream", (240, 232, 210)),
    ("tan", (190, 155, 115)),
    ("khaki", (160, 150, 110)),
    ("gold", (200, 165, 90)),
]
COLOR_MIN_FRACTION = 0.10          # ignore clusters covering < 10% of the garment
MAX_COLORS = 3
MAX_ANALYSIS_SIZE = 256            # downscale longest side before colour k-means
ALPHA_OPAQUE_THRESHOLD = 24        # alpha above this counts as garment
BACKGROUND_DISTANCE_THRESHOLD = 42 # for opaque photos: distance from border colour


# ---------------------------------------------------------------------------
# Model wrapper
# ---------------------------------------------------------------------------
class AutoTagModel:
    """Lazily-loaded FashionCLIP wrapper. Thread-safe inference (guarded by a lock)."""

    def __init__(self, model_name: str, revision: Optional[str], device: str):
        self.model_name = model_name
        self.revision = revision
        self._requested_device = device
        self.device = "cpu"
        self._model = None
        self._processor = None
        self._torch = None
        self._np = None
        self._lock = threading.Lock()
        self._category_text_features = None  # (num_categories, dim), per-category mean
        self._category_labels: list[str] = []

    @property
    def ready(self) -> bool:
        return self._model is not None

    def load(self) -> None:
        import numpy as np
        import torch
        from transformers import CLIPModel, CLIPProcessor

        self._torch = torch
        self._np = np

        if self._requested_device == "auto":
            self.device = "cuda" if torch.cuda.is_available() else "cpu"
        else:
            self.device = self._requested_device

        LOGGER.info("loading %s (revision=%s) on %s", self.model_name, self.revision or "main", self.device)
        self._model = CLIPModel.from_pretrained(self.model_name, revision=self.revision).to(self.device)
        self._model.eval()
        self._processor = CLIPProcessor.from_pretrained(self.model_name, revision=self.revision)

        # Precompute per-category text features (mean of each category's prompts).
        self._category_labels = list(CATEGORY_PROMPTS.keys())
        per_category = []
        for label in self._category_labels:
            prompts = [CATEGORY_PROMPT_TEMPLATE.format(label=p) for p in CATEGORY_PROMPTS[label]]
            feats = self._encode_text(prompts)          # (k, dim), L2-normalized
            mean = feats.mean(axis=0)
            mean = mean / (self._norm(mean) + 1e-8)
            per_category.append(mean)
        self._category_text_features = np.stack(per_category, axis=0)
        LOGGER.info("model ready (%d categories)", len(self._category_labels))

    # -- encoders -----------------------------------------------------------
    def _encode_text(self, prompts: list[str]):
        torch = self._torch
        inputs = self._processor(text=prompts, return_tensors="pt", padding=True, truncation=True)
        inputs = {k: v.to(self.device) for k, v in inputs.items()}
        with torch.no_grad():
            feats = self._model.get_text_features(**inputs)
        feats = feats.cpu().numpy()
        return feats / (self._row_norm(feats) + 1e-8)

    def _encode_image(self, pil_image):
        torch = self._torch
        inputs = self._processor(images=pil_image, return_tensors="pt")
        inputs = {k: v.to(self.device) for k, v in inputs.items()}
        with torch.no_grad():
            feats = self._model.get_image_features(**inputs)
        feats = feats.cpu().numpy()[0]
        return feats / (self._norm(feats) + 1e-8)

    def _norm(self, vec):
        return float(self._np.linalg.norm(vec))

    def _row_norm(self, mat):
        return self._np.linalg.norm(mat, axis=1, keepdims=True)

    # -- classification -----------------------------------------------------
    def classify(self, image_bytes: bytes, known_tags: list[str]) -> dict:
        from PIL import Image

        raw = Image.open(io.BytesIO(image_bytes))
        raw.load()
        rgba = raw.convert("RGBA")

        # Colour analysis works directly on the RGBA (alpha mask preferred).
        colors = extract_colors(rgba, self._np)

        # CLIP wants an opaque RGB; composite any transparency onto white so the
        # background does not bias the embedding.
        clip_image = _composite_on_white(rgba)

        with self._lock:
            image_feat = self._encode_image(clip_image)
            category, category_conf = self._classify_category(image_feat)
            tags = self._classify_tags(image_feat, known_tags)

        seasons = [
            {"value": value, "confidence": SEASON_CONFIDENCE}
            for value in (SEASON_BY_CATEGORY.get(category, []) if category else [])
        ]

        return {
            "provider": "fashionclip",
            "modelVersion": f"{self.model_name}@{self.revision or 'main'}",
            "device": self.device,
            "category": ({"value": category, "confidence": category_conf} if category else None),
            "colors": colors,
            "seasons": seasons,
            "tags": tags,
        }

    def _classify_category(self, image_feat):
        np = self._np
        sims = self._category_text_features @ image_feat        # cosine (both normalized)
        # Softmax with a temperature so probabilities are comparable across items.
        logits = sims / 0.07
        logits = logits - logits.max()
        probs = np.exp(logits)
        probs = probs / probs.sum()
        best = int(probs.argmax())
        confidence = float(probs[best])
        if confidence < CATEGORY_MIN_CONFIDENCE:
            return None, confidence
        return self._category_labels[best], confidence

    def _classify_tags(self, image_feat, known_tags: list[str]):
        np = self._np
        normalized_known = _normalize_tags(known_tags)
        known_set = set(normalized_known)
        candidates = normalized_known + [t for t in FASHION_TAG_LEXICON if t not in known_set]
        if not candidates:
            return []

        prompts = [TAG_PROMPT_TEMPLATE.format(label=t) for t in candidates]
        text_feats = self._encode_text(prompts)                 # (n, dim), normalized
        sims = text_feats @ image_feat                          # cosine per candidate

        scored = []
        for tag, sim in zip(candidates, sims):
            is_known = tag in known_set
            floor = KNOWN_TAG_SIMILARITY if is_known else TAG_MIN_SIMILARITY
            if sim < floor:
                continue
            score = float(sim) + (KNOWN_TAG_BONUS if is_known else 0.0)
            scored.append((tag, float(sim), score))

        scored.sort(key=lambda item: item[2], reverse=True)
        top = scored[:MAX_TAGS]
        # Confidence reported as the raw cosine similarity (clamped to [0, 1]).
        return [{"value": tag, "confidence": max(0.0, min(1.0, sim))} for tag, sim, _ in top]


# ---------------------------------------------------------------------------
# Colour extraction (no ML)
# ---------------------------------------------------------------------------
def extract_colors(rgba_image, np) -> list[dict]:
    from PIL import Image
    from sklearn.cluster import KMeans

    image = rgba_image
    width, height = image.size
    if max(width, height) > MAX_ANALYSIS_SIZE:
        scale = MAX_ANALYSIS_SIZE / max(width, height)
        image = image.resize((max(1, int(width * scale)), max(1, int(height * scale))), Image.BILINEAR)

    data = np.asarray(image, dtype=np.float32)      # (h, w, 4)
    rgb = data[:, :, :3]
    alpha = data[:, :, 3]

    flat_rgb = rgb.reshape(-1, 3)
    flat_alpha = alpha.reshape(-1)

    if float(flat_alpha.min()) < 250.0:
        # A real cutout: keep sufficiently-opaque pixels.
        mask = flat_alpha > ALPHA_OPAQUE_THRESHOLD
    else:
        # Opaque photo: drop pixels close to the estimated border background colour.
        background = _estimate_border_color(rgb, np)
        distance = np.sqrt(((flat_rgb - background) ** 2).sum(axis=1))
        mask = distance > BACKGROUND_DISTANCE_THRESHOLD

    garment = flat_rgb[mask]
    if garment.shape[0] < 24:
        # Not enough garment pixels to be meaningful (e.g. fully transparent input).
        return []

    k = int(min(MAX_COLORS + 1, max(1, garment.shape[0] // 8)))
    kmeans = KMeans(n_clusters=k, n_init=4, random_state=0)
    labels = kmeans.fit_predict(garment)
    centers = kmeans.cluster_centers_

    total = garment.shape[0]
    clusters = []
    for index in range(k):
        fraction = float((labels == index).sum()) / total
        if fraction < COLOR_MIN_FRACTION:
            continue
        center = centers[index]
        clusters.append((fraction, center))

    clusters.sort(key=lambda item: item[0], reverse=True)
    results = []
    for fraction, center in clusters[:MAX_COLORS]:
        r, g, b = (int(max(0, min(255, round(value)))) for value in center)
        results.append({
            "name": _nearest_color_name((r, g, b)),
            "hex": f"#{r:02x}{g:02x}{b:02x}",
            "confidence": round(fraction, 4),
        })
    return results


def _estimate_border_color(rgb, np):
    # Median colour of a thin border frame — a robust background estimate.
    h, w, _ = rgb.shape
    band = max(1, min(h, w) // 20)
    top = rgb[:band, :, :].reshape(-1, 3)
    bottom = rgb[-band:, :, :].reshape(-1, 3)
    left = rgb[:, :band, :].reshape(-1, 3)
    right = rgb[:, -band:, :].reshape(-1, 3)
    border = np.concatenate([top, bottom, left, right], axis=0)
    return np.median(border, axis=0)


def _nearest_color_name(rgb: tuple[int, int, int]) -> str:
    best_name = NAMED_COLORS[0][0]
    best_distance = None
    for name, reference in NAMED_COLORS:
        distance = _redmean_distance(rgb, reference)
        if best_distance is None or distance < best_distance:
            best_distance = distance
            best_name = name
    return best_name


def _redmean_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    # "redmean" perceptual approximation — cheap and better than plain RGB distance.
    rmean = (a[0] + b[0]) / 2.0
    dr = a[0] - b[0]
    dg = a[1] - b[1]
    db = a[2] - b[2]
    return ((2 + rmean / 256.0) * dr * dr) + (4 * dg * dg) + ((2 + (255 - rmean) / 256.0) * db * db)


def _composite_on_white(rgba_image):
    from PIL import Image

    background = Image.new("RGBA", rgba_image.size, (255, 255, 255, 255))
    return Image.alpha_composite(background, rgba_image).convert("RGB")


def _normalize_tags(tags: list[str]) -> list[str]:
    seen = set()
    result = []
    for tag in tags:
        token = (tag or "").strip().lower()
        if token and token not in seen:
            seen.add(token)
            result.append(token)
    return result


# ---------------------------------------------------------------------------
# HTTP app
# ---------------------------------------------------------------------------
def build_app(model: AutoTagModel):
    from fastapi import FastAPI, File, Form, HTTPException, UploadFile
    from fastapi.responses import JSONResponse

    app = FastAPI(title="Outfit Planner auto-tagging", version="1.0")

    @app.get("/health")
    def health() -> JSONResponse:
        return JSONResponse({
            "status": "ok" if model.ready else "loading",
            "ready": model.ready,
            "device": model.device,
            "model": model.model_name,
        })

    @app.post("/classify")
    async def classify(
        file: UploadFile = File(...),
        known_tags: list[str] = Form(default=[]),
    ) -> JSONResponse:
        if not model.ready:
            raise HTTPException(status_code=503, detail="Model is still loading.")

        image_bytes = await file.read()
        if not image_bytes:
            raise HTTPException(status_code=400, detail="Empty image.")

        try:
            result = model.classify(image_bytes, known_tags)
        except Exception as exc:  # pragma: no cover - defensive; never crash a prefill.
            LOGGER.exception("classification failed")
            raise HTTPException(status_code=500, detail=f"Classification failed: {exc}") from exc

        return JSONResponse(result)

    return app


def main() -> None:
    parser = argparse.ArgumentParser(description="Start the local garment auto-tagging service.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7100)
    parser.add_argument("--model", default="patrickjohncyh/fashion-clip")
    parser.add_argument("--revision", default=None, help="Pin a Hugging Face commit for reproducibility.")
    parser.add_argument("--device", default="auto", choices=["auto", "cuda", "cpu"])
    parser.add_argument("--log-level", default="info")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )

    try:
        import torch

        print(f"torch: {torch.__version__}", flush=True)
        print(f"cuda available: {torch.cuda.is_available()}", flush=True)
        if torch.cuda.is_available():
            print(f"cuda device: {torch.cuda.get_device_name(0)}", flush=True)
    except Exception as exc:  # pragma: no cover - diagnostic only.
        print(f"torch diagnostics failed: {exc}", file=sys.stderr, flush=True)

    model = AutoTagModel(args.model, args.revision, args.device)
    try:
        model.load()
    except Exception as exc:  # pragma: no cover - surfaced via /health = loading.
        LOGGER.error("model failed to load: %s", exc)

    import uvicorn

    uvicorn.run(build_app(model), host=args.host, port=args.port, log_level=args.log_level)


if __name__ == "__main__":
    main()
