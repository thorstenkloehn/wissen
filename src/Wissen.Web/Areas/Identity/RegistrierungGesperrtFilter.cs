using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Wissen.Web.Areas.Identity;

// Beantwortet die Registrierungsseiten mit 404, solange "RegistrierungErlaubt" nicht gesetzt ist.
public class RegistrierungGesperrtFilter : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        context.Result = new NotFoundResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
