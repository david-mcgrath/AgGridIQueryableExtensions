using System;
using System.Collections.Generic;
using System.Text;

namespace AgGridIQueryableExtensions.Model
{
    public enum AgGridDataType
    {
        Text,
        Number,
        Date,
        Boolean,
        Set,
    }
    public enum AgGridFilter
    {
        Equals,
        NotEqual,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        InRange,
        Null,
        NotNull,
    }
    public enum AgGridFilterBooleanOperator
    {
        AND,
        OR
    }
    public enum AgGridSort
    {
        Asc,
        Desc,
    }
}
