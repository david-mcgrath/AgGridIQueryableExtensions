using System;
using System.Collections.Generic;
using System.Text;

namespace AgGridIQueryableExtensions.Model
{
    public class AgGridFilterModel
    {
        public AgGridDataType FilterType { get; set; }
        public AgGridFilter Type { get; set; }
        public object Filter { get; set; } // This should match the data type of the column, which is unknown...
        public double? FilterTo { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string[] Values { get; set; }
        public AgGridFilterBooleanOperator? Operator { get; set; }
        public AgGridFilterModel Condition1 { get; set; }
        public AgGridFilterModel Condition2 { get; set; }
    }
}
