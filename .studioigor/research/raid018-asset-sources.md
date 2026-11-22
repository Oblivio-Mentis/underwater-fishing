# RAID-018 · Ассеты окружения

Проверено 23 ноября 2026 года. Модели для корабля и рифа выбраны отдельно от сторонних скриптов и демонстрационных сцен.

| Источник | Получение и решение |
|---|---|
| [Ultimate Modular Sci-Fi, Quaternius](https://quaternius.com/packs/ultimatemodularscifi.html) | CC0. На корабле уже есть детали из набора. Google Drive при проверке вернул ограничение загрузки новых FBX. |
| [Modular Sci-Fi MegaKit, Quaternius](https://quaternius.itch.io/modular-sci-fi-megakit) | CC0. Из официального Standard ZIP добавлены Unity FBX `Prop_AccessPoint`, `Prop_Crate4` и `Door_Frame_SquareTall`. |
| [Low Poly Underwater Pack, reodesuxz](https://reodesuxz.itch.io/low-poly-underwater-pack) | Официальный ZIP скачан. Четыре модели рифа можно установить локально для сборки игры. В репозитории есть процедурные варианты. |
| [Diver, nolanfa](https://sketchfab.com/3d-models/diver-9ee9b8b722ed457da424869cd8d08b1a) | Официальный API подтвердил скачиваемую модель CC BY 4.0. Неавторизованный запрос файла вернул HTTP 401. |
| [Bubbles VFX — URP](https://assetstore.unity.com/packages/vfx/bubbles-vfx-urp-389452) | Бесплатно через My Assets, совместим с URP. Сохранены собственные пузырьки и материалы. |
| [Low-poly Industrial Pack](https://assetstore.unity.com/packages/3d/props/low-poly-industrial-pack-397642) | Бесплатно через My Assets, совместим с URP. В проекте использованы доступные детали Quaternius. |
| [Sci-Fi Styled Modular Pack](https://assetstore.unity.com/packages/3d/environments/sci-fi/sci-fi-styled-modular-pack-82913) | Бесплатно через My Assets. Устанавливаемый файл в локальном кэше отсутствовал. |
| [Low Poly Fish](https://assetstore.unity.com/packages/3d/characters/animals/fish/low-poly-fish-339618) | Бесплатно через My Assets, совместим с URP. |
| [Fish — PolyPack](https://assetstore.unity.com/packages/3d/characters/animals/fish/fish-polypack-202232) | Бесплатно через My Assets, совместим с URP. |
| [Pixit3D — Fish Free Pack](https://assetstore.unity.com/packages/3d/characters/animals/fish/pixit3d-fish-free-pack-397878) | Бесплатно через My Assets, совместим с URP. |
| [Fish Alive — FREE Samples](https://assetstore.unity.com/packages/3d/characters/animals/fish/fish-alive-free-samples-362806) | Бесплатно через My Assets, совместим с URP. |
| [Simple Boids](https://assetstore.unity.com/packages/3d/characters/animals/simple-boids-flocks-of-birds-fish-and-insects-164188) | Бесплатно через My Assets. [Публичный исходник автора](https://github.com/nvjob/nvjob-boids) также доступен под MIT. Модель Fish 1 адаптирована для дальней стаи с существующим шейдером. |
| [Splash of Color — Unique Photogrammetry Plants](https://assetstore.unity.com/packages/3d/vegetation/plants/splash-of-color-unique-photogrammetry-plants-211137) | Бесплатно через My Assets. Набор весит 291 МБ и ориентирован на наземные растения. |
| [Fish Shader Animation](https://assetstore.unity.com/packages/vfx/shaders/fish-shader-animation-400006) | Бесплатно через My Assets, совместим с URP. В игре есть собственная анимация рыбы. |
| [Stylized Low Poly Rocks](https://assetstore.unity.com/packages/3d/environments/landscapes/stylized-low-poly-rocks-271334) | Бесплатно через My Assets. В проекте остаются доступные геометрические формы Kenney и собственный рельеф. |

Магазинные страницы используют [Standard Unity Asset Store EULA](https://unity.com/legal/as-terms). Для штатного [импорта через Unity](https://docs.unity3d.com/Manual/upm-ui-import.html) пакет должен быть в My Assets текущего Unity ID. Соответствующие файлы в локальном кэше и загрузках не найдены.

Локальные модели reodesuxz устанавливаются командой `python Tools/RAID018/Import-UnderwaterArt.py <LowPolyUnderwaterPack_v1.0.zip>`. Скрипт извлекает только используемые FBX. В сборку попадают игровые ресурсы, исходный ZIP остаётся вне проекта.
