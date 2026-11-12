# Day 3 APK / M1 可安装包说明（更新：真机已启动）

> 更新时间：2026-11-12 15:25（Asia/Shanghai）
> 交付性质：M1 灰盒可安装包。**已在 vivo iQOO Neo10 真机安装/启动/触摸冒烟，并采集 P4/P5/P6 CSV。**
> **用户已于 2026-11-12 指定 iQOO Neo10 为最终目标机；它是高端机，结果不外推中端。M1 尚未最终 Go。**
> **2026-11-12 15:46 起，最终 APK 是自动丢手雷修复后的 `1203BE...`；本文 `6C9A...`/ED98 段落为历史。最新数据与限制以 [09_自动手雷修复_2026-11-12.md](09_自动手雷修复_2026-11-12.md) 为准。**
> 此前控制/汉化 + 拖拽 Y 轴方向修复后的 `6C9A...`；其数据见 [08_最终真机验收_2026-11-12.md](08_最终真机验收_2026-11-12.md)。

## 1. 文件

| 项 | 值 |
|---|---|
| Debug 路径 | `E:\BreaklineDeliver\BREAKLINE_M1_DAY3_DEBUG_arm64.apk` |
| Debug 大小 / SHA-256 | 21.30 MB / `D6E88A3D59A96D4BE7E3CDEAB1DD633B862E674E01A6C04E14AA1D5572D09882` |
| Release 路径 | `E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk` |
| Release 大小 / SHA-256 | 20,038,662 B（约 19.11 MiB）/ `6C9A4A3041863C81AC4F1DC5ADC65EA513E4DABC79C6E8B78F9953080725F38E` |
| 包名 / 版本 | `com.breakline.m1` / 0.1.0 (versionCode 1) |
| 引擎/构建 | Unity 2021.3.45f2，IL2CPP，ARM64，GLES3，Release |
| minSdk / targetSdk | 26 / 33 |
| 签名 | APK Signature Scheme v2；**本次 Release 仍用 Android debug 证书（内测安装包），不是正式商店签名**；Debug 同 debug 证书 |
| 启动 Activity | `com.unity3d.player.UnityPlayerActivity` |
| 方向 | 横屏 |
| 实测设备 | vivo iQOO Neo10（V2425A），Android 16 / SDK 36，arm64 |

**只含 arm64-v8a。** 32 位设备无法安装；红米 Note 12 若为 64 位可安装，但目前尚未在该机实测。

## 2. 安装

```powershell
adb install -r "E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk"
# 手感/调试可用 Debug：
# adb install -r "E:\BreaklineDeliver\BREAKLINE_M1_DAY3_DEBUG_arm64.apk"
```

iQOO 实测安装/启动命令：

```powershell
adb -s 10AEBB32GF000F9 install -r "E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk"
adb -s 10AEBB32GF000F9 shell am start -W -n com.breakline.m1/com.unity3d.player.UnityPlayerActivity
```

## 3. 控制

- 左摇杆：任意方向移动；向下压（配合跳跃）仍由摇杆 Y 轴识别。
- 右下：跳跃、手雷；**没有 FIRE 按钮**。在非摇杆/非按钮区域按住拖动 = 瞄准并射击，松手停止；不自动锁定/不自动射击。
- 匕首：被动近身，仅在没有射击/手雷意图时触发；有触发环与音效。
- JUMP：跳跃；空中“下 + JUMP”下压砸地。
- FIRE：三连射；自动瞄准可见目标。
- NADE：手雷，碰撞引信。
- 自动小刀：靠近普通敌人自动出刀；世界环 + tick + 2 帧 hitstop；按住/刚按过 FIRE 或 NADE 时不出刀。

## 4. 调试与数据

- 右上角 `DBG` 打开调试面板。
- `STRESS 200 + 12 + CHAIN`：200 可见 T2 + 12 物理件 + 链爆，10 秒记录；P4 证据用这个入口。
- `FULL 200 RIGIDBODIES`：P5 实验，不是 Go/No-Go 门槛。
- `DEBUG SLOWMO: BOSS/CLEAR`：白名单验证。
- `WORLD FLIP Y`：Android GLES 上实测世界正向，不需要打开；保留作异常驱动兜底。
- CSV：`/sdcard/Android/data/com.breakline.m1/files/M1-Performance.csv` 与 `M1-RawFrames-*.csv`。
- 自动测试入口：

```powershell
adb shell am force-stop com.breakline.m1
adb shell am start -n com.breakline.m1/com.unity3d.player.UnityPlayerActivity --es breakline_auto stress
# 或 --es breakline_auto crush
adb pull /sdcard/Android/data/com.breakline.m1/files/
```

## 5. 版本状态 / 源码与 APK 差异

已实测（最终 APK `F2C1...`）：

- iQOO 真机可安装、启动，世界方向正向，横屏/安全区正常；
- FIRE 击杀木靶，NADE 发射/爆炸/冷却，调试面板正常；
- 自动压敌 PASS：`initialAlive=True barrel=True crushed=True gateOpen=True`（2026-11-12 13:04:53）；
  证据 `docs/evidence/iqoo-crush-final.png`；
- P4 最终 APK 三轮：平均 60.21、1% low 60.17–60.19、最大帧 16.63–16.64 ms；
- P5 最终 APK 一轮：`stress_full_max=200`、60.21 fps、最大帧 16.63 ms（非门槛）；
- 设备/证据：`docs/evidence/iqoo-device-info.txt`、`iqoo-new-p4-run*.csv`、`iqoo-new-p5-run1.csv`、`iqoo-M1-Performance-P4P5.csv`、`iqoo-raw/`。

**已解决的差异：**

1. `GameRoot.cs` 已不在 10 s 测量窗口内截图；最终 APK P4 最大帧 16.64 ms，无 49.83 ms 尖刺。
2. `InputRouter.cs` 已加入 `touchCount==0` 清理 joystick owner；但用 adb 注入复测时仍出现触摸未释放/角色继续移动，**真人手指释放尚未验收**。
3. Unity 许可证已恢复，Release 重建成功（2026-11-12 12:53:28）；源码/APK 已一致。

**仍未完成：**

1. iQOO 是用户指定目标机，但属高端机，结果不外推中端机。
2. P6 连续 10 分钟：旧 40×10 s 循环因 CSV 覆盖 + 冷启动无效；新单进程 `p6` 模式测试中，结果未出。
3. 跳跃/下压、摇杆真人释放、后台恢复、完整 A→C 路线尚未全部验收；自动刀零误触已 deterministic PASS。
4. 冷启动 `am start` 到进入自动压测约 10 s，首帧阶段 FPS 一度 6.7；需作为体验问题单独评估。

## 6. 已验证与未验证

**已验证（iQOO 真机，最终 APK）：**

- 安装/启动/横屏/朝向；
- 触摸 FIRE/NADE；
- 自动压敌与闸门；
- P4（200 可见 + 12 物理 + 3 链爆）三轮，平均 60.21、最大帧 16.64 ms；
- P5（200 全刚体）一轮，`stress_full_max=200`；
- 自动刀“贴身 + 持续 FireHeld”零误触 deterministic PASS（`knife-test.log`，test-only APK `547720...`；最终 `ED98...` 共用同一逻辑）；
- 源码/APK 一致（最终 APK `ED98...`）。

**未验证/未完成：**

- 红米 Note 12 或同级中端机 P4/P6；
- 真正的 10 分钟连续热稳定（P6）；
- 摇杆释放/多指并存的真人手指复测（源码已修，adb 注入仍无法证明 PASS）；
- 跳跃/下压/土狼/缓冲的逐项验收；
- 8 向、隔墙锁定、链爆地板、完整 A→C 路线；
- 后台/回前台；
- 冷启动约 10 s / 首帧 6.7 fps 的体验问题；
- 手感主观项。

> 不能把“APK 可安装/已签名/在 iQOO 上 60 fps”写成“M1 已 Go”。最终判定见 `06_M1验收记录.md` 与 `05_M1性能报告.md`。

## 7. 需要你提供 / 下一步

1. **目标机已指定**：iQOO Neo10（高端机，报告不外推中端）；如需中端机数据，仍需红米 Note 12 或同级机。
2. P6 连续 10 分钟与 P0 剩余项（摇杆真人释放、跳跃/下压、路线、后台恢复）由我继续补测；自动刀零误触已 deterministic PASS。
3. 评估冷启动约 10 s 的体验问题。
4. 只有上述完成后，才做最终 Go/No-Go。

M1 仍只包含灰盒测试关，不含 M2 内容。

---

## 8. 2026-11-12 14:50 最终控制/汉化构建

- APK：`E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk`，20,030,542 B，SHA-256 `ED9846D2AF8FCE9AD4EF4DC4484344B64A7A7E6423AD8482F80E2BAC9A6DF78E`；`apksigner` 校验为 Android Debug 证书（内测包，不是正式发布签名）。
- 已实测（iQOO Neo10，Android 16）：
  - 全中文 HUD/调试面板；顶部提示“拖动屏幕瞄准射击”；
  - 左摇杆水平移动，松手后坐标保持，不再自动跑到 B 隧道；
  - 拖动屏幕产生瞄准/射击，松手即停，无自动瞄准/自动射击；
  - 跳跃/落地不触发爆炸；
  - 贴竖墙/向上挤压不慢滑、不卡墙；
  - 最终 APK P4 三轮 60.22/60.21/60.22，1% low 60.18–60.20，max 16.62–16.64 ms；P5 一轮 full=200；压敌 logcat PASS 14:48:41。
- 仍未完成：真正连续 10 分钟完整游图/后台恢复、跳跃/下压的逐项干净验收、完整 A→C 路线、摇杆真人手指释放。P6 的 61 窗口数据来自 `3D8C...` 测试构建，最终 `ED98...` 未重跑 P6。
- 自动刀零误触已 deterministic PASS（`knife-test.log`）；test-only APK 后已卸载并恢复最终 `ED98...`。
- Git：本地源码树与证据已提交；无 remote / `gh` 不可用，尚未上传远程。需要用户提供仓库 URL 与凭据（或先配置 SSH key）。

---

## 9. 2026-11-12 15:46 自动丢手雷修复（当前最终）

- APK：`E:\BreaklineDeliver\BREAKLINE_M1_DAY6_RELEASE_arm64.apk`，20,041,346 B，SHA-256 `1203BE1C0C2C01E9CA8624577AE4EA293324B3443DE2FBA43E8F3A327D251CF5`；`apksigner` v2，Android Debug 证书内测包。
- 修复：手雷按钮改为 0.08s/28px 短按确认；拖拽、瞄准、已有开火意图时取消；Android 隔离桌面 K/右键路径；第二根手指不再抢触发。
- 真机：空闲 20s 不自动投掷；从按钮起划被取消；明确按住 300ms 正常投掷；跳跃/瞄准拖拽均不触雷。
- 回归：P4 三轮 60.22/60.22/60.22，1% low 60.20/60.18/60.17，max 16.62–16.64 ms；P5 一轮 full=200；压敌 PASS 15:46:02。
- 未重跑 P6；P6 数据属于 `6C9A...`。完整说明见 [09_自动手雷修复_2026-11-12.md](09_自动手雷修复_2026-11-12.md)。
