using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PoultryPro.Models.ViewModels
{
    public static class CommerceModelState
    {
        /// <summary>
        /// Drops validation entries for properties that were not part of the posted form (display-only fields such as
        /// joined names on entities trigger implicit [Required] for non-nullable strings).
        /// </summary>
        public static void KeepPostedOnly(this ModelStateDictionary ms, IFormCollection form)
        {
            foreach (var key in ms.Keys.ToList())
            {
                if (string.IsNullOrEmpty(key)) continue;
                if (!form.ContainsKey(key) && !form.Keys.Any(k => k.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase) || k.StartsWith(key + "[", StringComparison.OrdinalIgnoreCase)))
                    ms.Remove(key);
            }
        }
    }
}
