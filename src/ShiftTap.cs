using System;
namespace WireShelf
{
    // Pure gesture state: a held Shift, chord, click or context change cancels it.
    public sealed class ShiftTap
    {
        private bool down;
        private long pressed, first = -1;
        public void Reset() { down = false; first = -1; }
        public void Press(long now) { if (down) return; down = true; pressed = now; }
        public bool Release(long now, int interval)
        {
            if (!down) return false;
            down = false;
            if (now - pressed > 250) { first = -1; return false; }
            if (first >= 0 && now - first <= interval) { first = -1; return true; }
            first = now; return false;
        }
    }
}

