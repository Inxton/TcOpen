
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
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
        where TEntity : PlainTcoEntity, new()
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
        public BulkTraversalModel(IRepository<TEntity> repository)
        {

            entityTraversal = new EntityTraversalHelper<TEntity>(new TEntity());
            _includeTypeHandlers = new Dictionary<Type, Func<string, object, bool>>();
            _repository = repository;
            DataViewModel = new DataViewModel<TEntity>(_repository, new TcoDataExchange());
            WriteDataCommand = new TcOpen.Inxton.Input.RelayCommand((a) =>
            {
                ApplyWriteRequests(); if (!string.IsNullOrWhiteSpace(LastValidationLog))
                {
                    MessageBox.Show(LastValidationLog, "Validation Results");
                }
            });




        }



        public BulkTraversalModel(IRepository<TEntity> repository, ValidateDataDelegate<TEntity> validatorDelegate)
        {

            entityTraversal = new EntityTraversalHelper<TEntity>(new TEntity());
            _includeTypeHandlers = new Dictionary<Type, Func<string, object, bool>>();
            _repository = repository;
            DataViewModel = new DataViewModel<TEntity>(_repository, new TcoDataExchange());
            WriteDataCommand = new TcOpen.Inxton.Input.RelayCommand((a) =>
            {
                ApplyWriteRequests(); if (!string.IsNullOrWhiteSpace(LastValidationLog))
                {
                    MessageBox.Show(LastValidationLog, "Validation Results");
                }
            });

            // Register externally supplied validator
            _repository.OnRecordUpdateValidation = validatorDelegate ?? (_ => Array.Empty<DataItemValidation>());

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
                    ? BulkItemStatus.Editable
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
        /// <summary>
        /// Determines if a given item matches the current filter for status and symbol keyword.
        /// </summary>
        private bool FilterByStatusAndSymbol(object obj)
        {
            if (!(obj is BulkDataItem item)) return false;

            bool statusMatches = SelectedStatusFilter == null
                 || SelectedStatusFilter == BulkItemStatus.All
                 || item.Status == SelectedStatusFilter;

            bool symbolMatches = string.IsNullOrWhiteSpace(SymbolKeywordFilter)
                                 || item.Symbol.IndexOf(SymbolKeywordFilter, StringComparison.OrdinalIgnoreCase) >= 0;

            return statusMatches && symbolMatches;
        }

        public ICollectionView FilteredItems { get; set; }

        private BulkItemStatus? _selectedStatusFilter = BulkItemStatus.Editable;
        /// <summary>
        /// The selected status filter used to control which items are visible.
        /// </summary>
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
        /// <summary>
        /// The keyword filter used to filter items by symbol name.
        /// </summary>
        public string SymbolKeywordFilter
        {
            get => _symbolKeywordFilter;
            set
            {
                _symbolKeywordFilter = value;
                FilteredItems.Refresh();
            }
        }

        public bool WriteToAllEntities { get; set; }
        public int Limit { get; set; } = 1000;

        /// <summary>
        /// Latest validation summary log as a plain string.
        /// </summary>
        public string LastValidationLog { get; private set; }

        /// <summary>
        /// Applies modified values to all targeted entities in the repository.
        /// </summary>
        public void ApplyWriteRequests()
        {
            var writeItems = ItemCollection
                .Where(i => i.WriteStatus == BulkItemWriteStatus.Modified)
                .ToList();

            if (!writeItems.Any())
                return;
            // Create a HashSet for fast symbol lookup
            var modifiedSymbols = new HashSet<string>(writeItems.Where(p=>p.WriteStatus == BulkItemWriteStatus.Modified).Select(i => i.Symbol));

            IEnumerable<TEntity> allEntities = new List<TEntity>();
            if (WriteToAllEntities)
                allEntities = _repository.GetRecords("*", Limit);
            else
                allEntities = DataViewModel.ObservableRecords.OfType<TEntity>().ToList();
            foreach (var entity in allEntities)
            {
                var plain = entity;

                foreach (var item in writeItems)
                {
                    try
                    {
                        if (item.Status == BulkItemStatus.Editable)
                        {
                            SetValueBySymbolPath(plain, item.Symbol, item.Value);
                        }
                        else
                            return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to update {item.Symbol}: {ex.Message}");
                    }
                }
                var validations = _repository.OnRecordUpdateValidation?.Invoke(entity);
                if (validations != null)
                {

                    if (validations != null && validations.Any())
                    {
                        LastValidationLog = string.Join(Environment.NewLine, validations
                            .Where(v => v.Failed == true && modifiedSymbols.Any(symbol => v.Error.Contains(symbol)))
                            .Select(v => $"❌ {v.Error}"));
                    }

                    if (validations.Any(v => v.Failed && modifiedSymbols.Any(symbol => v.Error.Contains(symbol))))
                    {
                        return;
                    }
                }
              
                _repository.Update(entity._EntityId, entity);



            }

            // Optional: mark them as written
            foreach (var item in writeItems)
            {
                item.WriteStatus = BulkItemWriteStatus.NoChange;
            }
        }
        /// <summary>
        /// Sets a value on a nested property path using reflection.
        /// </summary>
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

        /// <summary>
        /// Determines if a specific object type should be included in the traversal result.
        /// </summary>
        private bool ShouldInclude(string symbol, object obj)
        {
            if (obj == null) return false;

            var objType = obj.GetType();

            var handler = _includeTypeHandlers
                .FirstOrDefault(x => x.Key.IsAssignableFrom(objType)).Value;
            return handler != null && handler(symbol, obj);
        }

        /// <summary>
        /// Converts a raw value to the correct target property type with culture and enum handling.
        /// </summary>
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
