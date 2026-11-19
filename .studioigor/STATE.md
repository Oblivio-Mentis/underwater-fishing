# studioigor - Underwater Fishing
PROJECT: Underwater Fishing
PHASE: 6 — Mechanics
UPDATED: 2026-11-19 14:11
## Now
Ревью RAID-012, ручная оценка анимаций и внешний Steam-плейтест с двумя аккаунтами.

## Open questions
Second Steam account/computer available later per user's answer. Cross-account Steam and human co-op/feel acceptance NOT RUN.
## Session log
| date | phase | done | next |
|---|---|---|---|
| 2026-11-18 | 4-6 | audit,dependencies,FoundationWindows host+Editor client,integrated loop,stock-loss fixture | finalWindows build,5connections,rescue/loot checks,user playtest |
| 2026-11-18 23:26 | 6 — Mechanics | Built integrated Windows prototype;23tests;5peers;RPC loop;wave stock-loss and persistence;archive prepared | User plays build and reviews feel; two-account Steam test when second device is ready; equal-interval balance and long co-op acceptance pending |
| 2026-11-19 00:26 | 6 — Mechanics | RAID-010 isolated visual slice implemented: NEREID hull/interior, depth-one environment, attached parasite, actual FP screenshots; 23 tests passing; local Editor and Windows peers verified. | Separate RAID-010 PR review and human visual/playtest feedback pending. Preserve PR7 branch and FirstPlayable build; no merge. |
| 2026-11-19 04:24 | 6 — Mechanics | RAID-011: обновлён UI/UX и по прямому указанию пользователя расширен физический прототип — резервуар, две трубы, переработчик, пасть, кормовой выход и взаимодействие напарников. Root проверил 26 EditMode-тестов и Editor-host с 4 Windows-клиентами; ручная приёмка не выполнена. | Phase 6: human acceptance pending. Root завершает проверку финальной Windows-сборки и отдельный PR raid/011-ui-ux → raid/010-visual-slice без merge. Второй Steam-аккаунт/ПК и нативный пользовательский ввод отложены; человеческий плейтест и оценка визуала остаются открытыми. |
| 2026-11-19 04:36 | 6 — Mechanics | RAID-011 complete for review: PR#9 https://github.com/Oblivio-Mentis/underwater-fishing/pull/9; non-development Windows x64 build0errors0warnings, Solo launcher+ZIP+46 real screenshots,26tests,Editor+4Windows peers, release two-process LAN smoke. No merge; parent branches/builds protected. | User playtest/review PR#9. Native Windows mouse/keyboard and Steam Overlay deferred by user; two Steam accounts/devices later. Human visual/feel acceptance and long co-op balance remain pending; phase6 retained. |
| 2026-11-19 13:03 | 6 — Mechanics | RAID-011: 36 EditMode-тестов, пуск/стоп, пять LAN-участников и отмена гарпуна с обеих сторон. В Play Mode подтверждены 4/16 доставленных рыб, голод, усиление перекорма и возврат камеры. Финальное видео снято с камеры владельца, исправлена отрисовка капель. Windows release без предупреждений. | Ручной плейтест NEREID и ревью PR #9. Steam между двумя устройствами и аккаунтами, Overlay, физический ввод, звук и баланс остаются на ручную проверку. |
| 2026-11-19 14:11 | 6 — Mechanics | RAID-012. Из камеры водолаза проверены HUD, резервуар, гибель и наблюдение. Два процесса FishNet поймали и доставили четыре улова, 38 EditMode-тестов и 16 проверок угроз прошли. | Ревью RAID-012, ручная оценка анимаций и внешний Steam-плейтест с двумя аккаунтами. |
