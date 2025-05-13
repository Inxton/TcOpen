
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Data;
using TcoCore;
using TcoData;
using TcoData.Helpers;
using TcoData.Models;
using TcOpen.Inxton.Data;
using TcOpen.Inxton.Input;
using Vortex.Connector;

namespace TcoData.Models
{
    /// <summary>
    /// Traverses a PLC entity and maps included inspection objects into a data collection.
    /// </summary>
    /// <typeparam name="TEntity">The PLC entity type (must inherit from PlainTcoEntity).</typeparam>
    /// <typeparam name="TItem">The output item type (must implement IDataSetItemsFad).</typeparam>
    public class BulkTraversalModel
        <TEntity, TItem>
        where TEntity : PlainTcoEntity,new()
        where TItem : IBulkTraversalItem
    {
        private readonly EntityTraversalHelper<TEntity> entityTraversal;
        private readonly Dictionary<Type, Func<string, object, bool>> _includeTypeHandlers = new Dictionary<Type, Func<string, object, bool>>();
        private IRepository<TEntity> _repository;

 
        public DataViewModel<TEntity> DataViewModel { get; set; }
        public RelayCommand WriteDataCommand { get; private set; }
        public RelayCommand UpdateFilterCommand { get; private set; }

        private DataItemValidation[] _validations;

        public ObservableCollection<TItem> ItemCollection { get; set; } = new ObservableCollection<TItem>();
        public ObservableCollection<TItem> TemplateCollection { get; set; } = new ObservableCollection<TItem>();
        public BulkTraversalModel(IRepository<TEntity> repository )
        {

            entityTraversal = new EntityTraversalHelper<TEntity>(new TEntity());
            _includeTypeHandlers = new Dictionary<Type, Func<string, object, bool>>();
            _repository = repository;
            DataViewModel = new DataViewModel<TEntity>(_repository,new TcoDataExchange());
            WriteDataCommand = new TcOpen.Inxton.Input.RelayCommand((a) => ApplyWriteRequests());




        }


        public BulkTraversalModel(IRepository<TEntity> repository, ValidateDataDelegate<TEntity> validatorDelegate)
        {

            entityTraversal = new EntityTraversalHelper<TEntity>(new TEntity());
            _includeTypeHandlers = new Dictionary<Type, Func<string, object, bool>>();
            _repository = repository;
            DataViewModel = new DataViewModel<TEntity>(_repository, new TcoDataExchange());
            WriteDataCommand = new TcOpen.Inxton.Input.RelayCommand((a) => ApplyWriteRequests());
            _repository.OnRecordUpdateValidation = validatorDelegate;

        }



        /// <summary>
        /// Registers a handler for a specific type to determine inclusion logic during traversal.
        /// </summary>
        public void RegisterIncludeHandler<T>(Func<string, T, bool> handler) where T : class
        {
            _includeTypeHandlers[typeof(T)] = (symbol, obj) => handler(symbol, obj as T);
        }

        /// <summary>
        /// Traverses the entity and collects objects matching the registered type handlers.
        /// </summary>
        public void UpdateFromDataTemplate(Func<string, object, TItem> itemFactory)
        {
            var values = entityTraversal.GetAllBasicValues("", new TEntity());

            TemplateCollection.Clear();
            foreach (var (symbol, value) in values)
            {
                var item = itemFactory(symbol, value);
                if (item != null)
                    TemplateCollection.Add(item);
            }

            UpdateList(TemplateCollection);
        }
       

        /// <summary>
        /// Updates the data item collection based on the template data.
        /// </summary>
        public void UpdateList(IEnumerable<TItem> templateData)
        {
            var templateLookup = templateData?.ToDictionary(p => p.Symbol) ?? new Dictionary<string, TItem>();

            foreach (var item in ItemCollection)
            {
                item.Status = templateLookup.ContainsKey(item.Symbol)
                    ? BulkItemStatus.Writable
                    : BulkItemStatus.Deleted;
            }

            var merged = ItemCollection
                .Concat(templateLookup.Values)
                .GroupBy(p => p.Symbol)
                .Select(g => g.First())
                .ToList();

            ItemCollection = new ObservableCollection<TItem>(merged);

            FilteredItems = CollectionViewSource.GetDefaultView(ItemCollection);
            FilteredItems.Filter = FilterByStatusAndSymbol;
        }

        private bool FilterByStatus(object obj)
        {
            if (!(obj is BulkDataItem item)) return false;
            if (SelectedStatusFilter == null) return true; // No filter

            return item.Status == SelectedStatusFilter;
        }
        private bool FilterByStatusAndSymbol(object obj)
        {
            if (!(obj is BulkDataItem item)) return false;

            bool statusMatches = SelectedStatusFilter == null || item.Status == SelectedStatusFilter;
            bool symbolMatches = string.IsNullOrWhiteSpace(SymbolKeywordFilter)
                                 || item.Symbol.IndexOf(SymbolKeywordFilter, StringComparison.OrdinalIgnoreCase) >= 0;

            return statusMatches && symbolMatches;
        }

        public ICollectionView FilteredItems { get; set; }

        private BulkItemStatus? _selectedStatusFilter;
        public BulkItemStatus? SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                _selectedStatusFilter = value;
                FilteredItems.Refresh();
            }
        }
        private string _symbolKeywordFilter;
        public string SymbolKeywordFilter
        {
            get => _symbolKeywordFilter;
            set
            {
                _symbolKeywordFilter = value;
                FilteredItems.Refresh();
            }
        }

        public void  ApplyWriteRequests()
        {
            var writeItems = ItemCollection
                .Where(i => i.WriteStatus == BulkItemWriteStatus.Modified)
                .ToList();

            if (!writeItems.Any())
                return;

           var allEntities = DataViewModel.ObservableRecords.OfType<TEntity>().ToList();  // _repository.GetRecords("*", 100);

            foreach (var entity in allEntities)
            {
                var plain = entity;

                foreach (var item in writeItems)
                {
                    try
                    {
                        SetValueBySymbolPath(plain, item.Symbol, item.Value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to update {item.Symbol}: {ex.Message}");
                    }
                }

                _repository.Update(entity._EntityId,entity);

                

            }

            // Optional: mark them as written
            foreach (var item in writeItems)
            {
                item.WriteStatus = BulkItemWriteStatus.NoChange;
            }
        }
        public static void SetValueBySymbolPath(object root, string symbolPath, object newValue)
        {
            var parts = symbolPath.Split('.');
            object current = root;
            PropertyInfo prop = null;

            for (int i = 0; i < parts.Length; i++)
            {
                prop = current.GetType().GetProperty(parts[i]);
                if (prop == null) return;

                if (i == parts.Length - 1)
                {
                    // Final property – set value
                    var converted = ConvertToPropertyType(newValue, prop.PropertyType);
                    prop.SetValue(current, converted);
                    return;
                }
                else
                {
                    current = prop.GetValue(current);

                    // Structs: reassign after setting inner field
                    if (current == null) return;
                }
            }
        }


        private bool ShouldInclude(string symbol, object obj)
        {
            if (obj == null) return false;

            var objType = obj.GetType();

            var handler = _includeTypeHandlers
                .FirstOrDefault(x => x.Key.IsAssignableFrom(objType)).Value;
            return handler != null && handler(symbol, obj);
        }
        private static object ConvertToPropertyType(object value, Type targetType)
        {
            if (value == null)
            {
                // Optional: Handle null assignment for value types
                if (Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType)
                    return null;

                throw new InvalidCastException($"Cannot assign null to non-nullable type {targetType.Name}");
            }

            var nonNullableTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                if (nonNullableTargetType.IsEnum)
                {
                    if (value is string s)
                        return Enum.Parse(nonNullableTargetType, s, ignoreCase: true);
                    else
                        return Enum.ToObject(nonNullableTargetType, value);
                }

                switch (Type.GetTypeCode(nonNullableTargetType))
                {
                    case TypeCode.Boolean:
                        return Convert.ToBoolean(value);
                    case TypeCode.Byte:
                        return Convert.ToByte(Convert.ToDouble(value));
                    case TypeCode.SByte:
                        return Convert.ToSByte(Convert.ToDouble(value));
                    case TypeCode.Int16:
                        return Convert.ToInt16(Convert.ToDouble(value));
                    case TypeCode.UInt16:
                        return Convert.ToUInt16(Convert.ToDouble(value));
                    case TypeCode.Int32:
                        return Convert.ToInt32(Convert.ToDouble(value));
                    case TypeCode.UInt32:
                        return Convert.ToUInt32(Convert.ToDouble(value));
                    case TypeCode.Int64:
                        return Convert.ToInt64(Convert.ToDouble(value));
                    case TypeCode.UInt64:
                        return Convert.ToUInt64(Convert.ToDouble(value));
                    //case TypeCode.Single:
                    //    return Convert.ToSingle(value);
                    //case TypeCode.Double:
                    //    return Convert.ToDouble(value);
                    //case TypeCode.Decimal:
                    //    return Convert.ToDecimal(value);
                    case TypeCode.Double:
                        return value is string s
                            ? double.Parse(s, CultureInfo.InvariantCulture)
                            : Convert.ToDouble(value);

                    case TypeCode.Single:
                        return value is string s2
                            ? float.Parse(s2, CultureInfo.InvariantCulture)
                            : Convert.ToSingle(value);

                    case TypeCode.Decimal:
                        return value is string s3
                            ? decimal.Parse(s3, CultureInfo.InvariantCulture)
                            : Convert.ToDecimal(value);
                    case TypeCode.String:
                        return Convert.ToString(value);
                    case TypeCode.DateTime:
                        return Convert.ToDateTime(value);
                    default:
                        return System.Convert.ChangeType(value, nonNullableTargetType);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Cannot convert value '{value}' to type {targetType.Name}.", ex);
            }
        }

    }
}
