namespace TowerLand.UI
{
    /// <summary>
    /// Counts quick consecutive taps (each within <see cref="MaxGap"/> seconds of the previous one) and reports when a
    /// run reaches the required count. A slow tap starts a new run. Plain C#, so it is unit-tested.
    /// </summary>
    public sealed class TapSequence
    {
        public float MaxGap { get; }
        private int _count;
        private float _last = float.NegativeInfinity;

        public TapSequence(float maxGap) => MaxGap = maxGap;

        public int Count => _count;

        /// <summary>Registers a tap at <paramref name="time"/>; true when this tap completes a run of <paramref name="required"/> (the run then restarts).</summary>
        public bool Tap(float time, int required)
        {
            _count = time - _last <= MaxGap ? _count + 1 : 1;
            _last = time;
            if (_count < required) return false;
            Reset();
            return true;
        }

        public void Reset()
        {
            _count = 0;
            _last = float.NegativeInfinity;
        }
    }
}
