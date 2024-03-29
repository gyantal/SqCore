using System;
using System.Linq;
using QuantConnect.Orders;
using System.Collections.Generic;
using System.Collections.Specialized;
using QuantConnect.Parameters;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Responsible for managing the resolution of position groups for an algorithm
    /// </summary>
    public class PositionManager
    {
        private bool _requiresGroupResolution;

        private PositionGroupCollection _groups;
        private readonly SecurityManager _securities;
        private readonly IPositionGroupResolver _resolver;
        private readonly IPositionGroupBuyingPowerModel _defaultModel;

        /// <summary>
        /// Gets the set of currently resolved position groups
        /// </summary>
        public PositionGroupCollection Groups
        {
            get
            {
                ResolvePositionGroups();
                return _groups;
            }
            private set
            {
                _groups = value;
            }
        }

        /// <summary>
        /// Gets whether or not the algorithm is using only default position groups
        /// </summary>
        public bool IsOnlyDefaultGroups => Groups.IsOnlyDefaultGroups;

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionManager"/> class
        /// </summary>
        /// <param name="securities">The algorithm's security manager</param>
        public PositionManager(SecurityManager securities)
        {
            _securities = securities;
            Groups = PositionGroupCollection.Empty;
            _defaultModel = new SecurityPositionGroupBuyingPowerModel();
            // SqCore Change ORIGINAL:
            // _resolver = new CompositePositionGroupResolver(new OptionStrategyPositionGroupResolver(securities),
            //                new SecurityPositionGroupResolver(_defaultModel));
            // SqCore Change NEW:
            if (SqBacktestConfig.SqFastestExecution) // Speed up by ignoring any OptionStrategy processing
                _resolver = new CompositePositionGroupResolver(new SecurityPositionGroupResolver(_defaultModel));
            else
                _resolver = new CompositePositionGroupResolver(new OptionStrategyPositionGroupResolver(securities),
                    new SecurityPositionGroupResolver(_defaultModel));
            // SqCore Change END

            // we must be notified each time our holdings change, so each time a security is added, we
            // want to bind to its SecurityHolding.QuantityChanged event so we can trigger the resolver

            securities.CollectionChanged += (sender, args) =>
            {
                var items = args.NewItems ?? new List<object>();
                if (args.OldItems != null)
                {
                    foreach (var item in args.OldItems)
                    {
                        items.Add(item);
                    }
                }

                foreach (Security security in items)
                {
                    if (args.Action == NotifyCollectionChangedAction.Add)
                    {
                        security.Holdings.QuantityChanged += HoldingsOnQuantityChanged;
                        if (security.Invested)
                        {
                            // if this security has holdings then we'll need to resolve position groups
                            _requiresGroupResolution = true;
                        }
                    }
                    else if (args.Action == NotifyCollectionChangedAction.Remove)
                    {
                        security.Holdings.QuantityChanged -= HoldingsOnQuantityChanged;
                        if (security.Invested)
                        {
                            // only trigger group resolution if we had holdings in the removed security
                            _requiresGroupResolution = true;
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Gets the <see cref="IPositionGroup"/> matching the specified <paramref name="key"/>. If one is not found,
        /// then a new empty position group is returned.
        /// </summary>
        public IPositionGroup this[PositionGroupKey key] => Groups[key];

        /// <summary>
        /// Creates a position group for the specified order, pulling
        /// </summary>
        /// <param name="order">The order</param>
        /// <returns>A new position group matching the provided order</returns>
        public IPositionGroup CreatePositionGroup(Order order)
        {
            IPositionGroup group;
            var newPositions = order.CreatePositions(_securities).ToList();

            // We send new and current positions to try resolve any strategy being executed by multiple orders
            // else the PositionGroup we will get out here will just be the default in those cases
            if (!_resolver.TryGroup(newPositions, Groups, out group))
            {
                throw new InvalidOperationException($"Unable to create group for order: {order.Id}");
            }

            return group;
        }

        /// <summary>
        /// Resolves position groups using the specified collection of positions
        /// </summary>
        /// <param name="positions">The positions to be grouped</param>
        /// <returns>A collection of position groups containing all of the provided positions</returns>
        public PositionGroupCollection ResolvePositionGroups(PositionCollection positions)
        {
            return _resolver.Resolve(positions);
        }

        /// <summary>
        /// Determines which position groups could be impacted by changes in the specified positions
        /// </summary>
        /// <param name="positions">The positions to be changed</param>
        /// <returns>All position groups that need to be re-evaluated due to changes in the positions</returns>
        public IEnumerable<IPositionGroup> GetImpactedGroups(IReadOnlyCollection<IPosition> positions)
        {
            return _resolver.GetImpactedGroups(Groups, positions);
        }

        /// <summary>
        /// Creates a <see cref="PositionGroupKey"/> for the security's default position group
        /// </summary>
        public PositionGroupKey CreateDefaultKey(Security security)
        {
            return new PositionGroupKey(_defaultModel, security);
        }

        /// <summary>
        /// Gets or creates the default position group for the specified <paramref name="security"/>
        /// </summary>
        /// <remarks>
        /// TODO: position group used here is the default, is this what callers want?
        /// </remarks>
        public IPositionGroup GetOrCreateDefaultGroup(Security security)
        {
            var key = CreateDefaultKey(security);
            return Groups[key];
        }

        private void HoldingsOnQuantityChanged(object sender, SecurityHoldingQuantityChangedEventArgs e)
        {
            _requiresGroupResolution = true;
        }

        /// <summary>
        /// Resolves the algorithm's position groups from all of its holdings
        /// </summary>
        private void ResolvePositionGroups()
        {
            if (_requiresGroupResolution)
            {
                _requiresGroupResolution = false;
                // TODO : Replace w/ special IPosition impl to always equal security.Quantity and we'll
                // use them explicitly for resolution collection so we don't do this each time
                var investedPositions = _securities.Where(kvp => kvp.Value.Invested).Select(kvp => (IPosition)new Position(kvp.Value));
                var positionsCollection = new PositionCollection(investedPositions);
                Groups = ResolvePositionGroups(positionsCollection);
            }
        }
    }
}
