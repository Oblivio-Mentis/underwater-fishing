# Серверные поручения Гоши · 22 ноября 2026

В общем `RaidWorld` сервер получает отправителя команды из последнего параметра ServerRpc. Игрок задаёт только действие G/H, а цель, профиль, резерв рыбы и карман определяет хост. Индивидуальный ответ направляется сделавшему запрос клиенту. [FishNet RPC](https://fish-networking.gitbook.io/docs/guides/features/network-communication/remote-procedure-calls)

Для проверки пути используется объёмный проход сферы по настоящим коллайдерам сцены. Если буфер попаданий заполнен, участок считается закрытым. В шлюзе дополнительно проверяются фаза существующей двери и занятость камеры экипажем. [Unity Physics.SphereCastNonAlloc](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SphereCastNonAlloc.html)
