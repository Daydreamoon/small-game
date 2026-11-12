#!/usr/bin/env python3
"""Aggregate the 56-window P6 run without third-party libraries.

Reads the in-game M1-Performance.csv plus optional M1-RawFrames-*.csv files.
The overall average is frame-weighted; p1/p0.1 are nearest-rank over raw frames
when raw files are present, otherwise the worst per-window values are reported
as a conservative fallback.
"""
import csv
import json
import math
import os
import sys
from pathlib import Path


def percentile(sorted_vals, p):
    if not sorted_vals:
        return None
    # nearest-rank, matching the runtime's practical reporting
    k = max(0, min(len(sorted_vals) - 1, math.ceil(p / 100.0 * len(sorted_vals)) - 1))
    return sorted_vals[k]


def main():
    root = Path(sys.argv[1] if len(sys.argv) > 1 else r"E:\小游戏开发\docs\evidence\iqoo-p6-files")
    csv_path = root / "M1-Performance.csv"
    if not csv_path.exists():
        print(f"missing {csv_path}", file=sys.stderr)
        return 2

    with csv_path.open(newline="", encoding="utf-8-sig") as f:
        rows = [r for r in csv.DictReader(f) if r.get("scenario")]

    p6_rows = [r for r in rows if str(r.get("scenario", "")).startswith("stress-")]
    if not p6_rows:
        print("no stress rows", file=sys.stderr)
        return 3

    frames = sum(int(float(r["frames"])) for r in p6_rows)
    elapsed = sum(float(r["elapsed_s"]) for r in p6_rows)
    weighted_fps = sum(float(r["fps_avg"]) * int(float(r["frames"])) for r in p6_rows) / frames
    max_frame = max(float(r["max_frame_ms"]) for r in p6_rows)
    over33 = sum(1 for r in p6_rows if float(r["max_frame_ms"]) > 33.0)
    worst_p1 = min(float(r["fps_1pct_low"]) for r in p6_rows)
    worst_p01 = min(float(r["fps_0_1pct_low"]) for r in p6_rows)

    raw_files = sorted(root.glob("M1-RawFrames-*.csv"))
    all_ms = []
    for raw in raw_files:
        with raw.open(newline="", encoding="utf-8-sig") as f:
            for r in csv.DictReader(f):
                try:
                    all_ms.append(float(r["frame_ms"]))
                except (KeyError, ValueError):
                    pass
    all_ms.sort()
    if all_ms:
        p1 = percentile(all_ms, 1)
        p01 = percentile(all_ms, 0.1)
        # FPS for a slow frame: 1000/ms; p1 FPS is based on the 1% slow tail.
        p1_ms = all_ms[max(0, math.ceil(0.01 * len(all_ms)) - 1)]
        p01_ms = all_ms[max(0, math.ceil(0.001 * len(all_ms)) - 1)]
        p1_fps = 1000.0 / p1_ms
        p01_fps = 1000.0 / p01_ms
        frames_over33 = sum(1 for x in all_ms if x > 33.0)
    else:
        p1_fps = worst_p1
        p01_fps = worst_p01
        p1_ms = None
        p01_ms = None
        frames_over33 = None

    summary = {
        "source": str(csv_path),
        "rows": len(p6_rows),
        "raw_files": len(raw_files),
        "frames_from_rows": frames,
        "frames_from_raw": len(all_ms),
        "elapsed_s": round(elapsed, 3),
        "fps_avg_weighted": round(weighted_fps, 3),
        "fps_1pct_low": round(p1_fps, 3) if p1_fps is not None else None,
        "fps_0_1pct_low": round(p01_fps, 3) if p01_fps is not None else None,
        "p1_frame_ms": round(p1_ms, 3) if p1_ms is not None else None,
        "p0_1_frame_ms": round(p01_ms, 3) if p01_ms is not None else None,
        "max_frame_ms": round(max_frame, 3),
        "windows_max_frame_over_33ms": over33,
        "raw_frames_over_33ms": frames_over33,
        "worst_window_1pct_low": worst_p1,
        "worst_window_0_1pct_low": worst_p01,
        "first_timestamp": p6_rows[0].get("timestamp_utc"),
        "last_timestamp": p6_rows[-1].get("timestamp_utc"),
        "build": p6_rows[0].get("build"),
        "device": p6_rows[0].get("device_model"),
        "notes": [r.get("notes", "") for r in p6_rows[-3:]],
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    out = root / "P6-SUMMARY.json"
    out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
