public interface ICyclerLogic
{
    int SelectedIndex { get; }
    void Cycle(int direction, int totalItems);
    void Reset();
}

public class CyclerLogic : ICyclerLogic
{
    public int SelectedIndex { get; private set; }

    public void Cycle(int direction, int totalItems)
    {
        if (totalItems <= 0) return;
        SelectedIndex = (SelectedIndex + direction + totalItems) % totalItems;
    }

    public void Reset()
    {
        SelectedIndex = 0;
    }
}
