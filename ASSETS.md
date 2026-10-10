# 像素素材与提示词

薄荷像素猫由内置 imagegen 工具生成，再以原图为参考编辑喝水状态。原始透明 PNG 保存在 `assets/`，编译时嵌入程序；运行时不调用生成服务。糯米兔与月光夜猫的完整角色图由用户提供；动画身体底图用内置 imagegen 编辑生成。五件装饰是 `PixelDecor.cs` 中原创绘制的像素图案。未复制参考产品的角色、图片或代码。随项目分发的代码与素材使用根目录的 MIT 许可证。

## 主角色：`assets/mint-cat.png`

```text
Use case: stylized-concept. Asset type: a final original pixel-art sprite for a tiny transparent Windows desktop companion, displayed at about 140x120 pixels. Create one single charming sleepy ivory cat in a muted sage green oversized sweater, typing at a small retro lavender laptop. Three-quarter front view, compact squat silhouette, round cheeks, expressive tiny dark eyes, little salmon blush, asymmetrical ears and tiny tucked paws. Professional cozy indie-game pixel art, true clean square pixel clusters, approx 64x56 underlying grid enlarged with nearest-neighbor, controlled 14-color palette, dark desaturated plum one-pixel outlines, ivory/sage/lilac and warm peach accents. Subtle directional clusters of light and shade, deliberately simple shapes that read instantly when very small. Laptop with a tiny mint leaf symbol. Character and laptop ONLY, no table, no plants, no cups, no environment, no ground, no shadow outside silhouette. Isolated centered cutout on genuinely transparent background, tight framing with 8 percent transparent padding. No labels, no letters, no text, no frame, no user interface. Not photorealistic, no vector curves, no gradients, no glow, no 3D. Single pose and single character, not a sprite sheet. This is an original character, do not copy an existing mascot.
```

## 喝水：`assets/mint-cat-water.png`

以主角色为参考，保留透明背景：

```text
Change only the cat's pose: taking a little break, gently raising one small warm terracotta mug toward its mouth with a paw, content expression. Keep the same original ivory cat, sage sweater, lavender laptop, same pixel-art medium, palette, outlines and transparent background. Match original image dimensions, composition, scale and canvas placement closely so animation can switch images without size jumps. Laptop remains in exactly the same place. No text, no other accessories, transparent outside silhouette.
```

## 动画身体层：`assets/mint-cat-rig.png`

使用内置 imagegen，以主角色为编辑目标；保留原图，新身体层用于独立绘制眼睛和手部。

```text
Edit target: the attached original pixel-art desktop cat. Prepare a clean body layer for runtime skeletal animation. Remove BOTH dark eyes entirely and replace their exact areas with seamless ivory fur, preserving cheeks, nose and mouth. Remove BOTH cream-colored front paws on the laptop keyboard and extend the sage sweater sleeves over exactly those areas to form short empty cuffs; do not leave any fingers or pale paw fragments. Keep the cat, head silhouette, ears, sweater, tail, laptop, shading, pixel clusters, palette, dimensions, framing and placements otherwise as close to the original as possible. Do not move or resize anything. This is deliberately an eyeless and pawless body layer: our software will draw animated eyes and paws over it. Transparent background must remain genuinely transparent. No new objects, no eyes, no hands, no text, no panels.
```

## 程序内的动画

身体层不含静态眼睛与手掌；程序绘制眼部高光、瞳孔和手掌。瞳孔跟随鼠标，自动眨眼；鼠标左右键驱动独立手部动画，中键驱动双手。午休闭眼并停止敲击。薄荷猫喝水使用独立的原始透明图，两款新角色通过抬手和杯子组合显示喝水动作；其余动作由计时器实时推进。图片保留原始透明通道，没有改变角色的默认尺寸。

## 用户提供的完整角色

- `assets/apricot-rabbit.png`：杏桃糯米兔，1254 × 1254，透明 PNG。
- `assets/moon-cat.png`：月光夜猫，1254 × 1254，透明 PNG。

保留用户原图，编译时嵌入程序。手掌像素在内存中分离为动画纹理，眼部区域用于定位；三款眼睛统一由 `PixelEye.cs` 绘制。

## 两款动画身体底图

使用内置 imagegen，分别以对应完整角色图为编辑目标，生成 `assets/apricot-rabbit-rig.png` 和 `assets/moon-cat-rig.png`。尺寸均与原图一致，为 1254 × 1254，保留透明背景。

两张底图使用相同提示词：

```text
Edit this image into an animation body layer. Remove BOTH eyes including eyelids and highlights and fill their exact areas with seamless matching fur. Remove BOTH cream front paws on the keyboard and fill those areas with matching empty sweater cuffs. Keep nose, mouth, cheeks, fur, ears, tail, sweater and laptop otherwise unchanged. Preserve EXACT original canvas proportions, scale, pose and all positions. Do not redraw or redesign anything else. Keep actual transparent background. No eyes, no paws, no new objects or text.
```

`SpriteMotion.cs` 在底图上叠加原图的手掌以及共用的像素眼睛。眼眶固定，仅瞳孔与高光跟随鼠标，眨眼节奏与主角色一致。抬手时只移动袖口上方的局部纹理，笔记本和键盘保持固定。午休时闭眼并放下手掌，喝水时抬手并叠加杯子。运行时仅在内存中清除导出图片的极低透明度杂点，避免透明背景拦截下方窗口；不修改原始 PNG。

## 装饰

v0.8.0 用 `PixelDecor.cs` 重新绘制杯子、绿植、台灯、时钟与坐垫：有限色板、方形像素簇、暗紫轮廓，商店、桌宠与喝水共用。夜猫坐垫为淡紫色；物品 ID 和价格沿用原版本，购买记录保持兼容。装饰可摆上和收起。
