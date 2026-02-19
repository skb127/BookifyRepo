using System.Collections.Concurrent;
using Bookify.Application.Abstractions.Email;
using Microsoft.Extensions.Options;
using Scriban;
using Scriban.Runtime;

namespace Bookify.Infrastructure.Email;

public class ScribanTemplateService : IEmailTemplateService
{
    // Thread-Safe Cache (Static or Singleton to persist in memory)
    // save the already parsed Template so we don't have to read the disk every time
    private static readonly ConcurrentDictionary<string, Template> _templateCache = new();

    private readonly EmailOptions _emailOptions;
    private readonly string _templatesPath;

    public ScribanTemplateService(IOptions<EmailOptions> emailOptions)
    {
        _emailOptions = emailOptions.Value;
        _templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmailTemplates");
    }

    public async Task<string> GenerateEmailBodyAsync(string templateName,
        object model,
        CancellationToken cancellationToken = default)
    {
        // Try to get the template from the cache
        if (_templateCache.TryGetValue(templateName, out Template? template))
        {
            return await RenderTemplateAsync(template, model);
        }

        // If the template is not in the cache, we load it from the disk.
        // ConcurrentDictionary will handle the final write safely.
        string filePath = Path.Combine(_templatesPath, templateName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template not found: {filePath}");
        }

        // Read the physical file
        string templateText = await File.ReadAllTextAsync(filePath, cancellationToken);

        // Scriban parses the text
        template = Template.Parse(templateText);

        if (template.HasErrors)
        {
            throw new InvalidOperationException($"Template error {templateName}: {template.Messages}");
        }

        // Save in cache for next time
        _templateCache.TryAdd(templateName, template);

        // Render the template with the data (The model)
        return await RenderTemplateAsync(template, model);
    }

    private async Task<string> RenderTemplateAsync(Template template, object model)
    {
        var scriptObject = new ScriptObject();

        // Import the model properties into the script object
        // This allows using {{ FirstName }} directly instead of {{ model.FirstName }}
        scriptObject.Import(model, renamer: member => member.Name);

        scriptObject.Add("SupportEmail", _emailOptions.SupportEmail);

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return await template.RenderAsync(context);
    }
}
