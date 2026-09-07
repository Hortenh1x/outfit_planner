# Multi-arch rembg server image. danielgatis/rembg:latest publishes no linux/arm64
# manifest, so ARM hosts (Oracle Ampere) build this instead: python + rembg[cli]
# (onnxruntime ships aarch64 wheels). The compose service supplies the `s ...` command.
FROM python:3.12-slim
# [cpu] pulls onnxruntime (aarch64 wheels exist); [cli] pulls the server deps.
RUN pip install --no-cache-dir "rembg[cpu,cli]"
EXPOSE 7000
ENTRYPOINT ["rembg"]
CMD ["s", "--host", "0.0.0.0", "--port", "7000", "--no-ui"]
