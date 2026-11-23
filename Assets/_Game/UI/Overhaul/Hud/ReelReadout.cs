using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    sealed class ReelReadout
    {
        readonly RectTransform root;
        readonly LineTensionArc arc;
        readonly TMP_Text species, range, hookCount;
        readonly Image stamina, speciesIcon;
        FishActor lastFish;
        float targetLoad, shownLoad, targetStamina, shownStamina;
        public bool Visible => root.gameObject.activeSelf;

        public ReelReadout(Transform parent)
        {
            root = UiKit.Node(parent, "Fishing instruments");
            UiKit.Fill(root);
            var gauge = UiKit.Node(root, "Line tension");
            UiKit.Place(gauge, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(238, 238));
            arc = gauge.gameObject.AddComponent<LineTensionArc>();
            arc.raycastTarget = false;

            var card = UiKit.Panel(root, "Hooked fish", new Color(.025f, .065f, .077f, .84f));
            UiKit.Tile(card);
            UiKit.Place(card.rectTransform, new Vector2(1f, .78f), Vector2.one, new Vector2(-26, 0), new Vector2(265, 72));
            speciesIcon = UiKit.Symbol(card.transform, "Species icon", DiveInstruments.Mark.Lantern, Color.white);
            UiKit.Place(speciesIcon.rectTransform, Vector2.up, Vector2.up, new Vector2(10, -7), new Vector2(29, 29));
            species = Label(card.transform, "Species", 47, -5, 146, 33, 23);
            species.fontStyle = FontStyles.Bold;
            range = Label(card.transform, "Distance", 189, -5, 65, 33, 19);
            range.color = UiTheme.Muted;
            range.alignment = TextAlignmentOptions.MidlineRight;

            var hook = UiKit.Symbol(card.transform, "Attached harpoons", DiveInstruments.Mark.Spear, UiTheme.Cyan);
            UiKit.Place(hook.rectTransform, Vector2.up, Vector2.up, new Vector2(10, -43), new Vector2(21, 20));
            hookCount = Label(card.transform, "Hook count", 35, -40, 42, 27, 17);
            var track = UiKit.Panel(card.transform, "Fish stamina track", new Color(.19f, .29f, .32f));
            UiKit.Place(track.rectTransform, Vector2.up, Vector2.up, new Vector2(83, -50), new Vector2(169, 7));
            stamina = UiKit.Panel(track.transform, "Remaining stamina", UiTheme.Cyan);
            UiKit.Fill(stamina.rectTransform);
            root.gameObject.SetActive(false);
        }

        static TMP_Text Label(Transform parent, string name, int x, int y, int width, int height, int size)
        {
            var label = UiKit.Label(parent, name, "", size);
            UiKit.Place(label.rectTransform, Vector2.up, Vector2.up, new Vector2(x, y), new Vector2(width, height));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        internal static string Species(string id) => id switch
        {
            "Bristlefish" => "Щетинник", "Sailray" => "Парусный скат", "Lanternfish" => "Фонарная рыба",
            "Needlefish" => "Иглорыба", _ => "Улов"
        };

        internal static string ActionHint(HarpoonTool tool, FishActor fish)
        {
            if (tool.LineTension >= LineTensionArc.Danger) return "Ослабить леску";
            if (fish.Surging || fish.Behaviour == FishBehaviour.LastEscape) return "Отпустить на рывке";
            if (fish.SurgeWarning || fish.Behaviour == FishBehaviour.SurgeWindup) return "Приготовиться отпустить";
            if (fish.RequiresPartner && fish.HookCount < 2) return "Нужен второй гарпун";
            if (fish.Mode == FishMode.Tired && fish.RequiresPartner) return "Тянуть и сблизиться";
            if (fish.RequiresPartner && !fish.Coordinated) return "Тянуть в одну сторону";
            return fish.Coordinated ? "Тянуть вместе" : "Подтягивать рыбу";
        }

        public void Refresh(UiContext ctx)
        {
            HarpoonTool tool = ctx.Harpoon;
            if (tool != null && tool.AssistTarget != null) tool = tool.AssistTarget.GetComponent<HarpoonTool>();
            FishActor fish = tool != null ? tool.HookedFish : null;
            bool active = !ctx.Player.IsDead && !UiInputGate.Modal && fish != null &&
                fish.Mode >= FishMode.Hooked && fish.Mode <= FishMode.Tired;
            bool entering = active && (!Visible || lastFish != fish);
            if (Visible != active) root.gameObject.SetActive(active);
            lastFish = active ? fish : null;
            if (!active) return;

            targetLoad = Mathf.Clamp01(tool.LineTension);
            targetStamina = Mathf.Clamp01(fish.Stamina / Mathf.Max(.01f, fish.tuning.initialStamina * fish.power));
            if (entering)
            {
                shownLoad = targetLoad;
                shownStamina = targetStamina;
                arc.ShowLoad(shownLoad);
                UiKit.Fraction(stamina, shownStamina);
            }

            speciesIcon.sprite = DiveInstruments.Art(DiveInstruments.CatchMark(fish.SpeciesId));
            UiKit.Set(species, Species(fish.SpeciesId));
            UiKit.Set(range, Mathf.RoundToInt(Vector3.Distance(tool.transform.position, fish.transform.position)) + " м");
            UiKit.Set(hookCount, fish.HookCount + "/" + (fish.RequiresPartner ? 2 : 1));
            hookCount.color = fish.RequiresPartner && fish.HookCount < 2 ? UiTheme.Amber : UiTheme.Text;
        }

        internal void Animate()
        {
            if (!root.gameObject.activeInHierarchy) return;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 24f);
            // Show danger on the first confirmed snapshot, even during a fast surge.
            shownLoad = targetLoad >= LineTensionArc.Danger && shownLoad < LineTensionArc.Danger
                ? targetLoad : Mathf.Lerp(shownLoad, targetLoad, blend);
            shownStamina = Mathf.Lerp(shownStamina, targetStamina, blend);
            arc.ShowLoad(shownLoad);
            UiKit.Fraction(stamina, shownStamina);
        }
    }
}
