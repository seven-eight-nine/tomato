using System;
using System.Collections.Generic;
using System.Linq;

namespace Tomato.StatusEffectSystem
{
    /// <summary>常にtrue</summary>
    public sealed class AlwaysTrueCondition : ICondition
    {
        public static readonly AlwaysTrueCondition Instance = new();
        private AlwaysTrueCondition() { }

        public bool Evaluate(IConditionContext context) => true;
    }

    /// <summary>常にfalse</summary>
    public sealed class AlwaysFalseCondition : ICondition
    {
        public static readonly AlwaysFalseCondition Instance = new();
        private AlwaysFalseCondition() { }

        public bool Evaluate(IConditionContext context) => false;
    }

    /// <summary>AND条件</summary>
    public sealed class AndCondition : ICondition
    {
        private readonly IReadOnlyList<ICondition> _conditions;

        public AndCondition(params ICondition[] conditions)
        {
            _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
        }

        public AndCondition(IEnumerable<ICondition> conditions)
        {
            _conditions = conditions?.ToArray() ?? throw new ArgumentNullException(nameof(conditions));
        }

        public bool Evaluate(IConditionContext context)
        {
            foreach (var condition in _conditions)
            {
                if (!condition.Evaluate(context))
                    return false;
            }
            return true;
        }
    }

    /// <summary>OR条件</summary>
    public sealed class OrCondition : ICondition
    {
        private readonly IReadOnlyList<ICondition> _conditions;

        public OrCondition(params ICondition[] conditions)
        {
            _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
        }

        public OrCondition(IEnumerable<ICondition> conditions)
        {
            _conditions = conditions?.ToArray() ?? throw new ArgumentNullException(nameof(conditions));
        }

        public bool Evaluate(IConditionContext context)
        {
            foreach (var condition in _conditions)
            {
                if (condition.Evaluate(context))
                    return true;
            }
            return false;
        }
    }

    /// <summary>NOT条件</summary>
    public sealed class NotCondition : ICondition
    {
        private readonly ICondition _condition;

        public NotCondition(ICondition condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public bool Evaluate(IConditionContext context) => !_condition.Evaluate(context);
    }

    /// <summary>スタック数条件</summary>
    public sealed class StackCountCondition : ICondition
    {
        private readonly ComparisonOperator _operator;
        private readonly int _threshold;

        public StackCountCondition(ComparisonOperator op, int threshold)
        {
            _operator = op;
            _threshold = threshold;
        }

        public bool Evaluate(IConditionContext context)
        {
            return _operator switch
            {
                ComparisonOperator.Equal => context.CurrentStacks == _threshold,
                ComparisonOperator.NotEqual => context.CurrentStacks != _threshold,
                ComparisonOperator.LessThan => context.CurrentStacks < _threshold,
                ComparisonOperator.LessThanOrEqual => context.CurrentStacks <= _threshold,
                ComparisonOperator.GreaterThan => context.CurrentStacks > _threshold,
                ComparisonOperator.GreaterThanOrEqual => context.CurrentStacks >= _threshold,
                _ => false
            };
        }
    }

    public enum ComparisonOperator
    {
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual
    }
}
