# 抽象奶龙素材说明

v0.10.1 以用户提供的四张奶龙截图为视觉参考，分别用于捧腹大笑、全身比例、托腮表情及抱臂姿势。保留绿色圆眼、与躯干连续的头部、浅奶油色肚皮、黄色粗短四肢和深灰色手掌。参考视频：https://www.bilibili.com/video/BV1nTcQzYEnc/

`assets/nailong-atlas.png` 由内置 imagegen 根据这四张参考图重新生成。没有提取或分发 B 站原视频、配音或音乐，也没有把用户截图打入运行包。4×3 网格共 12 帧：待机、眨眼、点击回应、抱臂、大笑预备、大笑后仰、大笑前俯、大笑交替、喝水、打哈欠、睡觉、托腮。空闲时偶尔托腮；运行时清除不相连的透明杂点，仅改变内存中的渲染图。

奶龙名称及原角色的相关权利归其权利人。本项目代码适用 MIT；本说明不授予第三方角色、商标或参考视频的许可。该实验素材不代表官方授权。

## 生成提示词

```text
Use case: stylized-concept.
Asset type: final 12-frame transparent sprite atlas for a Windows desktop companion.
Reference images: image 1 is the exact belly-laugh face and pose; image 2 shows the FULL BODY proportions (use ONE individual, never duplicate a pair); image 3 shows the green round eyes and contemplative face; image 4 is the main identity and silhouette reference. Match THIS character closely, NOT the previous lanky creature and NOT the official chibi baby cartoon.
Identity to preserve in ALL frames: smooth bright warm yellow skin, thick pear-shaped body, head smoothly CONTINUOUS with the sloping torso without any narrow neck, large round protruding GREEN eyes with black pupils, small simple thin relaxed mouth, large pale CREAM oval belly patch, very thick yellow upper arms and forearms, dark gray short hands, short sturdy yellow legs, SMALL flat dark gray/brown feet. The body is plump, compact and soft with a very broad lower belly, as in the references. NOT a human muscular or skinny body. No long legs, long fingers, big separated head, exposed narrow neck, giant long feet, cheek blush or cute sparkles. Do not add clothes, spikes, tail, props beyond the single drinking mug.
Medium: simple smooth 3D meme render like the supplied references, restrained soft lighting, simple surfaces, no skin pores or detailed realism.
Composition: EXACT 4 columns and 3 rows, 12 equal square cells, total canvas aspect ratio 4:3. A single full-body character centered in each cell, consistent scale across ALL cells and a shared foot baseline. Clear empty transparent padding so all hands, head and feet are entirely inside their own cells. No duplicates within a cell. No background, no floor, no cast shadows, no colored halos or stray dots. Actual transparent RGBA. No text, labels, grid lines or dividers.
Pose order, left to right then top to bottom:
0 default relaxed neutral stance arms loosely DOWN, open GREEN eyes looking forward.
1 exactly same stance with eyes fully closed blinking.
2 brief click response: tiny belly recoil and hands loosely lifted, eyes OPEN.
3 satisfied arms crossed pose like reference 4, open green eyes.
4 beginning BELLY LAUGH like reference 1: bend FORWARD, both dark gray hands close together across cream belly, eyes squeezed shut, LARGE wide open smiling mouth with VISIBLE WHITE UPPER TEETH and dark mouth interior.
5 laugh peak: both hands pressing cream belly, upper body leans BACKWARD, head tipped back, eyes closed, huge open toothed laughter mouth.
6 laughter recovery: deeply bent FORWARD with both hands clutching belly, eyes closed, wide open toothed mouth, visibly different pose from 5.
7 alternate laugh peak: lean BACK and slightly SIDEWAYS squeezing belly, eyes shut and big open toothed mouth. Both short feet remain visible.
8 drinking from one tiny plain mug held against the mouth; other hand at side.
9 yawn covering open mouth with one dark gray hand.
10 sleepy squat, head tipped down eyes fully shut, hands resting together on cream belly; whole character visible.
11 thinking: one hand touching chin and other arm across cream belly like reference 3; green eyes looking sideways.
The laughter must read like the user's first reference with crescent eyes and a toothy open mouth. Absolutely NO laptop, monitor, computer, desk, room or office furniture. Preserve the continuous head-to-body shape and cream belly in every pose.
```

## 连续动画

v0.10.2 保留原始角色图集，运行时用一张稳定的捂肚子姿势做连续头部与肚皮形变，脚底固定，缓入缓出；不再以 8 fps 循环硬切四张大笑图。左右手按鼠标按键分别驱动，并在重复触发时保留当前抬手位置。活动动作使用约 16 ms 的刷新间隔；纹理缓存、行系数与预乘透明双线性采样减少重绘开销。

## 复现演示

```powershell
.\OfficeBuddyStudio.exe --nailong-video-frames .\work\video-frames
ffmpeg -framerate 50 -i .\work\video-frames\%04d.png -c:v libx264 -crf 20 -pix_fmt yuv420p -movflags +faststart .\work\demo.mp4
```

20 秒、1280×720、50 fps，无声；使用独立演示数据和软件实际的角色与悬停面板渲染，不读写用户存档。
