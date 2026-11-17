# State — Underwater Fishing

## Now
17.11.2026: foundation в ветке raid/001-foundation, база origin/main. Полная цель — ВСЕ RAID-001…009,
ТЗ docs/RAID-SPEC.md. Steam обязателен, прежний запрет отменён прямым уточнением пользователя.
Реализованы NetworkSession, SteamLobbyService/Router, сетевой мотор/InputActions, пещера/подлодка,
серверные кислород/давление, HUD. Отчёт: docs/RAID-001-REPORT.md.

## Verified
- 23/23 Unity EditMode tests, job0976333d72e245dca84cca37334ce41a.
- Windows2/5 отдельных процессов видят всех игроков; client crash и host loss обработаны, исключений в QA-логах нет.
- Steam480: настоящее лобби создано, локальный Steam-хост1 участник подключён; удалённый аккаунт НЕ тестировался.
- Input Actions -> RPC -> перемещение до столкновения со стенкой.
- Исправлены свободные spawn slots, Esc offline, сохранность исходного InputActionAsset, объём герметичной зоны.
- Два последовательных Play после исправления InputActions:0 ошибок. bowSheltered=True, O2=180.
- Windows Development build176793380 bytes; финальная перепроверка2/5 после последней правки прошла.

## Next
1. Продолжать реализацию от foundation в новой ветке, используя stacked PR к raid/001-foundation.
2. В новой ветке от foundation последовательно RAID-003: два вида рыб/UniversalFSM, гарпун/трос/сопротивление;
   интегрировать спасение/наблюдение из RAID-002/004. Затем Cargo, Expedition, Progression, Offering, full QA.
3. НЕ уменьшать цель до foundation. main не объединять без ревью.

## Open questions
Настоящий Steam App ID для релиза ещё не указан;480 только Editor/Development.
Steam между разными аккаунтами/устройствами и приглашения ещё не проверены.

## Session log
- Unity MCP HTTP8080; использовать tools/unity_mcp.py. Не запускать второй Editor на тот же проект.
- Останавливать Play перед редактированием C#. force refresh compile=request может отложить reload до Play — избегать;
  использовать AssetDatabase.Refresh и проверить EditorApplication.isCompiling/isPlaying.
- Build после Play вызывать через EditorApplication.delayCall += FoundationBuilder.BuildWindows,
  иначе Unity может вернуть Unknown: inside player loop. Успех проверять по RAID_BUILD_SUCCEEDED в Logs/Editor.log.
- При повторной генерации сцены исходный InputActionAsset теперь не уничтожается: runtimeActions хранится отдельно.
- GitHub Schannel TLS ошибку устранила URL-specific глобальная настройка sslBackend=openssl.
- Исследовательские исходники FishNet/Steam/FSM находятся в AppData/Local/Temp/raid-*. Рабочие worktree RAID-diving и RAID-steam уже влиты в foundation.

## Git
Код foundation: 3c2185c. Draft PR: https://github.com/Oblivio-Mentis/underwater-fishing/pull/1
Ветка отправлена; main не объединялся. Коннектор GitHub create PR вернул403, PR создан через авторизацию локального Git.
Финальные Windows QA2/5 после последней правки: passed, client cleanup=true, host loss=true (5-process run).