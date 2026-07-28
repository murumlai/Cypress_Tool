using System;

namespace Fx3I2cProgrammer.Core.Validation
{
    /// <summary>
    /// Result of validating a single piece of user input or a workflow precondition.
    /// </summary>
    public sealed class ValidationResult
    {
        private ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
        }

        /// <summary>True when the validated value/precondition is acceptable.</summary>
        public bool IsValid { get; }

        /// <summary>Human readable explanation. Empty when <see cref="IsValid"/> is true.</summary>
        public string Message { get; }

        public static ValidationResult Success { get; } = new ValidationResult(true, string.Empty);

        public static ValidationResult Ok() => Success;

        public static ValidationResult Fail(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A failure message is required.", nameof(message));
            }

            return new ValidationResult(false, message);
        }

        public override string ToString() => IsValid ? "OK" : "Invalid: " + Message;
    }
}
