
using System;
using System.Collections.Generic;
using System.Linq;
using TcOpen.Inxton.Data;
using Vortex.Connector;

namespace TcoData.Helpers
{


    public class EntityTraversalHelper<TEntity>
    where TEntity : PlainTcoEntity, new()
    {
        private readonly TEntity _entity;

        public EntityTraversalHelper(TEntity entity)
        {
            _entity = entity;
        }

        public IEnumerable<(string symbol, object value)> GetAllBasicValues(string symbol, IPlain source)
        {
            var results = new List<(string, object)>();
            Traverse(source, symbol, results);
            return results;
        }

        private void Traverse(object obj, string path, List<(string, object)> results)
        {
            var props = obj.GetType().GetProperties();

            foreach (var prop in props)
            {
                var value = prop.GetValue(obj);
                var subPath = $"{path}.{prop.Name}".Trim('.');

                //if (value == null) continue;

                if (IsBasicType(prop.PropertyType))
                {
                    
                    results.Add((subPath, value));
                }
                else if (value is IPlain nested)
                {
                    Traverse(nested, subPath, results);
                }
                else if (value is Array array)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (array.GetValue(i) is IPlain arrayItem)
                            Traverse(arrayItem, $"{subPath}[{i}]", results);
                    }
                }
            }
        }

        private bool IsBasicType(Type type) =>
            type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
    }

    //    public class EntityTraversalHelper<T> where T : class
    //    {
    //        private T _entity;
    //        private readonly List<IPlain> _plainResults = new List<IPlain>();

    //        public EntityTraversalHelper(T entity)
    //        {
    //            _entity = entity;
    //            _plainResults.Clear();
    //        }

    //        public T Entity
    //        {
    //            get => _entity;
    //            set => _entity = value;
    //        }

    //        public Func<object, bool> Inclusion { get; set; }

    //        public void SearchPlainEntity(string symbol, IPlain sourceObj,  Func<string, object, bool> include = null)
    //        {
    //            foreach (var prop in sourceObj.GetType().GetProperties())
    //            {
    //                var obj = prop.GetValue(sourceObj);
    //                var objName = prop.Name;
    //                var path = $"{symbol}.{objName}";

    //                if (obj == null) continue;

    //                if (include != null && include(path, obj))
    //                {
    //                    if (obj is IPlain plainObj)
    //                        _plainResults.Add(plainObj);
    //                }

    //                if (obj.GetType().IsArray && obj is Array arrayObj)
    //                {
    //                    foreach (var element in arrayObj)
    //                    {
    //                        if (element is IPlain plainElement)
    //                            SearchPlainEntity(path, plainElement, include);
    //                    }
    //                }
    //                else if (obj is IPlain nestedPlain)
    //                {
    //                    SearchPlainEntity(path, nestedPlain, include);
    //                }
    //            }
    //        }


    //        public IReadOnlyList<IPlain> PlainResults => _plainResults.AsReadOnly();
    //    }


}
