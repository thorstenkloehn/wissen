using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Wissen.Web.Areas.Identity;

// Beantwortet eine Identity-Seite mit 404. Program.cs hängt ihn an die Seiten, die ohne E-Mail-Versand
// nichts bewirken, und an die Registrierungsseiten, solange "RegistrierungErlaubt" nicht gesetzt ist.
public class SeiteGesperrtFilter : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
