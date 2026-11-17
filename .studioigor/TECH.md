# Technology — Underwater Fishing
ENGINE: unity
ENGINE VERSION: 6000.5.7f1
DIMENSION: 3D
LANGUAGE: C#
PLATFORMS: Windows x64 / Steam

## Decision
Стек утверждён ТЗ. Серверная симуляция FishNet, команды клиентов, Unity Physics.
UniversalFSM не зависит от сети, Presentation отображает подтверждённые события.
Steam-лобби и FishySteamworks обязательны; LAN/Tugboat для локальных проверок.
Зависимости закреплены SHA в manifest; lock коммитируется после успешного импорта.
## Project structure
Assets/_Game/{Data,Player,Diving,Network,World,UI,Editor}; следующие этапы добавят Fishing,Cargo,Progression.
