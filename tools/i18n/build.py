"""Regenerates SharedUI/Resources/{ar,en}.json from strings.py (sorted, UTF-8, no ASCII escaping)."""
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from strings import S  # noqa: E402

root = pathlib.Path(__file__).resolve().parents[2]
out = root / "src" / "DentalLabOS.SharedUI" / "Resources"
for index, lang in enumerate(("ar", "en")):
    data = {k: v[index] for k, v in sorted(S.items())}
    (out / f"{lang}.json").write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"wrote {len(S)} keys")
