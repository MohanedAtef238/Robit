using System;
using System.Collections.Generic;

namespace Robit.Logic
{
    public interface IPaginationLogic
    {
        int ItemsPerPage { get; }
        int CurrentPage { get; set; }
        int GetPageCount(int totalItems);
        (int startIndex, int endIndex) GetPageRange(int totalItems, int pageIndex);
        bool CanChangePage(int delta, int totalItems);
    }

    public class PaginationLogic : IPaginationLogic
    {
        public int ItemsPerPage { get; }

        private int _currentPage;
        public int CurrentPage
        {
            get => _currentPage;
            set => _currentPage = value;
        }

        public PaginationLogic(int itemsPerPage)
        {
            if (itemsPerPage <= 0) throw new ArgumentException("Items per page must be greater than zero.");
            ItemsPerPage = itemsPerPage;
        }

        public int GetPageCount(int totalItems)
        {
            if (totalItems <= 0) return 1;
            return (int)Math.Ceiling(totalItems / (float)ItemsPerPage);
        }

        public (int startIndex, int endIndex) GetPageRange(int totalItems, int pageIndex)
        {
            if (totalItems <= 0) return (0, 0);
            
            int pageCount = GetPageCount(totalItems);
            int validPage = Math.Clamp(pageIndex, 0, pageCount - 1);
            
            int start = validPage * ItemsPerPage;
            int end = Math.Min(start + ItemsPerPage, totalItems);
            
            return (start, end);
        }

        public bool CanChangePage(int delta, int totalItems)
        {
            int targetPage = _currentPage + delta;
            int pageCount = GetPageCount(totalItems);
            return targetPage >= 0 && targetPage < pageCount;
        }
    }
}
