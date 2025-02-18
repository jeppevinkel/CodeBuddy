using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CodeBuddy.Core.Implementation.CodeValidation
{
    /// <summary>
    /// Thread-safe implementation of the validator registry
    /// </summary>
    public class ValidatorRegistry : IValidatorRegistrar, IDisposable
    {
        private readonly ConcurrentDictionary<string, ValidatorEntry> _validators = new ConcurrentDictionary<string, ValidatorEntry>();
        private readonly ReaderWriterLockSlim _eventLock = new ReaderWriterLockSlim();
        private bool _isDisposed;

        public event EventHandler<ValidatorRegistrationEventArgs> ValidatorRegistered;
        public event EventHandler<ValidatorRegistrationEventArgs> ValidatorUnregistered;

        public bool RegisterValidator(string languageId, ICodeValidator validator, ValidatorMetadata metadata = null)
        {
            if (string.IsNullOrEmpty(languageId)) throw new ArgumentNullException(nameof(languageId));
            if (validator == null) throw new ArgumentNullException(nameof(validator));

            ValidateValidator(validator);

            var entry = new ValidatorEntry(validator, metadata ?? new ValidatorMetadata());
            
            if (_validators.TryAdd(languageId, entry))
            {
                OnValidatorRegistered(new ValidatorRegistrationEventArgs(languageId, validator, entry.Metadata));
                return true;
            }

            return false;
        }

        public bool UnregisterValidator(string languageId)
        {
            if (string.IsNullOrEmpty(languageId)) throw new ArgumentNullException(nameof(languageId));

            if (_validators.TryRemove(languageId, out var entry))
            {
                OnValidatorUnregistered(new ValidatorRegistrationEventArgs(languageId, entry.Validator, entry.Metadata));
                return true;
            }

            return false;
        }

        public ICodeValidator GetValidator(string languageId)
        {
            return _validators.TryGetValue(languageId, out var entry) ? entry.Validator : null;
        }

        public IReadOnlyDictionary<string, ICodeValidator> GetAllValidators()
        {
            return _validators.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Validator
            );
        }

        public bool HasValidator(string languageId)
        {
            return _validators.ContainsKey(languageId);
        }

        public ValidatorMetadata GetValidatorMetadata(string languageId)
        {
            return _validators.TryGetValue(languageId, out var entry) ? entry.Metadata : null;
        }

        private void ValidateValidator(ICodeValidator validator)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));

            // Ensure the validator implements all required interfaces
            var requiredMethods = new[]
            {
                "ValidateCode",
                "Initialize",
                "Dispose"
            };

            var validatorType = validator.GetType();
            foreach (var method in requiredMethods)
            {
                if (validatorType.GetMethod(method) == null)
                {
                    throw new ArgumentException($"Validator must implement the {method} method");
                }
            }
        }

        protected virtual void OnValidatorRegistered(ValidatorRegistrationEventArgs e)
        {
            _eventLock.EnterReadLock();
            try
            {
                ValidatorRegistered?.Invoke(this, e);
            }
            finally
            {
                _eventLock.ExitReadLock();
            }
        }

        protected virtual void OnValidatorUnregistered(ValidatorRegistrationEventArgs e)
        {
            _eventLock.EnterReadLock();
            try
            {
                ValidatorUnregistered?.Invoke(this, e);
            }
            finally
            {
                _eventLock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            foreach (var entry in _validators.Values)
            {
                (entry.Validator as IDisposable)?.Dispose();
            }

            _validators.Clear();
            _eventLock.Dispose();
        }

        private class ValidatorEntry
        {
            public ICodeValidator Validator { get; }
            public ValidatorMetadata Metadata { get; }

            public ValidatorEntry(ICodeValidator validator, ValidatorMetadata metadata)
            {
                Validator = validator;
                Metadata = metadata;
            }
        }
    }
}