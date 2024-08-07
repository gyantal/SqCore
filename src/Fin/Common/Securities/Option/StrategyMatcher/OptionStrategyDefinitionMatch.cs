using System;
using System.Linq;
using System.Collections.Generic;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Defines a match of <see cref="OptionPosition"/> to a <see cref="OptionStrategyDefinition"/>
    /// </summary>
    public class OptionStrategyDefinitionMatch : IEquatable<OptionStrategyDefinitionMatch>
    {
        /// <summary>
        /// The <see cref="OptionStrategyDefinition"/> matched
        /// </summary>
        public OptionStrategyDefinition Definition { get; }

        /// <summary>
        /// The number of times the definition is able to match the available positions.
        /// Since definitions are formed at the 'unit' level, such as having 1 contract,
        /// the multiplier defines how many times the definition matched. This multiplier
        /// is used to scale the quantity defined in each leg definition when creating the
        /// <see cref="OptionStrategy"/> objects.
        /// </summary>
        public int Multiplier { get; }

        /// <summary>
        /// The <see cref="OptionStrategyLegDefinitionMatch"/> instances matched to the definition.
        /// </summary>
        public IReadOnlyList<OptionStrategyLegDefinitionMatch> Legs { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionStrategyDefinitionMatch"/> class
        /// </summary>
        public OptionStrategyDefinitionMatch(
            OptionStrategyDefinition definition,
            IReadOnlyList<OptionStrategyLegDefinitionMatch> legs,
            int multiplier
            )
        {
            Legs = legs;
            Multiplier = multiplier;
            Definition = definition;
        }

        /// <summary>
        /// Deducts the matched positions from the specified <paramref name="positions"/> taking into account the multiplier
        /// </summary>
        public OptionPositionCollection RemoveFrom(OptionPositionCollection positions)
        {
            var optionPositions = Legs.Select(leg => leg.CreateOptionPosition(Multiplier));
            if (Definition.UnderlyingLots != 0)
            {
                optionPositions = optionPositions.Concat(new[]
                {
                    new OptionPosition(Legs[0].Position.Symbol.Underlying, Definition.UnderlyingLots * Multiplier)
                });
            }
            return positions.RemoveRange(optionPositions);
        }

        /// <summary>
        /// Creates the <see cref="OptionStrategy"/> instance this match represents
        /// </summary>
        public OptionStrategy CreateStrategy()
        {
            var legs = Legs.Select(leg => leg.CreateOptionStrategyLeg(Multiplier));
            var strategy = new OptionStrategy {
                Name = Definition.Name,
                Underlying = Legs[0].Position.Underlying
            };

            foreach (var optionLeg in legs)
            {
                optionLeg.Invoke(strategy.UnderlyingLegs.Add, strategy.OptionLegs.Add);
            }

            if (Definition.UnderlyingLots != 0)
            {
                strategy.UnderlyingLegs = new List<OptionStrategy.UnderlyingLegData>
                {
                    OptionStrategy.UnderlyingLegData.Create(Definition.UnderlyingLots * Multiplier, Legs[0].Position.Underlying)
                };
            }

            return strategy;
        }

        /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(OptionStrategyDefinitionMatch other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (!Equals(Definition, other.Definition))
            {
                return false;
            }

            // index legs by OptionPosition so we can do the equality while ignoring ordering
            var positions = other.Legs.ToDictionary(leg => leg.Position, leg => leg.Multiplier);
            foreach (var leg in other.Legs)
            {
                int multiplier;
                if (!positions.TryGetValue(leg.Position, out multiplier))
                {
                    return false;
                }

                if (leg.Multiplier != multiplier)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Determines whether the specified object is equal to the current object.</summary>
        /// <param name="obj">The object to compare with the current object. </param>
        /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((OptionStrategyDefinitionMatch) obj);
        }

        /// <summary>Serves as the default hash function. </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                // we want to ensure that the ordering of legs does not impact equality operators in
                // pursuit of this, we compute the hash codes of each leg, placing them into an array
                // and then sort the array. using the sorted array, aggregates the hash codes

                var hashCode = Definition.GetHashCode();
                var arr = new int[Legs.Count];
                for (int i = 0; i < Legs.Count; i++)
                {
                    arr[i] = Legs[i].GetHashCode();
                }

                Array.Sort(arr);

                for (int i = 0; i < arr.Length; i++)
                {
                    hashCode = (hashCode * 397) ^ arr[i];
                }

                return hashCode;
            }
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"{Definition.Name}: {string.Join("|", Legs.Select(leg => leg.Position))}";
        }

        /// <summary>
        /// OptionStrategyDefinitionMatch == Operator
        /// </summary>
        /// <returns>True if they are the same</returns>
        public static bool operator ==(OptionStrategyDefinitionMatch left, OptionStrategyDefinitionMatch right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// OptionStrategyDefinitionMatch != Operator
        /// </summary>
        /// <returns>True if they are not the same</returns>
        public static bool operator !=(OptionStrategyDefinitionMatch left, OptionStrategyDefinitionMatch right)
        {
            return !Equals(left, right);
        }
    }
}
