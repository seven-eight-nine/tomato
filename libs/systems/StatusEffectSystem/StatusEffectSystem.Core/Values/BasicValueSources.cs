using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>定数値</summary>
    public sealed class ConstantValue : IValueSource
    {
        private readonly FixedPoint _value;

        public ConstantValue(FixedPoint value) => _value = value;
        public ConstantValue(int value) => _value = FixedPoint.FromInt(value);
        public ConstantValue(float value) => _value = FixedPoint.FromFloat(value);

        public FixedPoint Evaluate(IEffectContext context) => _value;
    }

    /// <summary>スタック数に応じてスケール</summary>
    public sealed class StackScaledValue : IValueSource
    {
        private readonly FixedPoint _baseValue;
        private readonly FixedPoint _perStackValue;

        public StackScaledValue(FixedPoint baseValue, FixedPoint perStackValue)
        {
            _baseValue = baseValue;
            _perStackValue = perStackValue;
        }

        public FixedPoint Evaluate(IEffectContext context)
        {
            var stacks = FixedPoint.FromInt(context.CurrentStacks);
            return _baseValue + _perStackValue * stacks;
        }
    }

    /// <summary>スナップショット値を参照</summary>
    public sealed class SnapshotValue : IValueSource
    {
        private readonly string _snapshotKey;
        private readonly FixedPoint _fallback;

        public SnapshotValue(string snapshotKey, FixedPoint fallback = default)
        {
            _snapshotKey = snapshotKey ?? throw new ArgumentNullException(nameof(snapshotKey));
            _fallback = fallback;
        }

        public FixedPoint Evaluate(IEffectContext context)
        {
            return context.TryGetSnapshot(_snapshotKey, out var value) ? value : _fallback;
        }
    }

    /// <summary>条件分岐値</summary>
    public sealed class ConditionalValue : IValueSource
    {
        private readonly ICondition _condition;
        private readonly IValueSource _trueValue;
        private readonly IValueSource _falseValue;

        public ConditionalValue(ICondition condition, IValueSource trueValue, IValueSource falseValue)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _trueValue = trueValue ?? throw new ArgumentNullException(nameof(trueValue));
            _falseValue = falseValue ?? throw new ArgumentNullException(nameof(falseValue));
        }

        public FixedPoint Evaluate(IEffectContext context)
        {
            return _condition.Evaluate(context)
                ? _trueValue.Evaluate(context)
                : _falseValue.Evaluate(context);
        }
    }
}
