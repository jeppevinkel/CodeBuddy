using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Base implementation of ICodeValidator providing lifecycle management
    /// </summary>
    public abstract class BaseCodeValidator : ICodeValidator
    {
        private ValidatorState _state = ValidatorState.Uninitialized;
        private bool _disposed;

        public ValidatorState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    var oldState = _state;
                    _state = value;
                    OnStateChanged(oldState, value);
                }
            }
        }

        public event EventHandler<ValidatorStateChangedEventArgs> StateChanged;

        public void Initialize()
        {
            ThrowIfDisposed();
            
            if (State != ValidatorState.Uninitialized)
            {
                throw new InvalidOperationException("Validator is already initialized");
            }

            try
            {
                State = ValidatorState.Initializing;
                InitializeCore();
                State = ValidatorState.Ready;
            }
            catch (Exception ex)
            {
                State = ValidatorState.Error;
                throw new InvalidOperationException("Failed to initialize validator", ex);
            }
        }

        /// <summary>
        /// Core initialization logic to be implemented by derived classes
        /// </summary>
        protected abstract void InitializeCore();

        public async Task<ValidationResult> ValidateAsync(string code, string language, ValidationOptions options)
        {
            ThrowIfDisposed();
            
            if (State != ValidatorState.Ready)
            {
                throw new InvalidOperationException("Validator is not in ready state");
            }

            try
            {
                return await ValidateAsyncCore(code, language, options);
            }
            catch (Exception ex)
            {
                State = ValidatorState.Error;
                throw new ValidationException("Validation failed", ex);
            }
        }

        /// <summary>
        /// Core validation logic to be implemented by derived classes
        /// </summary>
        protected abstract Task<ValidationResult> ValidateAsyncCore(string code, string language, ValidationOptions options);

        protected virtual void OnStateChanged(ValidatorState oldState, ValidatorState newState)
        {
            StateChanged?.Invoke(this, new ValidatorStateChangedEventArgs(oldState, newState));
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    State = ValidatorState.Disposing;
                    DisposeManagedResources();
                }
                finally
                {
                    State = ValidatorState.Disposed;
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// Dispose managed resources in derived classes
        /// </summary>
        protected virtual void DisposeManagedResources() { }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string message, Exception innerException = null) 
            : base(message, innerException)
        {
        }
    }
}