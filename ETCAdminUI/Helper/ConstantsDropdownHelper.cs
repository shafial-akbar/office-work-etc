using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Etc.Shared.Helpers
{
    public static class ConstantsDropdownHelper
    {
        // Type প্যারামিটার গ্রহণ করবে (যাতে static class পাস করা যায়)
        public static List<SelectListItem> GetSelectList(Type type, string? selectedValue = null)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                             .Where(f => f.IsLiteral && !f.IsInitOnly);

            var items = new List<SelectListItem>();

            foreach (var field in fields)
            {
                var val = field.GetValue(null)?.ToString() ?? string.Empty;
                items.Add(new SelectListItem
                {
                    Text = field.Name,
                    Value = val,
                    Selected = !string.IsNullOrWhiteSpace(selectedValue) &&
                               val.Equals(selectedValue, StringComparison.OrdinalIgnoreCase)
                });
            }

            return items;
        }
    }
}