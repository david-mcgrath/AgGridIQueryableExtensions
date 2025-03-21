using System;
using System.Collections.Generic;
using System.Text;

namespace AgGridIQueryableExtensions.Model
{
    public class AgGridRequest
    {

        public int? StartRow { get; set; }
        public int? EndRow { get; set; }
        public IEnumerable<AgGridSortModel> SortModel { get; set; }
        public IDictionary<string, AgGridFilterModel> FilterModel { get; set; }
    }
}
