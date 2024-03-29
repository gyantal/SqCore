﻿using System;
using System.Collections.Generic;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Provides an implementation of <see cref="IOptionStrategyLegPredicateReferenceValue"/> that represents a constant value.
    /// </summary>
    public class ConstantOptionStrategyLegPredicateReferenceValue<T> : IOptionStrategyLegPredicateReferenceValue
    {
        private readonly T _value;

        /// <summary>
        /// Gets the target of this value
        /// </summary>
        public PredicateTargetValue Target { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstantOptionStrategyLegPredicateReferenceValue{T}"/> class
        /// </summary>
        /// <param name="value">The constant reference value</param>
        /// <param name="target">The value target in relation to the <see cref="OptionPosition"/></param>
        public ConstantOptionStrategyLegPredicateReferenceValue(T value, PredicateTargetValue target)
        {
            _value = value;
            Target = target;
        }

        /// <summary>
        /// Returns the constant value provided at initialization
        /// </summary>
        public object Resolve(IReadOnlyList<OptionPosition> legs)
        {
            return _value;
        }
    }

    /// <summary>
    /// Provides methods for easily creating instances of <see cref="ConstantOptionStrategyLegPredicateReferenceValue{T}"/>
    /// </summary>
    public static class ConstantOptionStrategyLegReferenceValue
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ConstantOptionStrategyLegPredicateReferenceValue{T}"/> class for
        /// the specified <paramref name="value"/>
        /// </summary>
        public static IOptionStrategyLegPredicateReferenceValue Create(object value)
        {
            if (value is DateTime)
            {
                return new ConstantOptionStrategyLegPredicateReferenceValue<DateTime>((DateTime) value, PredicateTargetValue.Expiration);
            }

            if (value is decimal)
            {
                return new ConstantOptionStrategyLegPredicateReferenceValue<decimal>((decimal) value, PredicateTargetValue.Strike);
            }

            if (value is OptionRight)
            {
                return new ConstantOptionStrategyLegPredicateReferenceValue<OptionRight>((OptionRight) value, PredicateTargetValue.Right);
            }

            throw new NotSupportedException($"{value?.GetType().GetBetterTypeName()} is not supported.");
        }
    }
}