namespace Pulumi.Dungeon;

public static class ValidationExtensions
{
    public static IRuleBuilderInitialCollection<IEnumerable<KeyValuePair<string, TValue>>, KeyValuePair<string, TValue>> KeyNameIndexer<TValue>(this IRuleBuilderInitialCollection<IEnumerable<KeyValuePair<string, TValue>>, KeyValuePair<string, TValue>> rule)
    {
        rule.OverrideIndexer((_, _, element, _) =>
        {
            var asProperty = Regex.IsMatch(element.Key, @"^[A-Z][A-Za-z]*$");
            return asProperty ? $".{element.Key}" : $"['{element.Key}']";
        });
        return rule;
    }

    public static IRuleBuilderInitial<KeyValuePair<string, TValue>, TProperty> RuleFor<TValue, TProperty>(this InlineValidator<KeyValuePair<string, TValue>> validator, Expression<Func<TValue, TProperty>> expression) =>
        validator.RuleFor(ChainProperties<KeyValuePair<string, TValue>, TValue, TProperty>(entry => entry.Value, expression))
            .Configure(config => config.PropertyName = config.Member.Name);

    public static ValidationResult WithPropertyChain(this ValidationResult result)
    {
        foreach (var error in result.Errors)
        {
            // replace split pascal case display name with fully chained property name
            var displayName = error.FormattedMessagePlaceholderValues["PropertyName"];
            error.ErrorMessage = error.ErrorMessage.Replace($"'{displayName}'", error.PropertyName); // strip quotes
        }
        return result;
    }

    private static Expression<Func<A, C>> ChainProperties<A, B, C>(Expression<Func<A, B>> inner, Expression<Func<B, C>> outer) =>
        Expression.Lambda<Func<A, C>>(ExpressionHelpers.ReplaceExpressions(outer.Body, outer.Parameters[0], inner.Body), inner.Parameters[0]);
}
