# P0 补充：后台恢复与跳跃/落地（2026-11-12 15:57）

设备：iQOO Neo10 V2425A，Android 16，最终 APK `1203BE1C...`（已安装）。

## 后台/前台

- `am force-stop` 后正常启动，等待 15s；记录 PID `19975`。
- 按 Home 键后台 20s，再 `am start` 回前台；PID 仍为 `19975`，窗口焦点仍为 `com.breakline.m1/com.unity3d.player.UnityPlayerActivity`。
- 截图：`bg-before.png`、`bg-after.png`；画面同一状态，无崩溃/重启。
- 结论：**20s 后台恢复冒烟 PASS**；真正 10 分钟后台、输入全链路仍未测，不能当完整 P0。

## 跳跃/落地

- 正常游戏中点击跳跃按钮约 `(2660,1120)`；截图：
  - `jump-clean-180.png`（约 180ms）
  - `jump-clean-580.png`（约 580ms，已落回）
  - `jump-clean-land.png`（落地后）
- 三张均未见爆炸/爆炸环；只有正常落地尘土；落地后玩家可继续操作。
- 由于相机跟随 Y，玩家在屏幕上的像素 y 基本固定，不能用截图 y 直接量跳跃高度；本轮只证明“点击跳跃 + 落地不爆炸”的冒烟，不证明短/长按高度差异，也不证明下压。
- 待测：短按/长按高度、coyote/buffer、下压砸地/不穿混凝土、贴墙跳。

## 结论

- 后台 20s：PASS（有限条件）。
- 跳跃/落地不爆炸：冒烟 PASS；高度/下压未验收。
- M1 P0 仍不完整，整体仍 Conditional/未 Go。
