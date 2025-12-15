using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiveStock.Web.Pages;

[Authorize]
public class ChatModel : PageModel
{
    public void OnGet()
    {
    }
}
