"""
gif_to_sheet.py — convertit un GIF anime en sprite sheet PNG horizontal.

Pattern de nommage : <base>_<N>frame.gif -> <base>_<N>frame.png (sheet N cellules).
Si le GIF contient plus de N frames (cas frequent avec frame de cloture GIF),
on ne prend que les N premieres.

Usage :
    python gif_to_sheet.py <input.gif> [<input2.gif> ...] --out <output_dir>

Exemple :
    python tools/gif_to_sheet.py \
        "C:/Users/Lorenzo/Downloads/soulrender-part2/Marques/marque_de_carnage_4frame.gif" \
        --out "Assets/_Nymora/Art/Sprites/Soulrender/Marks"

Dependances : Pillow (deja installe sur le poste de Lorenzo).
Reutilisable pour tout futur drop d'animations du designer.
"""
import argparse
import os
import re
import sys

from PIL import Image


FRAME_PATTERN = re.compile(r"_(\d+)frame\.gif$", re.IGNORECASE)


def parse_frame_count(filename: str) -> int:
    m = FRAME_PATTERN.search(filename)
    if not m:
        raise ValueError(
            f"Nom de fichier ne suit pas le pattern *_Nframe.gif : {filename}"
        )
    return int(m.group(1))


def extract_to_sheet(gif_path: str, out_dir: str) -> str:
    base = os.path.basename(gif_path)
    n_expected = parse_frame_count(base)

    im = Image.open(gif_path)
    n_total = getattr(im, "n_frames", 1)

    if n_total < n_expected:
        raise ValueError(
            f"{base} : filename annonce {n_expected} frames mais le GIF n'en contient que {n_total}"
        )

    if n_total > n_expected:
        print(
            f"  [info] {base} : GIF contient {n_total} frames, on prend les {n_expected} premieres"
        )

    # Convertit chaque frame en RGBA (les GIFs sont souvent en palette indexed 'P').
    frames = []
    for i in range(n_expected):
        im.seek(i)
        frame = im.convert("RGBA")
        frames.append(frame)

    w, h = frames[0].size
    sheet = Image.new("RGBA", (w * n_expected, h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * w, 0), f)

    out_name = base.replace(".gif", ".png").replace(".GIF", ".png")
    out_path = os.path.join(out_dir, out_name)
    os.makedirs(out_dir, exist_ok=True)
    sheet.save(out_path, "PNG")
    print(f"  -> {out_path} ({n_expected} frames, sheet {sheet.size})")
    return out_path


def main(argv) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="+", help="Fichiers GIF a convertir")
    parser.add_argument("--out", required=True, help="Dossier de sortie")
    args = parser.parse_args(argv)

    for path in args.inputs:
        if not os.path.isfile(path):
            print(f"[skip] introuvable : {path}", file=sys.stderr)
            continue
        try:
            extract_to_sheet(path, args.out)
        except Exception as exc:
            print(f"[fail] {path} : {exc}", file=sys.stderr)
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
