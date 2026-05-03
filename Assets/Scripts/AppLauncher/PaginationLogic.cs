public interface IPaginationLogic
{
    int ItemsPerPage { get; }
    int CurrentPage { get; set; }
    int GetPageCount(int totalItems);
    (int start, int end) GetPageRange(int totalItems, int pageIndex);
    bool CanChangePage(int direction, int totalItems);
}

public class PaginationLogic : IPaginationLogic
{
    public int ItemsPerPage { get; }
    public int CurrentPage { get; set; }

    public PaginationLogic(int itemsPerPage)
    {
        ItemsPerPage = itemsPerPage;
    }

    public int GetPageCount(int totalItems)
    {
        if (totalItems <= 0) return 1;
        return (totalItems + ItemsPerPage - 1) / ItemsPerPage;
    }

    public (int start, int end) GetPageRange(int totalItems, int pageIndex)
    {
        int start = pageIndex * ItemsPerPage;
        int end = System.Math.Min(start + ItemsPerPage, totalItems);
        return (start, end);
    }

    public bool CanChangePage(int direction, int totalItems)
    {
        int targetPage = CurrentPage + direction;
        return targetPage >= 0 && targetPage < GetPageCount(totalItems);
    }
}
