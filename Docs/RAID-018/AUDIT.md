# RAID-018 · Аудит перед реализацией

База интеграции: `fea8ad4` (ветка `raid/017-complete-visual-overhaul`, которая содержит `raid/018-nereid-expedition` = RAID-014…017 целиком, плюс материалы RAID-017). Ветка работы: `raid/018-living-marine-ai-fishing-feel`. PR #12–#14 уже слиты в эту линию: проверено `git merge-base --is-ancestor`.

## Что уже есть

| Область | Файл | Состояние |
|---|---|---|
| Серверная истина рыбы | `Fishing/FishActor.cs` | `SyncVar<FishSnapshot>`; FSM UniversalFSM `Roam/Hooked/Struggling/Tired/Captured/Transported/Escaped`; клиент интерполирует позицию |
| Плавание | `FishActor.Reef.cs`, `ReefHabitats.cs` | 13 маршрутов-ломаных по точкам, одна «полоса» высоты на вид, `MoveTowards` к следующей точке; испуг от промаха/близкого водолаза; sphere-cast по телу; без профилей — характер через `switch(SpeciesId)`; вне NEREID — вечный синус вокруг spawn |
| Борьба | `FishActor.Lines.cs` | до 5 линий, `FishingLine` спул с `PaidOut/WindingTime`; рывок строго периодический `burstPeriod`; тяга игрока через `ApplyTetherMotion`; боковой уход — `sin(fightClock)`; захват только по реальной смотке + `minimumWindingSeconds` + `captureRange` |
| Данные | `FishingTuning.cs` (один asset на всех) | `fightFov=7`, `burstFov=2`, `burstPeriod=3`, `initialStamina=7` × `power` |
| Хищники | `Threats/ThreatDirector.Reef.cs` | Фонарник: `Watching→Display(0.98 с)→Pursuit(прямой бросок 1.15 с)→Recovery`, патруль по 5 точкам; Сетевик фазы 0–3 с decoy; Капкан по радиусу 1.7 м у `ClamBed`; Кракен по шуму лебёдки с меткой зоны. Все константы в коде |
| Презентация угроз | `Factory/ThreatPresentation.cs`, `Nereid/NereidFauna.cs` | Силуэты, челюсть/хвост/плавники по стадии охоты; капкан — две створки раковины |
| Камера | `UI/UiRoot.LateUpdate` | единственный FOV-контроллер: `fightFov+burstFov` × `Settings.fishingCamera`, поверх `NereidAim`; `NereidFeedingCameraImpulse` — доп. смещение камеры (шум при рывке, отрыжка) |
| Оружие | `Factory/HarpoonPresentation.cs`, `HarpoonShotFx.cs` | пружина отдачи по событию `ShotVisual`; трос и наконечник по `HarpoonContact` на поверхности меша |
| HUD | `UI/Overhaul/Hud/ReelReadout.cs` | вид, дистанция, нагрузка, силы, гарпуны N/M, подсказка |
| VFX | `FishActor.Wake.cs`, `CatchFishModel.cs` | след из двух TrailRenderer по скорости; хвост/корпус по синусу с сетевым тиком |
| Проверки | `FishingTrial.cs` (хост + независимый клиент), `NereidEncounterChecks.cs` (риф на хосте), EditMode `FishingLineTests` | 54 EditMode, 38 проверок рифа |

## Чего не хватает

1. Нет профиля вида и зоны обитания как данных: объём, глубина, скорость, радиус разворота, дистанции реакции, предпочтения укрытия.
2. Движение — по точкам ломаной без инерции поворота; высота — фиксированная полоса; нет состояний Cruise/Hover/Forage/Alert/Flee/Recover.
3. Борьба — один периодический рывок; нет подфаз, нет весов решений, нет seeded PRNG; направление борьбы — синус.
4. Фонарник не кружит и не промахивается физически: прямой бросок, урон по дистанции центра. Нет leash, нет сброса агрессии по попаданиям.
5. Камера: нет удара FOV по подтверждённому зацепу, нет отдельных настроек Dynamic FOV / Shake / Hit impulse; импульс укуса отсутствует.
6. Капкан — раковина, а не бутон.
7. Нет уникальных sequence для попадания/укуса в снимках.

## Что расширяем (без второй архитектуры)

- `FishActor` остаётся единственным актёром; `FishSnapshot` получает `Behaviour`, `HookSequence`, `TugSequence`, `TugStrength` (контракт для презентации).
- Новое: `FishSpeciesProfile` (ScriptableObject на вид), `FishHabitat` (объёмы/зоны), `FishFightBrain` (чистая логика фаз в `Raid.Progression`, тестируется с seed).
- `ThreatDirector.Reef.cs` — полный цикл Фонарника на профиле `PredatorProfile`; `ThreatSnapshot` получает `BiteSequence/BiteVictim/BiteDirection`.
- `UiRoot.LateUpdate` остаётся владельцем FOV; `NereidFeedingCameraImpulse` остаётся владельцем смещения камеры; добавляются только входы (события) и настройки.
- UniversalFSM и FishNet без изменений структуры графа.
