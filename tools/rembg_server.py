"""Start rembg's HTTP server with ONNX Runtime diagnostics.

This wrapper is useful on Windows when CUDA/cuDNN DLLs are installed through
pip packages. It preloads ONNX Runtime DLLs before importing rembg's server
command, then prints the providers used by the exact Python process that will
run inference.
"""

from __future__ import annotations

import argparse
import io
import sys
import threading
import time
import urllib.error
import urllib.request


def main() -> None:
    parser = argparse.ArgumentParser(description="Start a local rembg HTTP server.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", default="7000")
    parser.add_argument("--log-level", default="info")
    parser.add_argument("--model", default="birefnet-general-lite")
    parser.add_argument("--no-prewarm", action="store_true", help="Do not prewarm the rembg model.")
    parser.add_argument("--ui", action="store_true", help="Enable rembg's Gradio UI.")
    args, passthrough = parser.parse_known_args()

    try:
        import onnxruntime as ort

        if hasattr(ort, "preload_dlls"):
            ort.preload_dlls(directory="")

        print(f"onnxruntime: {ort.__file__}", flush=True)
        print(f"providers: {ort.get_available_providers()}", flush=True)
        print(f"device: {ort.get_device()}", flush=True)
    except Exception as exc:  # pragma: no cover - local diagnostic path.
        print(f"onnxruntime diagnostics failed: {exc}", file=sys.stderr, flush=True)

    from rembg.cli import main as rembg_main

    sys.argv = [
        "rembg",
        "s",
        "--host",
        args.host,
        "--port",
        str(args.port),
        "--log_level",
        args.log_level,
    ]
    if not args.ui:
        sys.argv.append("--no-ui")
    sys.argv.extend(passthrough)

    if not args.no_prewarm:
        threading.Thread(target=prewarm_server, args=(args.host, args.port, args.model), daemon=True).start()

    rembg_main()


def prewarm_server(host: str, port: str, model: str) -> None:
    url = f"http://{host}:{port}/api/remove"
    for _ in range(120):
        try:
            request = urllib.request.Request(
                url,
                data=multipart_prewarm_body(model),
                headers={"Content-Type": "multipart/form-data; boundary=outfitplannerprewarm"},
                method="POST",
            )
            started = time.perf_counter()
            with urllib.request.urlopen(request, timeout=300) as response:
                response.read(1)
            elapsed = time.perf_counter() - started
            print(f"prewarmed rembg model '{model}' in {elapsed:.1f}s", flush=True)
            return
        except (urllib.error.URLError, TimeoutError, ConnectionError):
            time.sleep(1)
        except Exception as exc:  # pragma: no cover - local diagnostic path.
            print(f"rembg prewarm failed: {exc}", file=sys.stderr, flush=True)
            return

    print("rembg prewarm skipped: server did not become ready", file=sys.stderr, flush=True)


def multipart_prewarm_body(model: str) -> bytes:
    boundary = "outfitplannerprewarm"
    image = prewarm_png()
    parts = [
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="model"\r\n\r\n'
        f"{model}\r\n".encode("utf-8"),
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="file"; filename="prewarm.png"\r\n'
        "Content-Type: image/png\r\n\r\n".encode("utf-8"),
        image,
        f"\r\n--{boundary}--\r\n".encode("utf-8"),
    ]
    return b"".join(parts)


def prewarm_png() -> bytes:
    from PIL import Image, ImageDraw

    image = Image.new("RGB", (128, 128), "white")
    draw = ImageDraw.Draw(image)
    draw.rectangle((44, 24, 84, 104), fill=(190, 20, 30))
    output = io.BytesIO()
    image.save(output, format="PNG")
    return output.getvalue()


if __name__ == "__main__":
    main()
