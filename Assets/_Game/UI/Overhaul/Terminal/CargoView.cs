using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    public sealed class CargoView
    {
        public RectTransform Root {get;private set;}
        readonly IUiController app;
        readonly TMP_Text capacity,phase,remaining,stock,manifest,description,loadReason,hoistReason,status;
        readonly Image capacityBar,phaseBar;
        readonly Image[] steps=new Image[4];
        readonly Button load,hoist;

        public CargoView(Transform parent,IUiController app)
        {
            this.app=app;RectTransform root;
            var layout=TerminalParts.Shell(parent,"CargoView",UiText.CargoTitle,UiText.CargoKicker,app,out root);Root=root;
            var stats=UiKit.Row(layout,"Stats",18);UiKit.Size(stats,102);
            var capBody=TerminalParts.Card(stats,"Capacity",102);UiKit.Size(capBody.parent,102,flexibleWidth:1);
            TerminalParts.Text(capBody,"Label",UiText.CapacityTitle,22,18,UiTheme.Muted);
            capacity=TerminalParts.Text(capBody,"Value","—",38,32,UiTheme.Cyan);
            var phaseBody=TerminalParts.Card(stats,"Phase",102);UiKit.Size(phaseBody.parent,102,flexibleWidth:1);
            TerminalParts.Text(phaseBody,"Label",UiText.CargoPhaseTitle,22,18,UiTheme.Muted);
            phase=TerminalParts.Text(phaseBody,"Value","—",38,30,UiTheme.Text);
            capacityBar=UiKit.Bar(layout,"CapacityProgress",UiTheme.Cyan,10);
            var sequence=UiKit.Row(layout,"PhaseSteps",8);UiKit.Size(sequence,46);
            for(int i=0;i<4;i++)
            {
                steps[i]=UiKit.Panel(sequence,"Phase_"+i,UiTheme.Panel);UiKit.Size(steps[i],46,flexibleWidth:1);
                var text=UiKit.Label(steps[i].transform,"Label",UiText.Phase((HoistPhase)i),21);UiKit.Fill(text.rectTransform,6);text.alignment=TextAlignmentOptions.Center;
            }
            phaseBar=UiKit.Bar(layout,"PhaseProgress",UiTheme.Cyan,10);
            remaining=TerminalParts.Text(layout,"Remaining","",28,22,UiTheme.Muted);
            description=TerminalParts.Text(layout,"PhaseDescription","",48,22);
            var actions=UiKit.Row(layout,"Actions",22);UiKit.Size(actions,116);
            var loadColumn=UiKit.Column(actions,"LoadColumn",8);UiKit.Size(loadColumn,116,flexibleWidth:1);
            load=UiKit.Button(loadColumn,"LoadCargo",UiText.LoadCargoAction,()=>TerminalParts.Execute(app,LoadReason,WorldAction.LoadCargo),true,52);
            loadReason=TerminalParts.Text(loadColumn,"Reason","",56,20,UiTheme.Muted);
            var hoistColumn=UiKit.Column(actions,"HoistColumn",8);UiKit.Size(hoistColumn,116,flexibleWidth:1);
            hoist=UiKit.Button(hoistColumn,"StartHoist",UiText.StartHoistAction,()=>TerminalParts.Execute(app,HoistReason,WorldAction.Hoist),true,52);
            hoistReason=TerminalParts.Text(hoistColumn,"Reason","",56,20,UiTheme.Muted);
            stock=TerminalParts.Text(layout,"DeliveredStock","",30,23,UiTheme.Green);
            manifest=TerminalParts.Text(layout,"Cargo manifest","",48,21,UiTheme.Text);
            status=TerminalParts.Text(layout,"Status","",36,20,UiTheme.Amber);
            Root.gameObject.SetActive(false);
        }
        public void Refresh()
        {
            if(!Root.gameObject.activeInHierarchy)return;var ctx=app.Context;var view=ctx.View;
            if(view==null){UiKit.Set(status,UiText.NoConfirmedState);load.interactable=hoist.interactable=false;return;}
            UiKit.Set(capacity,UiText.CargoCapacity(view.CargoUsed,view.Capacity(ctx.Tuning)));
            UiKit.Fraction(capacityBar,(float)view.CargoUsed/Mathf.Max(1,view.Capacity(ctx.Tuning)));
            capacityBar.color=view.CargoUsed>=view.Capacity(ctx.Tuning)?UiTheme.Amber:UiTheme.Cyan;
            UiKit.Set(phase,UiText.Phase(view.hoistPhase));
            for(int i=0;i<steps.Length;i++)
            {
                bool active=i==(int)view.hoistPhase;steps[i].color=active?UiTheme.Cyan:UiTheme.Panel;
                steps[i].GetComponentInChildren<TMP_Text>().color=active?UiTheme.Steel:UiTheme.Text;
            }
            float baseDuration=view.hoistPhase==HoistPhase.Raising?ctx.Tuning.raisingSeconds:view.hoistPhase==HoistPhase.Unloading?ctx.Tuning.unloadingSeconds:ctx.Tuning.returningSeconds;
            float duration=Mathf.Max(.01f,baseDuration/Mathf.Max(1,1+view.winchLevel*ctx.Tuning.winchSpeedPerLevel));
            UiKit.Fraction(phaseBar,view.hoistPhase==HoistPhase.Loading?0:1-view.hoistRemaining/duration);
            UiKit.Set(remaining,view.hoistPhase==HoistPhase.Loading?UiText.CargoPhaseSequence:UiText.CargoRemaining(view.hoistRemaining));
            UiKit.Set(description,view.hoistPhase==HoistPhase.Loading?(view.cargo.Count==0?"Загрузите улов, затем запустите подъём.":UiText.CargoLoadingHint):view.hoistPhase==HoistPhase.Raising?UiText.CargoRaisingHint:view.hoistPhase==HoistPhase.Unloading?UiText.CargoUnloadingHint:UiText.CargoReturningHint);
            TerminalParts.ButtonReason(load,loadReason,LoadReason(),UiText.LoadReady);TerminalParts.ButtonReason(hoist,hoistReason,HoistReason(),UiText.HoistReady);
            UiKit.Set(stock,UiText.DeliveredStock(view.tank.Count));
            if(view.cargo.Count==0)UiKit.Set(manifest,"Контейнер пуст");
            else UiKit.Set(manifest,string.Join("   ·   ",view.cargo.GroupBy(UiText.CargoName).Select(group=>group.Key+" ×"+group.Count())));
            string unavailable=TerminalParts.BaseReason(app,true);UiKit.Set(status,unavailable);
        }
        string LoadReason()
        {
            string reason=TerminalParts.BaseReason(app,true);if(!string.IsNullOrEmpty(reason))return reason;
            var ctx=app.Context;var view=ctx.View;
            if(view.hoistPhase!=HoistPhase.Loading)return UiText.CargoLocked;
            if(!ctx.HasCargo)return UiText.EmptyCarry;
            if((long)view.CargoUsed+ctx.CarriedSize>view.Capacity(ctx.Tuning))return UiText.CargoFull;
            return "";
        }
        string HoistReason()
        {
            string reason=TerminalParts.BaseReason(app,true);if(!string.IsNullOrEmpty(reason))return reason;
            var ctx=app.Context;var view=ctx.View;
            if(view.hoistPhase!=HoistPhase.Loading)return UiText.CargoLocked;
            if(view.cargo.Count==0)return UiText.CargoEmpty;
            return "";
        }
    }
}
