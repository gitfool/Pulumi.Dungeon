using System;
using System.Reflection;
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Pulumi.Dungeon
{
    public static class Scriban
    {
        public static ValueTask<string> RenderAsync(string name, string text, object model) => RenderAsync(name, _ => text, model);

        public static ValueTask<string> RenderAsync(string name, Func<string, string> text, object model)
        {
            static string ToCamelCase(MemberInfo member) => member.Name.ToCamelCase();

            var template = Template.Parse(text(name), name);
            if (template.HasErrors)
            {
                throw new InvalidOperationException($"Template parser returned errors:\n{template.Messages}");
            }

            var context = new TemplateContext
            {
                MemberRenamer = ToCamelCase,
                EnableRelaxedMemberAccess = false,
                StrictVariables = true
            };

            var scriptObject = new ScribanFunctions();
            scriptObject.Import(model, renamer: ToCamelCase);
            context.PushGlobal(scriptObject);

            return template.RenderAsync(context);
        }
    }
}
