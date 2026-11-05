### [俄文](./README_RU.md) | [English](./README.md) | [> 简体中文 <](./README_ZH.md)

---

# RadioReplacer

### 插件作者：[Mr_Dodep](https://t.me/MrDodep_1337)

将游戏电台替换为自定义音乐的模组。仅在本地生效，其他玩家仍然听到原版电台。

---

## 安装

1. 将 `RadioReplacer.dll` 复制到 `BepInEx/plugins/`。
2. 启动游戏，模组会自动创建音乐文件夹。

---

## 音乐文件夹

首次启动后，游戏目录内会出现以下文件夹：

```text
BepInEx/plugins/RadioReplacer/
├── tracks_1/
├── tracks_2/
├── tracks_3/
├── tracks_4/
└── tracks_5/
```

每个文件夹对应游戏中的一个电台。
支持格式：`.ogg`、`.wav`。

将音乐文件放入对应文件夹，模组会自动识别。

---

## 使用方法

1. 将曲目添加到 `tracks_1` 至 `tracks_5` 文件夹。
2. 进入游戏并坐进车辆。
3. 按 **Alt + M** 打开 RadioReplacer 菜单。
4. 选择电台，将曲目替换为自己的音乐。
5. 关闭菜单，音乐会立即生效。

---

## 说明

- 替换仅在本地生效，只有你自己能听到。

- 文件夹中的文件越多，对应电台的曲目就越多。

- 如果文件夹为空，电台会继续播放原版音乐。

- 支持长音轨，无文件大小限制。
