namespace Pulumi.Dungeon;

public static class ExpressionHelpers
{
    public static T ReplaceExpressions<T>(T expression, Expression from, Expression to) where T : Expression =>
        new ExpressionReplacer { From = from, To = to }.VisitAndConvert(expression, nameof(ReplaceExpressions));

    private sealed class ExpressionReplacer : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node) => node != null && node == From ? To : base.Visit(node);

        public Expression From { get; init; } = null!;
        public Expression To { get; init; } = null!;
    }
}
