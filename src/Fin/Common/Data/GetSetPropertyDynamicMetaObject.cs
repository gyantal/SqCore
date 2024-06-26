﻿using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace QuantConnect.Data
{
    /// <summary>
    /// Provides an implementation of <see cref="DynamicMetaObject"/> that uses get/set methods to update
    /// values in the dynamic object.
    /// </summary>
    public class GetSetPropertyDynamicMetaObject : DynamicMetaObject
    {
        private readonly MethodInfo _setPropertyMethodInfo;
        private readonly MethodInfo _getPropertyMethodInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:QuantConnect.Data.GetSetPropertyDynamicMetaObject" /> class.
        /// </summary>
        /// <param name="expression">The expression representing this <see cref="T:System.Dynamic.DynamicMetaObject" /></param>
        /// <param name="value">The value represented by the <see cref="T:System.Dynamic.DynamicMetaObject" /></param>
        /// <param name="setPropertyMethodInfo">The set method to use for updating this dynamic object</param>
        /// <param name="getPropertyMethodInfo">The get method to use for updating this dynamic object</param>
        public GetSetPropertyDynamicMetaObject(
            Expression expression,
            object value,
            MethodInfo setPropertyMethodInfo,
            MethodInfo getPropertyMethodInfo
            )
            : base(expression, BindingRestrictions.Empty, value)
        {
            _setPropertyMethodInfo = setPropertyMethodInfo;
            _getPropertyMethodInfo = getPropertyMethodInfo;
        }

        /// <summary>
        /// Performs the binding of the dynamic set member operation.
        /// </summary>
        /// <param name="binder">An instance of the <see cref="T:System.Dynamic.SetMemberBinder" /> that represents the details of the dynamic operation.</param>
        /// <param name="value">The <see cref="T:System.Dynamic.DynamicMetaObject" /> representing the value for the set member operation.</param>
        /// <returns>The new <see cref="T:System.Dynamic.DynamicMetaObject" /> representing the result of the binding.</returns>
        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
        {
            // we need to build up an expression tree that represents accessing our instance
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

            var args = new Expression[]
            {
                // this is the name of the property to set
                Expression.Constant(binder.Name),

                // this is the value
                Expression.Convert(value.Expression, typeof (object))
            };

            // set the 'this' reference
            var self = Expression.Convert(Expression, LimitType);

            var call = Expression.Call(self, _setPropertyMethodInfo, args);

            return new DynamicMetaObject(call, restrictions);
        }

        /// <summary>Performs the binding of the dynamic get member operation.</summary>
        /// <param name="binder">An instance of the <see cref="T:System.Dynamic.GetMemberBinder" /> that represents the details of the dynamic operation.</param>
        /// <returns>The new <see cref="T:System.Dynamic.DynamicMetaObject" /> representing the result of the binding.</returns>
        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            // we need to build up an expression tree that represents accessing our instance
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

            // arguments for 'call'
            var args = new Expression[]
            {
                // this is the name of the property to set
                Expression.Constant(binder.Name)
            };

            // set the 'this' reference
            var self = Expression.Convert(Expression, LimitType);

            var call = Expression.Call(self, _getPropertyMethodInfo, args);

            return new DynamicMetaObject(call, restrictions);
        }
    }
}