using System;

namespace Robit.Logic
{
    public interface ICyclerLogic
    {
        int SelectedIndex { get; set; }
        void Cycle(int direction, int totalItems);
        void Reset();
    }

    public class CyclerLogic : ICyclerLogic
    {
        public int SelectedIndex { get; set; }

        public void Cycle(int direction, int totalItems)
        {
            if (totalItems <= 0)
            {
                SelectedIndex = 0;
                return;
            }

            // Standard wrap-around logic
            SelectedIndex = ((SelectedIndex + direction) % totalItems + totalItems) % totalItems;
        }

        public void Reset()
        {
            SelectedIndex = 0;
        }
    }
}
