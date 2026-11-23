using UnityEngine;

namespace Raid.UI
{
    public static partial class UiText
    {
        public const string MenuBrand = "UNDERWATER FISHING";
        public const string MenuService = "NEREID / СЛУЖБА ЭКСПЕДИЦИЙ";
        public const string MenuMainTitle = "Подготовка к погружению";
        public const string MenuMainSubtitle = "Один водолаз или экипаж до пяти человек";
        public const string MenuCreate = "Создать сессию";
        public const string MenuJoin = "Присоединиться";
        public const string MenuSettings = "Настройки";
        public const string MenuQuit = "Выйти из игры";
        public const string MenuBack = "Назад";
        public const string MenuResume = "Продолжить";
        public const string MenuCrew = "Экипаж и связь";
        public const string MenuDisconnect = "Покинуть сессию";
        public const string MenuPauseTitle = "Пост управления";
        public const string MenuDiveControls = "L — фонарь под водой     ·     Shift — ускорение     ·     ПКМ — прицел";
        public const string MenuPauseSubtitle = "Локальное меню. Экспедиция и время на сервере продолжаются.";
        public const string MenuPauseDescription = "Во время работы с меню движение, стрельба и взаимодействия вашего водолаза отключены. Возвращайтесь на пост, когда будете готовы.";
        public const string MenuConnectionTitle = "Связь с экипажем";
        public const string MenuConnectionSubtitle = "Создайте лобби для друзей или подключитесь к существующему экипажу.";
        public const string MenuCreateIntent = "Создать экипаж";
        public const string MenuJoinIntent = "Присоединиться к экипажу";
        public const string MenuSteamTab = "Steam";
        public const string MenuLocalTab = "Локальное тестирование";
        public const string MenuSteamTitle = "СВЯЗЬ ЧЕРЕЗ STEAM";
        public const string MenuSteamCreateHelp = "Лобби для друзей. Приглашение и его номер будут доступны в разделе «Экипаж и связь» после подключения.";
        public const string MenuSteamJoinHelp = "Получите номер лобби у хоста. Оба участника должны запустить совместимую сборку игры и войти в Steam.";
        public const string MenuSteamEnable = "Подключить Steam";
        public const string MenuSteamCreate = "Создать лобби для друзей";
        public const string MenuSteamJoin = "Войти в лобби";
        public const string MenuSteamIdPlaceholder = "Номер лобби Steam от хоста";
        public const string MenuSteamDevelopment = "Прототип: Steam AppID 480 используется только для разработки. Для проверки вдвоём нужны отдельные аккаунты и компьютеры.";
        public const string MenuLocalTitle = "ЛОКАЛЬНЫЙ ТЕСТ / РЕЖИМ РАЗРАБОТКИ";
        public const string MenuLocalHelp = "Проверка по IP без Steam. На одном компьютере укажите 127.0.0.1; в локальной сети — IP компьютера хоста.";
        public const string MenuAddressPlaceholder = "IP или имя компьютера хоста";
        public const string MenuLocalHost = "Запустить локальный хост";
        public const string MenuLocalJoin = "Подключиться по IP";
        public const string MenuConnectionStatus = "СОСТОЯНИЕ СВЯЗИ";
        public const string MenuConnectionBusy = "Устанавливаем соединение…";
        public const string MenuConnectionCancel = "Отменить подключение";
        public const string MenuConnectionMissing = "Подготовка службы подключения…";
        public const string MenuSteamNotInitialized = "Сервис Steam пока не подключён";
        public const string MenuSteamInitialized = "Steam подключён";
        public const string MenuSteamOverlay = "Открыта панель Steam";
        public const string MenuCrewTitle = "Экипаж NEREID";
        public const string MenuCrewSubtitle = "Здесь отображаются только фактически подключённые водолазы.";
        public const string MenuCrewEmpty = "Свободное место";
        public const string MenuCrewWaiting = "Ожидает подключения";
        public const string MenuHostRole = "Ваша роль: хост";
        public const string MenuClientRole = "Ваша роль: клиент";
        public const string MenuNoSession = "Нет активной сессии";
        public const string MenuSteamLobbyInactive = "Лобби Steam неактивно";
        public const string MenuLocalSession = "Локальная тестовая сессия";
        public const string MenuCopyLobby = "Скопировать номер лобби";
        public const string MenuInvite = "Пригласить друга в Steam";
        public const string MenuLobbyCopied = "Номер лобби скопирован";
        public const string MenuSettingsTitle = "Настройки приборов";
        public const string MenuSettingsSubtitle = "Применяются сразу и сохраняются локально, отдельно от прогресса экипажа.";
        public const string MenuScale = "МАСШТАБ ИНТЕРФЕЙСА";
        public const string MenuFov = "ПОЛЕ ЗРЕНИЯ";
        public const string MenuSensitivity = "ЧУВСТВИТЕЛЬНОСТЬ МЫШИ";
        public const string MenuDynamicFov = "ДИНАМИЧЕСКОЕ ПОЛЕ ЗРЕНИЯ";
        public const string MenuCameraShake = "ТРЯСКА КАМЕРЫ";
        public const string MenuReducedMotion = "СНИЖЕННАЯ АНИМАЦИЯ ИНТЕРФЕЙСА";
        public const string MenuEnabled = "Вкл.";
        public const string MenuDisabled = "Выкл.";
        public const string MenuHitImpulse = "ТОЛЧОК ПРИ ПОПАДАНИИ";
        public const string MenuSettingsLocalOnly = "Поле зрения и чувствительность относятся только к вашей камере. Они не меняют скорость водолаза, серверные правила или настройки других игроков.";
        public const string MenuRestore = "По умолчанию";
        public const string MenuRestoreDone = "Локальные настройки восстановлены";
        public const string MenuDisconnectTitle = "Покинуть сессию?";
        public const string MenuDisconnectHost = "Вы — хост. Ваш выход завершит сессию для всего экипажа. Миграция хоста не поддерживается.";
        public const string MenuDisconnectClient = "Вы вернётесь в главное меню. Прогресс экипажа остаётся у хоста.";
        public const string MenuQuitTitle = "Выйти из игры?";
        public const string MenuQuitDescription = "Приложение будет закрыто. Если вы хост, подключённые участники потеряют соединение.";
        public const string MenuFooterOnline = "Esc — назад к предыдущему экрану";

        public static string MenuBuild => "Версия " + Application.version;
        public static string MenuCount(int count) => "ПОДКЛЮЧЕНО: " + count + " / 5";
        public static string MenuOwner(int owner) => "Сетевой ID " + owner;
        public static string MenuLobby(string id) => "НОМЕР ЛОББИ  " + id;
        public static string MenuPercent(float value) => Mathf.RoundToInt(value * 100) + "%";
        public static string MenuDegrees(float value) => Mathf.RoundToInt(value) + "°";
    }
}
