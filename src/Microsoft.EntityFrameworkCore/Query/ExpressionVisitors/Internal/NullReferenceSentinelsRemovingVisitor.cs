// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class NullReferenceSentinelsRemovingVisitor : ExpressionVisitorBase
    {
        private bool _processOnlyTopLevel;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public NullReferenceSentinelsRemovingVisitor(bool processOnlyTopLevel = false)
        {
            _processOnlyTopLevel = processOnlyTopLevel;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            var test = _processOnlyTopLevel ? node.Test : Visit(node.Test);
            var ifTrue = _processOnlyTopLevel ? node.IfTrue : Visit(node.IfTrue);
            var ifFalse = _processOnlyTopLevel ? node.IfFalse : Visit(node.IfFalse);
            var updatedResult = node.Update(test, ifTrue, ifFalse);

            var binaryTest = test as BinaryExpression;

            if (binaryTest == null
                || !(binaryTest.NodeType == ExpressionType.Equal
                     || binaryTest.NodeType == ExpressionType.NotEqual))
            {
                return updatedResult;
            }

            var leftConstant = binaryTest.Left as ConstantExpression;
            var isLeftNullConstant = leftConstant != null && leftConstant.Value == null;

            var rightConstant = binaryTest.Right as ConstantExpression;
            var isRightNullConstant = rightConstant != null && rightConstant.Value == null;

            if (isLeftNullConstant == isRightNullConstant)
            {
                return updatedResult;
            }

            if (binaryTest.NodeType == ExpressionType.Equal)
            {
                var ifTrueConstant = ifTrue as ConstantExpression;
                if (ifTrueConstant == null
                    || ifTrueConstant.Value != null)
                {
                    return updatedResult;
                }
            }
            else
            {
                var ifFalseConstant = ifFalse as ConstantExpression;
                if (ifFalseConstant == null
                    || ifFalseConstant.Value != null)
                {
                    return updatedResult;
                }
            }

            var testExpression = isLeftNullConstant ? binaryTest.Right : binaryTest.Left;
            var resultExpression = binaryTest.NodeType == ExpressionType.Equal ? ifFalse : ifTrue;
            if (_processOnlyTopLevel)
            {
                _processOnlyTopLevel = false;
                testExpression = Visit(testExpression);
                _processOnlyTopLevel = true;
            }

            return new NullCheckRemovalTestingVisitor().CanRemoveNullCheck(testExpression.RemoveConvert(), resultExpression)
                ? resultExpression
                : updatedResult;
        }

        private class NullCheckRemovalTestingVisitor : ExpressionVisitorBase
        {
            private bool _canRemoveNullCheck = true;
            private bool _topLevelPropertyOrMethodVisited = false;
            private TestExpressionAnalyzer _testExpressionAnalyzer = new TestExpressionAnalyzer();

            public bool CanRemoveNullCheck(Expression testExpression, Expression resultExpression)
            {
                _testExpressionAnalyzer.Visit(testExpression);

                if (_testExpressionAnalyzer.InvalidExpression || _testExpressionAnalyzer.QuerySource == null)
                {
                    return false;
                }

                Visit(resultExpression);

                return _canRemoveNullCheck
                    && _testExpressionAnalyzer.QuerySource == null
                    && !_testExpressionAnalyzer.PropertyNames.Any()
                    && !_testExpressionAnalyzer.MethodInfos.Any();
            }

            public override Expression Visit(Expression node)
                => _canRemoveNullCheck ? base.Visit(node) : node;

            protected override Expression VisitQuerySourceReference(QuerySourceReferenceExpression expression)
            {
                _canRemoveNullCheck = expression.ReferencedQuerySource == _testExpressionAnalyzer.QuerySource;
                _testExpressionAnalyzer.QuerySource = null;

                return expression;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (_testExpressionAnalyzer.PropertyNames.Any() && _testExpressionAnalyzer.PropertyNames.First() == node.Member.Name)
                {
                    _testExpressionAnalyzer.PropertyNames.RemoveAt(0);
                }
                else
                {
                    if (!_topLevelPropertyOrMethodVisited)
                    {
                        _topLevelPropertyOrMethodVisited = true;
                    }
                    else
                    {
                        _canRemoveNullCheck = false;
                    }
                }

                Visit(node.Expression);

                return node;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (EntityQueryModelVisitor.IsPropertyMethod(node.Method))
                {
                    var propertyNameExpression = (ConstantExpression)node.Arguments[1];
                    if (_testExpressionAnalyzer.PropertyNames.Any() && _testExpressionAnalyzer.PropertyNames.First() == (string)propertyNameExpression.Value)
                    {
                        _testExpressionAnalyzer.PropertyNames.RemoveAt(0);
                    }
                    else
                    {
                        if (!_topLevelPropertyOrMethodVisited)
                        {
                            _topLevelPropertyOrMethodVisited = true;
                        }
                        else
                        {
                            _canRemoveNullCheck = false;
                        }
                    }

                    Visit(node.Arguments[0]);

                    return node;
                }

                if (_testExpressionAnalyzer.MethodInfos.Any() && _testExpressionAnalyzer.MethodInfos.First() == node.Method)
                {
                    _testExpressionAnalyzer.MethodInfos.RemoveAt(0);
                }
                else
                {
                    if (!_topLevelPropertyOrMethodVisited)
                    {
                        _topLevelPropertyOrMethodVisited = true;
                    }
                    else
                    {
                        _canRemoveNullCheck = false;
                    }
                }

                Visit(node.Object);

                return node;
            }

            protected override Expression VisitUnary(UnaryExpression node)
            {
                if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
                {
                    return Visit(node.Operand);
                }
                else
                {
                    _canRemoveNullCheck = true;

                    return node;
                }
            }

            private class TestExpressionAnalyzer : ExpressionVisitorBase
            {
                public TestExpressionAnalyzer()
                {
                    PropertyNames = new List<string>();
                    MethodInfos = new List<MethodInfo>();
                }

                public IQuerySource QuerySource { get; set; }
                public List<string> PropertyNames { get; set; }
                public List<MethodInfo> MethodInfos { get; set; }
                public bool InvalidExpression { get; private set; }

                public override Expression Visit([CanBeNull] Expression node)
                {
                    if (InvalidExpression || !(node is QuerySourceReferenceExpression || node is MemberExpression || node is MethodCallExpression || node is UnaryExpression))
                    {
                        InvalidExpression = true;

                        return node;
                    }

                    return base.Visit(node);
                }

                protected override Expression VisitQuerySourceReference(QuerySourceReferenceExpression expression)
                {
                    if (QuerySource == null)
                    {
                        QuerySource = expression.ReferencedQuerySource;
                    }
                    else
                    {
                        InvalidExpression = true;
                    }

                    return expression;
                }

                protected override Expression VisitMember(MemberExpression node)
                {
                    PropertyNames.Add(node.Member.Name);
                    Visit(node.Expression);

                    return node;
                }

                protected override Expression VisitMethodCall(MethodCallExpression node)
                {
                    if (EntityQueryModelVisitor.IsPropertyMethod(node.Method))
                    {
                        var propertyNameExpression = (ConstantExpression)node.Arguments[1];
                        PropertyNames.Add((string)propertyNameExpression.Value);
                        Visit(node.Arguments[0]);
                    }
                    else
                    {
                        MethodInfos.Add(node.Method);
                        Visit(node.Object);
                    }

                    return node;
                }

                protected override Expression VisitUnary(UnaryExpression node)
                {
                    if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
                    {
                        return Visit(node.Operand);
                    }
                    else
                    {
                        InvalidExpression = true;

                        return node;
                    }
                }
            }
        }
    }
}