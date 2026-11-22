using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector : NetworkBehaviour
    {
        public ThreatTuning tuning;
        public Vector3[] factoryWaveRoute;
        public static ThreatDirector Instance { get; private set; }
        public event Action<float> StockAttacked;
        public struct ThreatSnapshot
        {
            public Vector3 PredatorPosition, WavePosition,AnglerHeading;
            public HuntStage AnglerStage;
            public float PredatorHealth, WaveHealth;
            public bool WaveActive;
            public Vector3 WaveHeading;
            public int WaveHits;
            public string Message;
        }
        private readonly SyncVar<ThreatSnapshot> state = new();
        private Transform predatorView, waveView;
        private bool ownsTuning, throughAirlock;
        private int depthTier = 1, stockAttacks;
        private int factoryWaypoint;
        private Collider reservoirWall;
        readonly RaycastHit[] routeHits = new RaycastHit[24];
        public Vector3 WavePosition => state.Value.WavePosition;
        public float WaveHealth => state.Value.WaveHealth;
        public int WaveHits => state.Value.WaveHits;
        private float predatorCooldown, respawn, waveRemaining, stockCooldown, waveDeficit, patrolTime;
        public bool WaveActive => state.Value.WaveActive;
        public string Message => state.Value.Message ?? "";
        public HuntStage AnglerStage=>state.Value.AnglerStage;

        private void Awake()
        {
            Instance = this;
            if (tuning == null) { tuning = ScriptableObject.CreateInstance<ThreatTuning>(); ownsTuning = true; }
            state.UpdateSettings(new SyncTypeSettings(Mathf.Max(.02f, tuning.snapshotInterval)));
            reefState.UpdateSettings(new SyncTypeSettings(.15f));
            predatorView = Primitive("Predator", new Vector3(1.4f, .8f, 2.8f), new Color(.8f, .23f, .12f));
            waveView = Primitive("Reservoir raider", new Vector3(1.1f, .7f, 1.8f), new Color(.8f, .1f, .6f));
        }

        private Transform Primitive(string label, Vector3 scale, Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = label;
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = scale;
            Collider collider = visual.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
            visual.GetComponent<Renderer>().SetPropertyBlock(properties);
            visual.SetActive(false);
            return visual.transform;
        }

        public override void OnStartServer()
        {
            ResetReef();
            depthTier = 1; predatorCooldown = 0f; respawn = 0f; patrolTime = 0f;
            state.Value = new ThreatSnapshot { PredatorPosition = tuning.predatorSpawn, PredatorHealth = tuning.predatorHealth };
        }

        public override void OnStopClient() { if(reefView!=null)reefView.gameObject.SetActive(false); predatorView.gameObject.SetActive(false); waveView.gameObject.SetActive(false); }

        public void SetDepth(int tier)
        {
            if (!IsServerInitialized) return;
            depthTier = Mathf.Clamp(tier, 1, 2);
            ResetReef();
            ThreatSnapshot next = state.Value;
            next.PredatorPosition = tuning.predatorSpawn;
            next.PredatorHealth = tuning.predatorHealth * Strength;
            next.AnglerStage=HuntStage.Watching;next.AnglerHeading=Vector3.forward;
            respawn = 0f; predatorCooldown = 0f;
            state.Value = next;
        }

        private float Strength => depthTier > 1 ? Mathf.Max(1f, tuning.depthTwoStrength) : 1f;

        public void StartWave(float deficit, int masteredDepth)
        {
            if (!IsServerInitialized || WaveActive || !Finite(deficit) || deficit <= 0f) return;
            ThreatSnapshot next = state.Value;
            waveDeficit = Mathf.Clamp01(deficit);
            next.WaveActive = true; next.WavePosition = tuning.waveSpawn;
            next.WaveHeading = Vector3.forward;
            var vessel = Nereid.NereidVessel.Active;
            reservoirWall = vessel == null ? null : vessel.transform.Find("Cylindrical tank collision")?.GetComponent<Collider>();
            next.WaveHealth = tuning.waveHealth * Mathf.Max(1, masteredDepth - 1) * Mathf.Lerp(.5f, 1f, waveDeficit);
            next.Message = "ГОЛОД: мелководный налётчик идёт к резервуару! Защитите запас гарпунами.";
            waveRemaining = Mathf.Max(1f, tuning.waveSeconds); stockCooldown = 0f; stockAttacks = 0; throughAirlock = false;factoryWaypoint=0;
            state.Value = next;
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized) return;
            ThreatSnapshot next = state.Value;
            float dt = Time.fixedDeltaTime;
            if(Nereid.NereidVessel.Active!=null)
            {
                TickReef(ref next,dt);
                if(reefState.Value.Active)TickAngler(ref next,dt);
            }
            else TickPredator(ref next, dt);
            if (next.WaveActive)
            {
                waveRemaining -= dt;
                bool factoryPath=factoryWaveRoute!=null&&factoryWaveRoute.Length>0;
                Vector3 goal = factoryPath?factoryWaveRoute[Mathf.Min(factoryWaypoint,factoryWaveRoute.Length-1)]:throughAirlock ? tuning.reservoirPoint + tuning.reservoirApproachOffset : tuning.airlockWaypoint;
                Vector3 delta = goal - next.WavePosition;
                bool atTank = false;
                if (delta.sqrMagnitude > .0001f)
                {
                    var direction = delta.normalized;
                    next.WaveHeading = direction;
                    float step = Mathf.Min(delta.magnitude, Mathf.Max(.1f, tuning.pursuitSpeed) * dt);
                    float clearance = Mathf.Max(.05f, tuning.raiderBodyRadius);
                    int count = Physics.SphereCastNonAlloc(next.WavePosition, clearance, direction, routeHits,
                        step + .03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    float closest = float.PositiveInfinity;
                    Collider barrier = null;
                    for (int i = 0; i < count; i++)
                    {
                        var hit = routeHits[i];
                        if (hit.transform.IsChildOf(waveView) || hit.distance >= closest) continue;
                        closest = hit.distance;
                        barrier = hit.collider;
                    }
                    if (count == routeHits.Length) step = 0;
                    else if (barrier != null) step = Mathf.Clamp(closest - .03f, 0, step);
                    next.WavePosition += direction * step;
                    atTank = barrier != null && barrier == reservoirWall && closest <= .04f && factoryPath && factoryWaypoint == factoryWaveRoute.Length - 1;
                }
                if (atTank || Vector3.Distance(next.WavePosition, goal) < .2f)
                {
                    if(factoryPath&&factoryWaypoint<factoryWaveRoute.Length-1)factoryWaypoint++;
                    else if (!factoryPath&&!throughAirlock) throughAirlock = true;
                    else if ((Nereid.NereidVessel.Active == null || atTank) && (stockCooldown -= dt) <= 0f)
                    {
                        StockAttacked?.Invoke(waveDeficit);
                        ++stockAttacks;
                        stockCooldown = Mathf.Max(.1f, tuning.stockAttackInterval);
                        next.Message = "Налётчик повреждает резервуар! Часть необработанного улова под угрозой.";
                    }
                }
                if (waveRemaining <= 0f || stockAttacks >= Mathf.Max(1, tuning.maximumWaveAttacks))
                { next.WaveActive = false; next.Message = "Нападение окончено. Технологии и улучшения сохранены."; }
            }
            state.Value = next;
            PlaceRaider(next, false);
        }

        void PlaceRaider(ThreatSnapshot snapshot, bool interpolate)
        {
            bool appeared = !waveView.gameObject.activeSelf;
            waveView.gameObject.SetActive(snapshot.WaveActive);
            if (!snapshot.WaveActive) return;
            float blend = interpolate && !appeared ? 1 - Mathf.Exp(-Time.deltaTime / tuning.snapshotInterval) : 1;
            waveView.position = Vector3.Lerp(waveView.position, snapshot.WavePosition, blend);
            if (snapshot.WaveHeading.sqrMagnitude > .01f)
                waveView.rotation = Quaternion.Slerp(waveView.rotation, Quaternion.LookRotation(snapshot.WaveHeading), blend);
        }

        private void TickPredator(ref ThreatSnapshot next, float dt)
        {
            if (next.PredatorHealth <= 0f)
            {
                respawn -= dt;
                if (respawn <= 0f) { next.PredatorHealth = tuning.predatorHealth * Strength; next.PredatorPosition = tuning.predatorSpawn; }
                return;
            }
            predatorCooldown -= dt; patrolTime += dt;
            DiverMotor target = null;
            float nearest = tuning.detectionRange * tuning.detectionRange;
            foreach (DiverMotor diver in DiverMotor.ServerDivers)
            {
                if (diver == null || diver.IsInside || diver.Oxygen <= 0f) continue;
                float distance = (diver.transform.position - next.PredatorPosition).sqrMagnitude;
                if (distance < nearest) { nearest = distance; target = diver; }
            }
            Vector3 goal = target != null ? target.transform.position : tuning.predatorSpawn +
                new Vector3(Mathf.Sin(patrolTime * tuning.patrolSpeed), 0f, Mathf.Cos(patrolTime * tuning.patrolSpeed)) * tuning.patrolRadius;
            Vector3 step = Vector3.MoveTowards(next.PredatorPosition, goal, tuning.predatorSpeed * Mathf.Sqrt(Strength) * dt);
            if (!Physics.Linecast(next.PredatorPosition, step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) next.PredatorPosition = step;
            if (target == null || predatorCooldown > 0f || Vector3.Distance(next.PredatorPosition, goal) > tuning.attackRange) return;
            if (Physics.Linecast(next.PredatorPosition, goal, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(target.transform)) return;
            target.DamageOxygen(tuning.attackDamage * Strength);
            target.GetComponent<HarpoonTool>()?.DropCargo();
            predatorCooldown = Mathf.Max(.1f, tuning.attackInterval);
            next.Message = "Хищник ударил водолаза и сорвал улов. Укройтесь в подлодке или за скалой!";
        }

        public bool Strike(Vector3 origin, Vector3 direction, float power, RaycastHit firstHit)
        {
            if (!IsServerInitialized || !Finite(origin.x) || !Finite(origin.y) || !Finite(origin.z) ||
                !Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z) || !Finite(power) || power <= 0f || direction.sqrMagnitude < .01f) return false;
            ThreatSnapshot next = state.Value;
            direction.Normalize();
            bool waveHit = next.WaveActive && firstHit.collider != null && firstHit.distance <= tuning.strikeRange &&
                firstHit.collider.transform.IsChildOf(waveView);
            bool predatorHit = next.PredatorHealth > 0f && Hits(origin, direction, next.PredatorPosition);
            if (!waveHit && !predatorHit) return StrikeReef(origin,direction);
            power = Mathf.Clamp(power, 0f, tuning.maximumStrikePower);
            if (waveHit)
            {
                next.WaveHealth -= power;
                next.WaveHits++;
                next.Message = "Гарпун попал в налётчика!";
                if (next.WaveHealth <= 0f) { next.WaveActive = false; next.Message = "Резервуар защищён. Налётчик не даёт ресурсов."; }
            }
            else
            {
                next.PredatorHealth -= power;
                if (next.PredatorHealth <= 0f) { respawn = Mathf.Max(1f, tuning.predatorRespawnSeconds); next.Message = "Хищник отступил. Путь временно безопасен."; }
            }
            state.Value = next;
            return true;
        }

        private bool Hits(Vector3 origin, Vector3 direction, Vector3 target)
        {
            float along = Vector3.Dot(target - origin, direction);
            if (along < 0f || along > tuning.strikeRange || (origin + direction * along - target).sqrMagnitude > tuning.strikeRadius * tuning.strikeRadius) return false;
            return !Physics.Linecast(origin, target, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private void LateUpdate()
        {
            if (!IsClientInitialized) return;
            ThreatSnapshot snapshot = state.Value;
            bool nereid=Nereid.NereidVessel.Active!=null;
            if(nereid)ShowReef();
            Show(predatorView, snapshot.PredatorPosition, snapshot.PredatorHealth > 0f && (!nereid || reefState.Value.Active));
            if(nereid && snapshot.AnglerStage!=HuntStage.Watching && snapshot.AnglerHeading.sqrMagnitude>.01f)
                predatorView.rotation=Quaternion.RotateTowards(predatorView.rotation,Quaternion.LookRotation(snapshot.AnglerHeading),Time.deltaTime*360);
            if (!IsServerInitialized) PlaceRaider(snapshot, true);
        }

        private void Show(Transform visual, Vector3 position, bool visible)
        {
            bool wasVisible = visual.gameObject.activeSelf;
            visual.gameObject.SetActive(visible);
            if (!visible) return;
            Vector3 previous = visual.position;
            visual.position = wasVisible ? Vector3.Lerp(previous, position, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.02f, tuning.snapshotInterval))) : position;
            if ((position - previous).sqrMagnitude > .001f) visual.rotation = Quaternion.LookRotation(position - previous);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnDestroy() { if (Instance == this) Instance = null; if (ownsTuning) Destroy(tuning); }
    }
}
