# 最终 aim 修复构建证据清单（2026-11-12 15:25）

- 最终 APK：`6C9A4A3041863C81AC4F1DC5ADC65EA513E4DABC79C6E8B78F9953080725F38E`，20,038,662 B。
- 构建日志：`build-release-aimfinal.log`；临时 aim 日志构建：`build-release-aimlog.log`（APK `7A81...`，只用于验证输入方向；未作为交付）。
- 输入方向实测日志（测试构建，console 输出）：
  - 上划：`drag delta=0,19 dir=0.00,1.00`
  - 下划：`drag delta=0,-19 dir=0.00,-1.00`
- 最终 P4：`aim-p4-run1..3.csv` / `aim-p4-run1..3.png`。
  - 60.21/60.18/60.13/16.63；60.21/60.17/60.09/16.64；60.20/60.17/60.03/16.66。
- 最终 P5：`aim-p5-run1.csv` / `aim-p5-run1.png`，full=200。
- 最终压敌：`aim-crush-final.log` / `aim-crush-final.png`，PASS `initialAlive=True barrel=True crushed=True gateOpen=True`。
- P6 最终 ED98 构建（aim 修复前）：`iqoo-M1-Performance-P6-ED98.csv`（56 行/33,255 帧）、`P6-DONE-ED98.txt`、`p6-ed98.log`、`p6-ed98.png`。加权平均 60.207 / 最差 1% 60.1 / max 16.66ms。
- 自动刀 test-only：`knife-test.log`；最终构建共用同一逻辑。
- 注意：iQOO Neo10 是高端目标机，不外推中端；P6 是压力循环，不是完整游图。
