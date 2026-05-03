public enum CyclerState { Tucked, Peeked, Expanded }

public interface ICyclerStateManager
{
    CyclerState CurrentState { get; }
    bool TryTransition(CyclerState newState);
    void Reset();
}

public class CyclerStateManager : ICyclerStateManager
{
    public CyclerState CurrentState { get; private set; } = CyclerState.Tucked;

    public bool TryTransition(CyclerState newState)
    {
        switch (CurrentState)
        {
            case CyclerState.Tucked:
                return newState == CyclerState.Peeked ? SetState(newState) : false;

            case CyclerState.Peeked:
                return (newState == CyclerState.Tucked || newState == CyclerState.Expanded) ? SetState(newState) : false;

            case CyclerState.Expanded:
                return (newState == CyclerState.Tucked || newState == CyclerState.Peeked) ? SetState(newState) : false;

            default:
                return false;
        }
    }

    public void Reset()
    {
        CurrentState = CyclerState.Tucked;
    }

    private bool SetState(CyclerState newState)
    {
        CurrentState = newState;
        return true;
    }
}
