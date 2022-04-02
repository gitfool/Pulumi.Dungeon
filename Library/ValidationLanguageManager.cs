namespace Pulumi.Dungeon;

public sealed class ValidationLanguageManager : LanguageManager
{
    public ValidationLanguageManager()
    {
        Enabled = false; // disable localization
        AddTranslation("en", "RegularExpressionValidator", "'{PropertyName}' must match regex '{RegularExpression}'.");
    }
}
