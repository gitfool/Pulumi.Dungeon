namespace Pulumi.Dungeon;

public static class ConfigExtensions
{
    public static ValidationResult Validate(this Config config)
    {
        ValidatorOptions.Global.LanguageManager = new ValidationLanguageManager();
        return new ConfigValidator().Validate(config).WithPropertyChain();
    }
}
