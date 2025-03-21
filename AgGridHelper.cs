using AgGridIQueryableExtensions.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace AgGridIQueryableExtensions
{
    public static class AgGridHelper
    {
        public static IQueryable<T> ApplyAgGridRequest<T>(this IQueryable<T> query, AgGridRequest options)
        {
            // Ignore skip, take, and sort for now
            // Just confirm if I can make filter work

            return query
                .ApplyAgGridRequestFilter(options.FilterModel)
                .ApplyAgGridRequestSort(options.SortModel)
                .ApplyAgGridRequestStartEnd(options)
                ;
        }
        public static IQueryable<T> ApplyAgGridRequestFilter<T>(this IQueryable<T> query, string propertyName, AgGridFilterModel filter)
            => ApplyAgGridRequestFilter(query, new Dictionary<string, AgGridFilterModel> { [propertyName] = filter });
        public static IQueryable<T> ApplyAgGridRequestFilter<T>(this IQueryable<T> query, IDictionary<string, AgGridFilterModel> filter)
        {
            if (filter == null || filter.Count == 0)
                return query;

            foreach (var kvp in filter)
            {
                var predicate = GetPredicate<T>(kvp.Key, kvp.Value, false); // TODO: include the case insensitive option, I assume it's in the base object
                query = query.Where(predicate);
            }

            return query;
        }

        public static IQueryable<T> ApplyAgGridRequestSort<T>(this IQueryable<T> query, AgGridSortModel sort)
            => ApplyAgGridRequestSort(query, new AgGridSortModel[] { sort });
        public static IQueryable<T> ApplyAgGridRequestSort<T>(this IQueryable<T> query, IEnumerable<AgGridSortModel> sort)
        {
            IOrderedQueryable<T> ordered = null;
            foreach (var sortItem in sort)
            {
                string propertyName = sortItem.ColId;
                PropertyInfo propertyInfo = typeof(T).GetProperty(propertyName);

                if (propertyInfo == null)
                    throw new ArgumentException($"{propertyName} not found for sort");
                if (!Enum.IsDefined(typeof(AgGridSort), sortItem.Sort))
                    throw new ArgumentException($"Unrecognised sort direction for sort {propertyName}");

                ParameterExpression param = Expression.Parameter(typeof(T));
                Expression property = Expression.Property(param, propertyInfo);
                Expression<Func<T, object>> lambda = Expression.Lambda<Func<T, object>>(property, param);

                ordered = sortItem.Sort == AgGridSort.Desc
                    ? ordered == null
                        ? query.OrderByDescending(lambda)
                        : ordered.ThenByDescending(lambda)
                    : ordered == null
                        ? query.OrderBy(lambda)
                        : ordered.ThenBy(lambda);
            }

            return ordered ?? query;
        }

        public static IQueryable<T> ApplyAgGridRequestStartEnd<T>(this IQueryable<T> query, AgGridRequest request)
            => ApplyAgGridRequestStartEnd(query, request.StartRow, request.EndRow);
        public static IQueryable<T> ApplyAgGridRequestStartEnd<T>(this IQueryable<T> query, int? startRow, int? endRow)
        {
            if (startRow.HasValue)
                query = query.Skip(startRow.Value);
            if (endRow.HasValue)
                query = query.Take(endRow.Value + 1 - startRow ?? 0);
            return query;
        }

        private static Expression<Func<T, bool>> GetPredicate<T>(string colName, AgGridFilterModel filter, bool caseInsensitive)
        {
            var entityType = typeof(T);
            PropertyInfo propertyInfo = entityType.GetProperty(colName);

            // Ensure arguments are value
            if (propertyInfo == null)
                throw new ArgumentException($"Could not find property {colName} in entity {entityType.Name}");

            ParameterExpression param = Expression.Parameter(entityType);
            Expression prop = Expression.Property(param, propertyInfo);

            var expression = GetExpression<T>(colName, filter, caseInsensitive, param, prop, propertyInfo);
            var predicate = Expression.Lambda<Func<T, bool>>(expression, param);

            return predicate;
        }

        private static Expression GetExpression<T>(string colName, AgGridFilterModel filter, bool caseInsensitive, ParameterExpression parameter, Expression property, PropertyInfo propertyInfo)
        {
            // AND / OR combine child expressions
            if (filter.Operator.HasValue)
            {
                if (filter.Condition1 == null)
                    throw new ArgumentException($"No {nameof(filter.Condition1)} found for column {colName}");
                if (filter.Condition2 == null)
                    throw new ArgumentException($"No {nameof(filter.Condition2)} found for column {colName}");

                var cond1 = GetExpression<T>(colName, filter.Condition1, caseInsensitive, parameter, property, propertyInfo);
                var cond2 = GetExpression<T>(colName, filter.Condition2, caseInsensitive, parameter, property, propertyInfo);

                switch (filter.Operator.Value)
                {
                    case AgGridFilterBooleanOperator.AND:
                        return Expression.And(cond1, cond2);
                    case AgGridFilterBooleanOperator.OR:
                        return Expression.Or(cond1, cond2);
                }
            }

            // Ensure arguments are value
            if (!Enum.IsDefined(typeof(AgGridFilter), filter.Type))
                throw new ArgumentException($"Unrecognised filter for property {colName}");
            if (!Enum.IsDefined(typeof(AgGridDataType), filter.FilterType))
                throw new ArgumentException($"Unrecognised filter type for property {colName}");

            // TODO: Check property type, ensure it matches a valid one for filter data type

            Expression arg0 = Expression.Constant(null);
            Expression arg1 = Expression.Constant(null);
            // TODO: Need to convert, ensure data is the correct types
            if (filter.Type != AgGridFilter.Null && filter.Type != AgGridFilter.NotNull)
            {
                switch (filter.FilterType)
                {
                    case AgGridDataType.Text:
                        arg0 = Expression.Constant(caseInsensitive ? filter.Filter?.ToString().ToLower() : filter.Filter?.ToString());
                        break;
                    case AgGridDataType.Number:
                        arg0 = Expression.Constant(filter.Filter);
                        if (filter.Type == AgGridFilter.InRange)
                            arg1 = Expression.Constant(filter.FilterTo);
                        break;
                    case AgGridDataType.Date:
                        arg0 = Expression.Constant(filter.DateFrom);
                        if (filter.Type == AgGridFilter.InRange)
                            arg1 = Expression.Constant(filter.DateTo);
                        break;
                    case AgGridDataType.Boolean:
                        arg0 = Expression.Constant(filter.Filter);
                        break;
                    case AgGridDataType.Set:
                        arg0 = caseInsensitive
                            ? Expression.Constant((filter.Values ?? Array.Empty<string>()).Select(v => v?.ToLower()).ToArray())
                            : Expression.Constant(filter.Values ?? Array.Empty<string>());
                        break;
                    default:
                        throw new ArgumentException($"Unrecognised filter type for property {colName}");
                }
            }

            // If case insensitive, should .ToLower() first
            if ((filter.FilterType == AgGridDataType.Text || filter.FilterType == AgGridDataType.Set) && caseInsensitive)
            {
                var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower));
                property = Expression.Call(property, toLowerMethod);
            }

            // Construct the predicate
            Expression result = Expression.Constant(true);
            var containsMethod = typeof(string[]).GetMethod("Contains"); // why wasn't nameof working here?
            // These have an ambigous match, need to figure out what to do here
            var startsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), new Type[] { typeof(string) });
            var endsWithMethod = typeof(string).GetMethod(nameof(string.EndsWith), new Type[] { typeof(string) });
            switch (filter.Type)
            {
                case AgGridFilter.Null:
                    result = Expression.Equal(property, Expression.Constant(null, propertyInfo.GetType()));
                    break;
                case AgGridFilter.NotNull:
                    result = Expression.NotEqual(property, Expression.Constant(null, propertyInfo.GetType()));
                    break;
                case AgGridFilter.Equals:
                    result = Expression.Equal(property, arg0);
                    break;
                case AgGridFilter.NotEqual:
                    result = Expression.NotEqual(property, arg0);
                    break;
                case AgGridFilter.Contains:
                    result = Expression.Call(arg0, containsMethod, property);
                    break;
                case AgGridFilter.NotContains:
                    result = Expression.Not(Expression.Call(arg0, containsMethod, property));
                    break;
                case AgGridFilter.StartsWith:
                    result = Expression.Call(property, startsWithMethod, arg0);
                    break;
                case AgGridFilter.EndsWith:
                    result = Expression.Call(property, endsWithMethod, arg0);
                    break;
                case AgGridFilter.LessThan:
                    result = Expression.LessThan(property, arg0);
                    break;
                case AgGridFilter.LessThanOrEqual:
                    result = Expression.LessThanOrEqual(property, arg0);
                    break;
                case AgGridFilter.GreaterThan:
                    result = Expression.GreaterThan(property, arg0);
                    break;
                case AgGridFilter.GreaterThanOrEqual:
                    result = Expression.GreaterThanOrEqual(property, arg0);
                    break;
                case AgGridFilter.InRange:
                    result = Expression.And(
                        Expression.GreaterThanOrEqual(property, arg0),
                        Expression.LessThanOrEqual(property, arg1)
                    );
                    break;
                default:
                    throw new ArgumentException($"Unrecognised filter for property {colName}");
            }

            return result;
        }
    }
}
