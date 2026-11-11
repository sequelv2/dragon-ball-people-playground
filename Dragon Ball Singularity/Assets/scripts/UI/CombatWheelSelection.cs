using System;

namespace DBPU
{
    // Pure navigation rules, shared by the live wheel and its executable checks.
    public sealed class CombatWheelSelection
    {
        public const int PageSize = 8;
        public int Count { get; private set; }
        public int Selected { get; private set; }
        public bool Armed { get; private set; }
        public int Page { get; private set; }
        public int Pages { get { return Math.Max(1, (Count + PageSize - 1) / PageSize); } }
        public int VisibleCount { get { return Math.Min(PageSize, Count - Page * PageSize); } }

        public void Reset(int count, int current)
        {
            Count = Math.Max(0, count);
            Selected = Count == 0 ? -1 : Math.Max(0, Math.Min(current, Count - 1));
            Page = Selected < 0 ? 0 : Selected / PageSize;
            Armed = false;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Count) { CancelSelection(); return; }
            Selected = index;
            Page = index / PageSize;
            Armed = true;
        }

        public void Step(int direction)
        {
            if (Count == 0 || direction == 0) return;
            Select(((Selected < 0 ? Page * PageSize : Selected) + (direction > 0 ? 1 : -1) + Count) % Count);
        }

        public void ChangePage(int direction)
        {
            if (Pages <= 1 || direction == 0) return;
            int slot = Selected < 0 ? 0 : Selected % PageSize;
            int next = (Page + (direction > 0 ? 1 : -1) + Pages) % Pages;
            Select(Math.Min(next * PageSize + slot, Count - 1));
        }

        public void SelectSlot(int slot)
        {
            int index = Page * PageSize + slot;
            if (slot >= 0 && slot < VisibleCount) Select(index);
        }

        public void CancelSelection() { Selected = -1; Armed = false; }

        // Clockwise from twelve o'clock; center and distant gestures are cancellation zones.
        public int Hit(double x, double y, double deadZone, double outerRadius)
        {
            double r2 = x * x + y * y;
            if (VisibleCount <= 0 || r2 < deadZone * deadZone || r2 > outerRadius * outerRadius) return -1;
            double angle = Math.Atan2(x, y) * 180.0 / Math.PI;
            if (angle < 0) angle += 360.0;
            double step = 360.0 / VisibleCount;
            int slot = (int)Math.Floor((angle + step * .5) / step) % VisibleCount;
            return Page * PageSize + slot;
        }
    }
}
