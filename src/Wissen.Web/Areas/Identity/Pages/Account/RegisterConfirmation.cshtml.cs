// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Wissen.Web.Areas.Identity.Pages.Account
{
    // Zeigt nur einen Hinweis. Die Vorlage hat hier den Bestätigungslink angezeigt; damit konnte
    // jeder Besucher ein Konto selbst bestätigen. Ohne echten E-Mail-Versand schaltet deshalb nur
    // der Betreiber Konten frei (Konsolenanwendung, Befehl konto-anlegen).
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
