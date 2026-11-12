# iQOO Neo10 真机测试证据清单

日期：2026-11-12（Asia/Shanghai）
设备：vivo iQOO Neo10，型号 V2425A / 产品 PD2425，serial `10AEBB32GF000F9`
系统：Android 16 / SDK 36；SoC SM8650；GPU Adreno 750；RAM 15197 MB
屏幕：2800×1260；Unity 上报 60 Hz；面板支持 90/120/144 Hz
构建：Release 0.1.0，Unity 2021.3.45f2，IL2CPP/ARM64/GLES3
APK：`E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk`
SHA-256：`F2C1CF614C4D8C478B89653493EBA913E9C06B0035E9BECD8C63D8E5FDF17369`
构建：2026-11-12 12:53:28，含 GameRoot 截图修复与 InputRouter owner 修复

| 文件/目录 | 用途 |
|---|---|
| `iqoo-device-info.txt` | 设备、测试条件、设备等级提醒 |
| `iqoo-smoke.png` | 首次真机启动、世界正向、HUD |
| `iqoo-dbg.png` | DBG 调试面板 |
| `iqoo-p4-manual.png` | 旧自动压测截图（含测量污染，仅作历史） |
| `iqoo-fire.png` | FIRE 击杀木靶（VIS 27、血迹） |
| `iqoo-jump2.png` | 跳跃/按钮冒烟（未作为严格跳跃验收） |
| `iqoo-nade.png` | NADE 爆炸、冷却 3.4 s |
| `iqoo-crush6.png` | 自动压敌 PASS 后 `GATE OPEN — PROCEED RIGHT` |
| `iqoo-crush6.log` | crush PASS 日志摘录与截图路径错误 |
| `iqoo-M1-Performance-P4P5.csv` | 3 轮 P4 + 3 轮 P5 摘要（6 行） |
| `iqoo-raw/` | 6 个逐帧 CSV，每个 6555 B |
| `iqoo-p4-run1..3/screen.png` | P4 三轮截图 |
| `iqoo-p5-run1..3/screen.png` | P5 三轮截图 |

## 结果摘录

P4（200 可见 + 12 物理 + 3 链爆，Release，604 帧/10.015 s）：

| 轮 | 平均 | 1% low | 0.1% low | 最大帧 | 可见峰值 |
|---|---:|---:|---:|---:|---:|
| 1 | 60.31 | 60.18 | 59.93 | 16.69 ms | 336 |
| 2 | 60.32 | 60.18 | 60.14 | 16.63 ms | 336 |
| 3 | 60.31 | 60.20 | 60.18 | 16.62 ms | 336 |

P5（200 全刚体，非门槛，604 帧/10.01 s）：

| 轮 | 平均 | 1% low | 0.1% low | 最大帧 | 全刚体 |
|---|---:|---:|---:|---:|---:|
| 1 | 60.31 | 60.19 | 60.18 | 16.62 ms | 200 |
| 2 | 60.32 | 60.20 | 60.16 | 16.62 ms | 200 |
| 3 | 60.30 | 60.17 | 60.08 | 16.64 ms | 200 |

## 最终 APK 复测（F2C1...）

P4（`--es breakline_auto stress`，603 帧/10.015–10.016 s）：

| 时间（本地） | 平均 | 1% low | 0.1% low | 最大帧 | 可见峰值 | T1 |
|---|---:|---:|---:|---:|---:|---:|
| 13:02:18 | 60.21 | 60.17 | 60.09 | 16.64 ms | 333 | 1 |
| 13:03:18 | 60.21 | 60.18 | 60.08 | 16.64 ms | 336 | 4 |
| 13:03:46 | 60.21 | 60.19 | 60.14 | 16.63 ms | 333 | 1 |

P5（`--es breakline_auto full`，603 帧/10.015 s）：60.21 / 60.18 / 60.13，最大帧 16.63 ms，`stress_full_max=200`，可见 200。

压敌：2026-11-12 13:04:53 logcat PASS
`initialAlive=True barrel=True crushed=True gateOpen=True`。
截图 `iqoo-crush-final.png`。

P6：**无效**。40×10 s 循环只留下 1 行旧格式数据；每次启动覆盖 CSV + 冷启动约 10 s，13 s 等待不足。`iqoo-M1-Performance-P6like-INVALID.csv` 仅作失败记录，不是 P6 数据。

## 重要边界

1. 这是 **Snapdragon 8 Gen 3 级高端机**，不是红米 Note 12 中端机；
   P4 通过不能替代中端最终门槛。
2. 测试时 AC 充电、电池 37–39°C、CPU 64–76°C，属于偏热/充电条件。
3. 最终 APK 已包含截图修复和 InputRouter owner 修复；
   P4 最大帧 16.64 ms，无 49.83 ms 截图尖刺。
4. 冷启动 `am start` 到自动压测开始约 10 s，首帧阶段 FPS 一度 6.7；
   P4 窗口不含冷启动，但该体验问题需单独评估。
5. Android `ScreenCapture` 路径仍被二次拼接，截图存储失败；
   外部 `adb screencap` 是证据；不影响游戏帧。
6. 摇杆释放用 adb 注入复测时仍出现触摸未释放/角色继续移动，
   真人手指释放未验收，不能写成 PASS。
