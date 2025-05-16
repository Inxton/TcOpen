using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;


namespace TcoData.Helpers
{
    using System;
    using System.Linq.Expressions;

    public static class SymbolHelper
    {
        public static string GetFullPath<T>(Expression<Func<T>> expr)
        {
            var memberExpr = expr.Body as MemberExpression;
            if (memberExpr == null)
                throw new ArgumentException("Expression must be a MemberExpression");

            var parts = new System.Collections.Generic.List<string>();
            while (memberExpr != null)
            {
                parts.Insert(0, memberExpr.Member.Name);
                memberExpr = memberExpr.Expression as MemberExpression;
            }

            return string.Join(".", parts);
        }
    }

    public static class PropertyHelper
    {
        public static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> property)
        {
            PropertyInfo propertyInfo = null;
            var body = property.Body;

            if (body is MemberExpression)
            {
                propertyInfo = (body as MemberExpression).Member as PropertyInfo;
            }
            else if (body is UnaryExpression)
            {
                propertyInfo = ((MemberExpression)((UnaryExpression)body).Operand).Member as PropertyInfo;
            }

            if (propertyInfo == null)
            {
                throw new ArgumentException("The lambda expression 'property' should point to a valid Property");
            }

            var propertyName = propertyInfo.Name;

            return propertyName;
        }

        public static string GetPropertyName<T>(Expression<Func<T, object>> property)
        {
            PropertyInfo propertyInfo = null;
            var body = property.Body;

            if (body is MemberExpression)
            {
                propertyInfo = (body as MemberExpression).Member as PropertyInfo;
            }
            else if (body is UnaryExpression)
            {
                propertyInfo = ((MemberExpression)((UnaryExpression)body).Operand).Member as PropertyInfo;
            }

            if (propertyInfo == null)
            {
                throw new ArgumentException("The lambda expression 'property' should point to a valid Property");
            }

            var propertyName = propertyInfo.Name;

            return propertyName;
        }

        public static List<string> GetPropertiesNames<T>(T obj, params Expression<Func<T, object>>[] expressions)
        {
            List<string> memberNames = new List<string>();
            foreach (var cExpression in expressions)
            {
                memberNames.Add(GetPropertyName(cExpression));
            }

            return memberNames;
        }


    }
}
