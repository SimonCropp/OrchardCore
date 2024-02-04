using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Taxonomies.Models;
using OrchardCore.Taxonomies.ViewModels;

namespace OrchardCore.Taxonomies.Drivers
{
    public class TaxonomyPartDisplayDriver : ContentPartDisplayDriver<TaxonomyPart>
    {
        private readonly IStringLocalizer S;

        public TaxonomyPartDisplayDriver(IStringLocalizer<TaxonomyPartDisplayDriver> stringLocalizer)
        {
            S = stringLocalizer;
        }

        public override IDisplayResult Display(TaxonomyPart part, BuildPartDisplayContext context)
        {
            var hasItems = part.Terms.Any();
            return Initialize<TaxonomyPartViewModel>(hasItems ? "TaxonomyPart" : "TaxonomyPart_Empty", m =>
            {
                m.ContentItem = part.ContentItem;
                m.TaxonomyPart = part;
            })
            .Location("Detail", "Content");
        }

        public override IDisplayResult Edit(TaxonomyPart part)
        {
            return Initialize<TaxonomyPartEditViewModel>("TaxonomyPart_Edit", model =>
            {
                model.TermContentType = part.TermContentType;
                model.TaxonomyPart = part;
            });
        }

        public override async Task<IDisplayResult> UpdateAsync(TaxonomyPart part, IUpdateModel updater)
        {
            var model = new TaxonomyPartEditViewModel();

            if (await updater.TryUpdateModelAsync(model, Prefix, t => t.Hierarchy, t => t.TermContentType))
            {
                if (string.IsNullOrWhiteSpace(model.TermContentType))
                {
                    updater.ModelState.AddModelError(Prefix, nameof(model.TermContentType), S["The Term Content Type field is required."]);
                }

                if (!string.IsNullOrWhiteSpace(model.Hierarchy))
                {
                    var originalTaxonomyItems = part.ContentItem.As<TaxonomyPart>();

                    var newHierarchy = JsonNode.Parse(model.Hierarchy).AsArray();

                    var taxonomyItems = new JsonArray();

                    foreach (var item in newHierarchy)
                    {
                        taxonomyItems.Add(ProcessItem(originalTaxonomyItems, item as JsonObject));
                    }

                    part.Terms = taxonomyItems.ToObject<List<ContentItem>>();
                }

                part.TermContentType = model.TermContentType;
            }

            return Edit(part);
        }

        /// <summary>
        /// Clone the content items at the specific index.
        /// </summary>
        private static JsonObject GetTaxonomyItemAt(List<ContentItem> taxonomyItems, int[] indexes)
        {
            ContentItem taxonomyItem = null;

            // Seek the term represented by the list of indexes
            foreach (var index in indexes)
            {
                if (taxonomyItems == null || taxonomyItems.Count < index)
                {
                    // Trying to access an unknown index
                    return null;
                }

                taxonomyItem = taxonomyItems[index];

                var terms = (JsonArray)taxonomyItem.Content["Terms"];
                taxonomyItems = terms?.ToObject<List<ContentItem>>();
            }

            var newObj = JObject.FromObject(taxonomyItem);

            if (newObj["Terms"] != null)
            {
                newObj["Terms"] = new JsonArray();
            }

            return newObj;
        }

        private JsonObject ProcessItem(TaxonomyPart originalItems, JsonObject item)
        {
            var contentItem = GetTaxonomyItemAt(originalItems.Terms, item["index"].ToString().Split('-').Select(x => Convert.ToInt32(x)).ToArray());

            if (item["children"] is JsonArray children)
            {
                var taxonomyItems = new JsonArray();

                for (var i = 0; i < children.Count; i++)
                {
                    taxonomyItems.Add(ProcessItem(originalItems, children[i] as JsonObject));
                    contentItem["Terms"] = taxonomyItems;
                }
            }

            return contentItem;
        }
    }
}
