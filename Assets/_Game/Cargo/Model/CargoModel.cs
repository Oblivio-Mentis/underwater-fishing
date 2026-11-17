using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UnderwaterFishing.Cargo
{
    /// <summary>One run's unique manifest and repeatable winch. Only delivered cargo enters the protected hold.</summary>
    public sealed class CargoModel : IDisposable
    {
        private readonly CargoSettings settings;
        private readonly WinchMachine winch;
        private readonly List<CargoItem> container = new List<CargoItem>(), hold = new List<CargoItem>();
        private readonly HashSet<ulong> accepted = new HashSet<ulong>();
        public ReadOnlyCollection<CargoItem> Container { get; }
        public ReadOnlyCollection<CargoItem> Hold { get; }
        public int Capacity => Math.Max(1, settings.Capacity);
        public int UsedCapacity { get; private set; }
        public uint Revision { get; private set; }
        public WinchState State => winch.State;
        public float Progress => winch.Progress;
        public float SpeedMultiplier { get => winch.SpeedMultiplier; set => winch.SpeedMultiplier = value; }
        public bool CurrentLoadProtected => container.Count == 0;
        public event Action<WinchState, WinchState> StateChanged;

        public CargoModel(CargoSettings configuration)
        {
            settings = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Container = container.AsReadOnly(); Hold = hold.AsReadOnly();
            winch = new WinchMachine(settings);
            winch.Transitioned += Transitioned;
        }
        public bool CanLoad(CargoItem item) => State == WinchState.Deployed && item.IsValid
            && item.Size <= Capacity - UsedCapacity && !accepted.Contains(item.Id);
        public bool TryLoad(CargoItem item)
        {
            if (!CanLoad(item)) return false;
            // A world item cannot arrive pre-reserved; reservation is a base decision after delivery.
            item.Reserved = false;
            accepted.Add(item.Id); container.Add(item); UsedCapacity += item.Size; Revision++;
            return true;
        }
        public bool StartLift() => container.Count > 0 && winch.StartLift();
        public void Step(float dt) => winch.Step(dt);
        private void Transitioned(WinchState previous, WinchState next)
        {
            if (previous == WinchState.Unloading && next == WinchState.Descending)
            {
                hold.AddRange(container); container.Clear(); UsedCapacity = 0;
            }
            Revision++;
            StateChanged?.Invoke(previous, next);
        }
        public bool TrySetReserved(ulong id, bool reserve, bool atBase)
        {
            if (!atBase) return false;
            for (int i = 0; i < hold.Count; i++)
            {
                CargoItem item = hold[i];
                if (item.Id != id || !item.IsFish || item.Reserved == reserve) continue;
                item.Reserved = reserve; hold[i] = item; Revision++; return true;
            }
            return false;
        }
        public bool TryConsumeProtected(ulong id, CargoDisposition disposition, bool atBase, out CargoItem consumed)
        {
            consumed = default;
            if (!atBase) return false;
            for (int i = 0; i < hold.Count; i++)
            {
                CargoItem item = hold[i];
                if (item.Id != id) continue;
                if (disposition == CargoDisposition.Process && item.Reserved) return false;
                if (disposition == CargoDisposition.Offering && (!item.IsFish || !item.Reserved)) return false;
                if (disposition != CargoDisposition.Process && disposition != CargoDisposition.Offering) return false;
                hold.RemoveAt(i); consumed = item; Revision++; return true;
            }
            return false;
        }
        /// <summary>Called after normal unload completion or for emergency return. Hold is protected in both cases.</summary>
        public void ResetExpedition()
        {
            container.Clear(); UsedCapacity = 0; winch.Reset(); Revision++;
        }
        public void ResetRun()
        { ResetExpedition(); hold.Clear(); accepted.Clear(); Revision++; }
        public void Dispose()
        { winch.Transitioned -= Transitioned; winch.Dispose(); }
    }
}
