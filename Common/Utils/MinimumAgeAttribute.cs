using System;
using System.ComponentModel.DataAnnotations;

namespace Common.Utils
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class MinimumAgeAttribute : ValidationAttribute
    {
        private readonly int _minimumAge;

        public MinimumAgeAttribute(int minimumAge)
        {
            _minimumAge = minimumAge;
        }

        public override bool IsValid(object? value)
        {
            if (value == null)
            {
                return true;
            }

            var today = VietnamTime.Today;

            return value switch
            {
                DateOnly dateOnly => IsAtLeastMinimumAge(dateOnly, today),
                DateTime dateTime => IsAtLeastMinimumAge(DateOnly.FromDateTime(dateTime), today),
                _ => false
            };
        }

        private bool IsAtLeastMinimumAge(DateOnly birthDate, DateOnly today)
        {
            var cutoff = today.AddYears(-_minimumAge);
            return birthDate <= cutoff;
        }
    }
}