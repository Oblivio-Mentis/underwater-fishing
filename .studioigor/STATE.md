# State — Underwater Fishing

## Now
Зафиксировать stacked PR к raid/001-foundation, затем RAID-004/005: спасение, завал, крупная рыба в сетевом сценарии, контейнер и лебёдка. Далее экспедиции, ресурсы, улучшения, сонар и подношения. Steam двух аккаунтов ещё не проверен. Editor закрыт после пакетных сборок; надёжные команды tools/Build-Windows.ps1, Run-LogicTests.ps1, Verify-Fishing.ps1 -WaitForShutdown. Всю цель RAID-001..009 сохранять.

## Verified
- 41/41 Unity EditMode tests; повторный studio.py verify прошёл TEST и CAPTURE, ENV VERIFIED=yes.
- Windows2/5 отдельных процессов видят всех игроков; client crash и host loss обработаны, исключений в QA-логах нет.
- Steam480: настоящее лобби создано, локальный Steam-хост1 участник подключён; удалённый аккаунт НЕ тестировался.
- Input Actions -> RPC -> перемещение до столкновения со стенкой.
- Исправлены свободные spawn slots, Esc offline, сохранность исходного InputActionAsset, объём герметичной зоны.
- Два последовательных Play после исправления InputActions:0 ошибок. bowSheltered=True, O2=180.
- Windows Development 0.0.1-raid003; повторный LAN клиент реально поймал рыбу с помощью хоста, оба процесса вышли0 без исключений; кадры осмотрены. Перепроверка5 процессов прошла.

## Next
1. Отправить raid/003-fishing как stacked PR к raid/001-foundation.
2. Исправить найденные проверкой Steam проблемы повторного хоста; Steam обязателен для лобби и транспорта.
3. RAID-004: спасение/наблюдение, завал; RAID-005 Cargo, далее Expedition, Progression, Offering, full QA.
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
| 2026-11-17 22:45 | ? | RAID-003: branch raid/003-fishing. FishBrain UniversalFSM, spatial tether, harpoon ServerRpc, catch/carry/drop, bracing, Presentation SFX/flash, FishingGym tuning implemented. 40/40 Unity EditMode tests passed job5e7d4d17eabe4fe9b4c4ac0e87329af7. Fixed state domain metadata and static cave collider geometry; runtime input bots merged, not yet tested. | Regenerate scenes, build Windows, run tools/Verify-Fishing.ps1 two-process real remote catch/assistance, inspect screenshots and fixes, then stacked PR to raid/001-foundation. Continue full RAID scope afterward. |
| 2026-11-17 23:34 | ? | RAID-003: реализованы UniversalFSM рыб, пространственный трос, серверный гарпун, поимка/переноска и помощь F. 41/41 Unity EditMode; удалённый Windows-клиент реально поймал рыбу с напарником, штатное закрытие обоих процессов прошло без исключений. Пять процессов повторно проверены. Кадры native-fishing/fishing-fight.png и fishing-caught.png осмотрены; трос и переноска исправлены. Полный Core Loop ещё не завершён. | Зафиксировать stacked PR к raid/001-foundation, затем RAID-004/005: спасение, завал, крупная рыба в сетевом сценарии, контейнер и лебёдка. Далее экспедиции, ресурсы, улучшения, сонар и подношения. Steam двух аккаунтов ещё не проверен. Editor закрыт после пакетных сборок; надёжные команды tools/Build-Windows.ps1, Run-LogicTests.ps1, Verify-Fishing.ps1 -WaitForShutdown. Всю цель RAID-001..009 сохранять. |
