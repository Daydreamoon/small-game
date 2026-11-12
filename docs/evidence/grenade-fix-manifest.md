# 自动手雷修复证据清单（2026-11-12）

最终 APK：`E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk`
SHA-256：`1203BE1C0C2C01E9CA8624577AE4EA293324B3443DE2FBA43E8F3A327D251CF5`
大小：20,041,346 B

## 自动手雷专项

| 文件 | 内容 |
|---|---|
| `grenade-normal.png` | 普通模式空闲，无自动投掷 |
| `grenade-drag.png` | 从手雷按钮起划被取消 |
| `grenade-accepted.png` | 明确按住 300ms 后正常投掷、地形炸坑、冷却 |
| `grenade-jump.png` | 跳跃后未触发手雷 |
| `grenade-aimdrag.png` | 瞄准拖拽后未触发手雷 |

日志摘录：

```text
[BREAKLINE] Grenade tap cancelled: finger moved like a drag.
[BREAKLINE] Grenade tap accepted id=0.
```

## 回归性能

- `grenade-p4-run1.csv` / `.png`
- `grenade-p4-run2.csv` / `.png`
- `grenade-p4-run3.csv` / `.png`
- `grenade-p5-run1.csv` / `.png`
- `grenade-crush.png`
- 构建日志：`build-release-grenadefix2.log`

## P6（上一构建 6C9A）

- `p6-final-6C9A.csv`（56 行）
- `p6-final-6C9A-files/`（56 个 raw + `P6-SUMMARY.json`）
- `p6-final-6C9A.png`、`p6-final-6C9A.log`、`p6-final-6C9A-done.txt`

P6 未在 `1203BE...` 上重跑；本修复只改输入判定，P4 三轮用于确认无渲染回归。
