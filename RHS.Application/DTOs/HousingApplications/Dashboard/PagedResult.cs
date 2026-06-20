using System.Collections.Generic;

namespace RHS.Application.DTOs.HousingApplications.Dashboard
{
    public class PagedResult<T>
    {
        public IReadOnlyCollection<T> Items { get; set; } = new List<T>();

        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }
    }
}
