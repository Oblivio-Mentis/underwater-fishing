# RAID-018 · Living Marine AI & Fishing Feel — отчёт

База интеграции: `fea8ad4` (см. `AUDIT.md`, решение #11 в `.studioigor/DECISIONS.md`). Ветка `raid/018-living-marine-ai-fishing-feel`.

## Что сделано

_(заполняется по мере merge и проверок)_

## Новые конфиги и параметры

| Файл | Назначение |
|---|---|
| `Assets/_Game/Progression/FishFightProfile.cs` + `Data/Fishing/Fight*.asset` | фазы борьбы по видам: длительности, интервал/кулдаун рывка, веса решений, боковая сила, инерция поворота, двухгарпунный коэффициент тяги, шанс последнего рывка |
| `Assets/_Game/Data/Fishing/FishingTuning.asset → fightProfiles[]` | привязка профиля к `speciesId`; `burstPeriod/burstDuration` deprecated |
| `UiSettings.cameraShake`, `UiSettings.hitImpulse` (+ `fishingCamera` = Dynamic FOV) | локальные настройки комфорта, PlayerPrefs `RAID018.*` |

## Проверки

_(заполняется)_

## Известные ограничения

_(заполняется)_
