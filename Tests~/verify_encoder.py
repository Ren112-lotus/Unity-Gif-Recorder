"""Independent GIF decoding checks. Run after Tests~/EncoderSmoke.

Requires Pillow only for verification; the Unity extension has no Python dependency.
"""
import json
import sys
from pathlib import Path
from PIL import Image

directory = Path(sys.argv[1] if len(sys.argv) > 1 else "TestResults").resolve()
checks = []


def check(name, passed):
    checks.append({"name": name, "passed": bool(passed)})
    if not passed:
        raise AssertionError(name)


with Image.open(directory / "exact.gif") as gif:
    check("GIF89a dimensions, frame count and infinite loop", gif.size == (257, 129) and gif.n_frames == 3 and gif.info["loop"] == 0)
    for index, duration in enumerate((70, 110, 190)):
        gif.seek(index)
        expected = Image.frombytes("RGB", gif.size, (directory / f"expected-{index}.rgb").read_bytes()).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        check(f"LZW size growth/reset, exact colors and row orientation: frame {index}", gif.convert("RGB").tobytes() == expected.tobytes())
        check(f"frame {index} duration", gif.info["duration"] == duration)

with Image.open(directory / "single.gif") as gif:
    check("single pixel and no loop extension", gif.size == (1, 1) and gif.n_frames == 1 and "loop" not in gif.info)
    check("single pixel color and delay", gif.convert("RGB").getpixel((0, 0)) == (31, 137, 241) and gif.info["duration"] == 120)

with Image.open(directory / "gradient.gif") as gif:
    actual = gif.convert("RGB").tobytes()
    expected = Image.frombytes("RGB", gif.size, (directory / "gradient.rgb").read_bytes()).transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
    mean_squared_error = sum((a - b) ** 2 for a, b in zip(actual, expected)) / len(actual)
    check("adaptive palette preserves gradient within color quantization error", mean_squared_error < 100)

print(json.dumps({"checks": checks, "gradient_mse": round(mean_squared_error, 3)}, indent=2))
