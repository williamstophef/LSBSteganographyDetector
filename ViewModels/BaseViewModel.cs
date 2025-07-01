using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LSBSteganographyDetector.ViewModels
{
    /// <summary>
    /// Base ViewModel providing common MVVM functionality
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        private bool _isBusy;
        private string _title = string.Empty;

        /// <summary>
        /// Indicates if the ViewModel is currently performing an operation
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Indicates if the ViewModel is not busy (inverse of IsBusy)
        /// </summary>
        public bool IsNotBusy => !IsBusy;

        /// <summary>
        /// Title for the page/view
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Sets the property value and raises PropertyChanged if the value changed
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="backingStore">Reference to the backing field</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Property name (automatically provided)</param>
        /// <returns>True if the value changed, false otherwise</returns>
        protected virtual bool SetProperty<T>(ref T backingStore, T value,
            [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            
            // Also raise IsNotBusy when IsBusy changes
            if (propertyName == nameof(IsBusy))
                OnPropertyChanged(nameof(IsNotBusy));
                
            return true;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
} 