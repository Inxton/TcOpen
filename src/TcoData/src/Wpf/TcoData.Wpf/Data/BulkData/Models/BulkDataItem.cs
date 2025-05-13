using System;
using System.ComponentModel;

namespace TcoData.Models
{
    /// <summary>
    /// Represents an item of bulk data with symbol, value, and status information.
    /// Supports property change notification and value-based equality.
    /// </summary>
    public class BulkDataItem : INotifyPropertyChanged, IEquatable<BulkDataItem>, IBulkTraversalItem
    {
        private string symbol;
        private string humanReadable;
        private BulkItemStatus status;
        private BulkItemWriteStatus writeStatus;
        private object _value;
        private object _originalValue;

        /// <summary>
        /// Event raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the given property name.
        /// </summary>
        /// <param name="propertyName">Name of the changed property.</param>
        protected void NotifyPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Gets or sets the unique symbol identifier for the item.
        /// </summary>
        public string Symbol
        {
            get => symbol;
            set
            {
                if (symbol == value) return;
                symbol = value;
                NotifyPropertyChange(nameof(Symbol));
            }
        }

        /// <summary>
        /// Gets or sets the human-readable description for the item.
        /// </summary>
        public string HumanReadable
        {
            get => humanReadable;
            set
            {
                if (humanReadable == value) return;
                humanReadable = value;
                NotifyPropertyChange(nameof(HumanReadable));
            }
        }

        /// <summary>
        /// Gets or sets the dynamic value of the item.
        /// </summary>
        public object Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                NotifyPropertyChange(nameof(Value));
                // Automatically mark as modified if the new value differs from the original
                if (!Equals(_value, _originalValue))
                {
                    WriteStatus = BulkItemWriteStatus.Modified;
                }
                else
                {
                    WriteStatus = BulkItemWriteStatus.NoChange; // Or another appropriate default
                }
            }
        }

        /// <summary>
        /// Gets or sets the dynamic value of the item.
        /// </summary>
        public object OriginalValue
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                NotifyPropertyChange(nameof(OriginalValue));
            }
        }

        /// <summary>
        /// Gets or sets the current write status of the item.
        /// </summary>
        public BulkItemWriteStatus WriteStatus
        {
            get => writeStatus;
            set
            {
                if (writeStatus == value) return;
                writeStatus = value;
                NotifyPropertyChange(nameof(WriteStatus));
            }
        }

        /// <summary>
        /// Gets or sets the current processing status of the item.
        /// </summary>
        public BulkItemStatus Status
        {
            get => status;
            set
            {
                if (status == value) return;
                status = value;
                NotifyPropertyChange(nameof(Status));
            }
        }

        /// <summary>
        /// Determines whether the specified item is equal to the current item, based on the Symbol.
        /// </summary>
        public bool Equals(BulkDataItem other)
        {
            if (other == null) return false;
            return Symbol == other.Symbol;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current item.
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as BulkDataItem);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            return Symbol?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Returns a string representation of the item, useful for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"{Symbol} ({HumanReadable}): {Value}, Status: {Status}, WriteStatus: {WriteStatus}";
        }
    }
}
