using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;

namespace Sdl.Web.Mvc.Html
{
    /// <summary>
    /// <see cref="HtmlHelper"/> extension methods for use in (Razor) Views.
    /// </summary>
    /// <remarks>
    /// These extension methods are available on the built-in <c>@Html</c> object.
    /// For example: <code>@Html.DxaRegions(exclude: "Logo")</code>
    /// </remarks>
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Format a date using the appropriate localization culture
        /// </summary>
        /// <param name="htmlHelper">HtmlHelper</param>
        /// <param name="date">Date to format</param>
        /// <param name="format">Format string (default is "D")</param>
        /// <returns>Formatted date</returns>
        public static string Date(this HtmlHelper htmlHelper, DateTime? date, string format = "D")
        {
            return date != null ? ((DateTime)date).ToString(format, WebRequestContext.Localization.CultureInfo) : null;
        }

        /// <summary>
        /// Show a text representation of the difference between a given date and now
        /// </summary>
        /// <param name="htmlHelper">HtmlHelper</param>
        /// <param name="date">The date to compare with the current date</param>
        /// <param name="format">Format string (default is "D")</param>
        /// <returns>Localized versions of "Today", "Yesterday", "X days ago" (for less than a week ago) or the formatted date</returns>
        public static string DateDiff(this HtmlHelper htmlHelper, DateTime? date, string format = "D")
        {
            if (date != null)
            {
                int dayDiff = (int)(DateTime.Now.Date - ((DateTime)date).Date).TotalDays;
                if (dayDiff <= 0)
                {
                    return htmlHelper.Resource("core.todayText");
                }
                if (dayDiff == 1)
                {
                    return htmlHelper.Resource("core.yesterdayText");
                }
                if (dayDiff <= 7)
                {
                    return String.Format(htmlHelper.Resource("core.xDaysAgoText"), dayDiff);
                }

                return ((DateTime)date).ToString(format, WebRequestContext.Localization.CultureInfo);
            }
            return null;
        }

        /// <summary>
        /// Read a resource value
        /// </summary>
        /// <param name="htmlHelper">HtmlHelper</param>
        /// <param name="resourceName">The resource key (eg core.readMoreText)</param>
        /// <returns>The resource value, or key name if none found</returns>
        public static string Resource(this HtmlHelper htmlHelper, string resourceName)
        {
            return (string)Resource(htmlHelper.ViewContext.HttpContext, resourceName);
        }

        /// <summary>
        /// Read a resource string and format it with parameters
        /// </summary>
        /// <param name="htmlHelper">HtmlHelper</param>
        /// <param name="resourceName">The resource key (eg core.readMoreText)</param>
        /// <param name="parameters">Format parameters</param>
        /// <returns>The formatted resource value, or key name if none found</returns>
        public static string FormatResource(this HtmlHelper htmlHelper, string resourceName, params object[] parameters)
        {
            return string.Format(htmlHelper.Resource(resourceName), parameters);
        }        

        /// <summary>
        /// Read a resource string and format it with parameters
        /// </summary>
        /// <param name="httpContext">The HttpContext</param>
        /// <param name="resourceName">The resource key (eg core.readMoreText)</param>
        /// <param name="parameters">Format parameters</param>
        /// <returns>The formatted resource value, or key name if none found</returns>
        public static object FormatResource(this HttpContextBase httpContext, string resourceName, params object[] parameters)
        {
            return string.Format((string)httpContext.Resource(resourceName), parameters);
        }

        /// <summary>
        /// Read a resource value
        /// </summary>
        /// <param name="httpContext">The HttpContext</param>
        /// <param name="resourceName">The resource key (eg core.readMoreText)</param>
        /// <returns>The resource value, or key name if none found</returns>
        public static object Resource(this HttpContextBase httpContext, string resourceName)
        {
            return httpContext.GetGlobalResourceObject(CultureInfo.CurrentUICulture.ToString(), resourceName);
        }

        /// <summary>
        /// Convert a number into a filesize display value
        /// </summary>
        /// <param name="httpContext">The HttpContext</param>
        /// <param name="sizeInBytes">The file size in bytes</param>
        /// <returns>File size string (eg 132 MB)</returns>
        public static string FriendlyFileSize(this HtmlHelper httpContext, long sizeInBytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            double len = sizeInBytes;
            int order = 0;
            while (len >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                len = len / 1024;
            }

            return $"{Math.Ceiling(len)} {sizes[order]}";
        }

        /// <summary>
        /// Write out a media item with a responsive url
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="media">The media item to write out</param>
        /// <param name="widthFactor">The factor to apply to the width - can be % (eg "100%") or absolute (eg "120")</param>
        /// <param name="aspect">The aspect ratio for the image</param>
        /// <param name="cssClass">Css class to apply to img tag</param>
        /// <param name="containerSize">The size (in grid column units) of the containing element</param>
        /// <returns>Complete media markup with all required attributes</returns>
        public static MvcHtmlString Media(this HtmlHelper helper, MediaItem media, string widthFactor, double aspect, string cssClass = null, int containerSize = 0)
        {
            using (new Tracer(helper, media, widthFactor, aspect, cssClass, containerSize))
            {
                if (media == null)
                {
                    return MvcHtmlString.Empty;
                }

                if (cssClass == null)
                {
                    cssClass = media.HtmlClasses;
                }

                //We read the container size (based on bootstrap grid) from the view bag
                //This means views can be independent of where they are rendered and do not
                //need to know their width
                if (containerSize == 0)
                {
                    containerSize = (int) helper.ViewData[DxaViewDataItems.ContainerSize];
                }

                return new MvcHtmlString(media.ToHtml(widthFactor, aspect, cssClass, containerSize));
            }
        }

        public static MvcHtmlString Media(this HtmlHelper helper, MediaItem media, string widthFactor = null, string cssClass = null)
        {
            return Media(helper, media, widthFactor, SiteConfiguration.MediaHelper.DefaultMediaAspect, cssClass);
        }

        public static MvcHtmlString Media(this HtmlHelper helper, MediaItem media, double aspect, string cssClass = null)
        {
            return Media(helper, media, null, aspect, cssClass);
        }

        #region Region/Entity rendering extension methods
        /// <summary>
        /// Renders a given Entity Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="entity">The Entity to render.</param>
        /// <param name="containerSize">The size (in grid column units) of the containing element.</param>
        /// <returns>The rendered HTML or an empty string if <paramref name="entity"/> is <c>null</c>.</returns>
        public static MvcHtmlString DxaEntity(this HtmlHelper htmlHelper, EntityModel entity, int containerSize = 0)
        {
            if (entity == null)
            {
                return MvcHtmlString.Empty;
            }

            if (containerSize == 0)
            {
                containerSize = SiteConfiguration.MediaHelper.GridSize;
            }

            MvcData mvcData = entity.MvcData;
            using (new Tracer(htmlHelper, entity, containerSize, mvcData))
            {
                if (mvcData == null)
                {
                    throw new DxaException($"Unable to render Entity Model [{entity}], because it has no MVC data.");
                }

                string actionName = mvcData.ActionName ?? SiteConfiguration.GetEntityAction();
                string controllerName = mvcData.ControllerName ?? SiteConfiguration.GetEntityController();
                string controllerAreaName = mvcData.ControllerAreaName ?? SiteConfiguration.GetDefaultModuleName();

                RouteValueDictionary parameters = new RouteValueDictionary();
                
                int parentContainerSize = (DxaViewDataItems.ContainerSize != null) ? (int)htmlHelper.ViewData[DxaViewDataItems.ContainerSize] : 0;
                
                if (parentContainerSize == 0)
                {
                    parentContainerSize = SiteConfiguration.MediaHelper.GridSize;
                }
                parameters["containerSize"] = (containerSize * parentContainerSize) / SiteConfiguration.MediaHelper.GridSize;
                parameters["entity"] = entity;
                parameters["area"] = controllerAreaName;
                if (mvcData.RouteValues != null)
                {
                    foreach (string key in mvcData.RouteValues.Keys)
                    {
                        parameters[key] = mvcData.RouteValues[key];
                    }
                }

                MvcHtmlString result = htmlHelper.Action(actionName, controllerName, parameters);
                // If the Entity is being rendered inside a Region (typical), we don't have to transform the XPM markup attributes here; it will be done in DxaRegion.
                if (!(htmlHelper.ViewData.Model is RegionModel) && WebRequestContext.IsPreview)
                {
                    result = new MvcHtmlString(Markup.TransformXpmMarkupAttributes(result.ToString()));
                }
                return Markup.DecorateMarkup(result, entity);
            }
        }

        /// <summary>
        /// Renders a given Entity Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="entity">The Entity to render.</param>
        /// <param name="viewName">The (qualified) name of the View used to render the entity. This overrides the View set in <see cref="EntityModel.MvcData"/>.</param>
        /// <param name="containerSize">The size (in grid column units) of the containing element.</param>
        /// <returns>The rendered HTML or an empty string if <paramref name="entity"/> is <c>null</c>.</returns>
        public static MvcHtmlString DxaEntity(this HtmlHelper htmlHelper, EntityModel entity, string viewName, int containerSize = 0)
        {
            MvcData mvcDataOverride = new MvcData(viewName);
            MvcData orginalMvcData = entity.MvcData;
            MvcData tempMvcData = new MvcData(orginalMvcData)
            {
                AreaName = mvcDataOverride.AreaName,
                ViewName = mvcDataOverride.ViewName
            };

            try
            {
                entity.MvcData = tempMvcData;
                return htmlHelper.DxaEntity(entity, containerSize);
            }
            finally
            {
                entity.MvcData = orginalMvcData;
            }
        }

        /// <summary>
        /// Renders all Entities in the current Region Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="containerSize">The size (in grid column units) of the containing element.</param>
        /// <returns>The rendered HTML.</returns>
        /// <remarks>This method will throw an exception if the current Model does not represent a Region.</remarks>
        public static MvcHtmlString DxaEntities(this HtmlHelper htmlHelper, int containerSize = 0)
        {
            using (new Tracer(htmlHelper, containerSize))
            {
                RegionModel region = (RegionModel)htmlHelper.ViewData.Model;

                StringBuilder resultBuilder = new StringBuilder();
                foreach (EntityModel entity in region.Entities)
                {
                    resultBuilder.Append(htmlHelper.DxaEntity(entity, containerSize));
                }
                return new MvcHtmlString(resultBuilder.ToString());
            }
        }

        /// <summary>
        /// Renders a given Region Model
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="region">The Region Model to render. This object determines the View that will be used.</param>
        /// <param name="containerSize">The size (in grid column units) of the containing element.</param>
        /// <returns>The rendered HTML or an empty string if <paramref name="region"/> is <c>null</c>.</returns>
        public static MvcHtmlString DxaRegion(this HtmlHelper htmlHelper, RegionModel region, int containerSize = 0)
        {
            if (region == null)
            {
                return MvcHtmlString.Empty;
            }

            if (containerSize == 0)
            {
                containerSize = SiteConfiguration.MediaHelper.GridSize;
            }

            using (new Tracer(htmlHelper, region, containerSize))
            {
                MvcData mvcData = region.MvcData;
                string actionName = mvcData.ActionName ?? SiteConfiguration.GetRegionAction();
                string controllerName = mvcData.ControllerName ?? SiteConfiguration.GetRegionController();
                string controllerAreaName = mvcData.ControllerAreaName ?? SiteConfiguration.GetDefaultModuleName();

                MvcHtmlString result = htmlHelper.Action(actionName, controllerName, new { Region = region, containerSize = containerSize, area = controllerAreaName });

                if (WebRequestContext.IsPreview)
                {
                    result = new MvcHtmlString(Markup.TransformXpmMarkupAttributes(result.ToString()));
                }
                return Markup.DecorateMarkup(result, region);
            }
        }

        /// <summary>
        /// Renders a Region (of the current Page or Region Model) with a given name.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="regionName">The name of the Region to render. This object determines the View that will be used.</param>
        /// <param name="emptyViewName">
        /// The name of the View to use when no Region with the given name is found in the Page Model (i.e. no Entities exist in the given Region). 
        /// If <c>null</c> (the default) then nothing will be rendered in that case.
        /// If the View is not in the Core Area, the View name has to be in the format AreaName:ViewName. 
        /// </param>
        /// <param name="containerSize">The size (in grid column units) of the containing element.</param>
        /// <returns>The rendered HTML or an empty string if no Region with a given name is found and <paramref name="emptyViewName"/> is <c>null</c>.</returns>
        /// <remarks>This method will throw an exception if the current Model does not represent a Page.</remarks>
        public static MvcHtmlString DxaRegion(this HtmlHelper htmlHelper, string regionName, string emptyViewName = null, int containerSize = 0)
        {
            using (new Tracer(htmlHelper, regionName, emptyViewName, containerSize))
            {
                RegionModelSet regions = GetRegions(htmlHelper.ViewData.Model);

                RegionModel region;
                if (!regions.TryGetValue(regionName, out region))
                {
                    if (emptyViewName == null)
                    {
                        Log.Debug("Region '{0}' not found and no empty View specified. Skipping.", regionName);
                        return MvcHtmlString.Empty;
                    }
                    Log.Debug("Region '{0}' not found. Using empty View '{1}'.", regionName, emptyViewName);
                    region = new RegionModel(regionName, emptyViewName);
                }

                return htmlHelper.DxaRegion(region, containerSize);
            }
        }

        /// <summary>
        /// Renders the current (Include) Page as a Region.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <returns>The rendered HTML.</returns>
        public static MvcHtmlString DxaRegion(this HtmlHelper htmlHelper)
        {
            using (new Tracer(htmlHelper))
            {
                PageModel pageModel = (PageModel)htmlHelper.ViewData.Model;

                // Create a new Region Model which reflects the Page Model
                string regionName = pageModel.Title;
                MvcData mvcData = new MvcData
                {
                    ViewName = regionName,
                    AreaName = SiteConfiguration.GetDefaultModuleName(),
                    ControllerName = SiteConfiguration.GetRegionController(),
                    ControllerAreaName = SiteConfiguration.GetDefaultModuleName(),
                    ActionName = SiteConfiguration.GetRegionAction()
                };

                RegionModel regionModel = new RegionModel(regionName) { MvcData = mvcData };
                regionModel.Regions.UnionWith(pageModel.Regions);

                return htmlHelper.DxaRegion(regionModel);
            }
        }

        /// <summary>
        /// Renders all Regions (of the current Page or Region Model), except the ones with given names.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="exclude">The (comma separated) name(s) of the Regions to exclude. Can be <c>null</c> (the default) to render all Regions.</param>
        /// <param name="containerSize">The size (in grid column units) of the containing element.</param>
        /// <returns>The rendered HTML.</returns>
        /// <remarks>This method will throw an exception if the current Model does not represent a Page.</remarks>
        public static MvcHtmlString DxaRegions(this HtmlHelper htmlHelper, string exclude = null, int containerSize = 0)
        {
            using (new Tracer(htmlHelper, exclude, containerSize))
            {
                RegionModelSet regions = GetRegions(htmlHelper.ViewData.Model);

                IEnumerable<RegionModel> filteredRegions;
                if (string.IsNullOrEmpty(exclude))
                {
                    filteredRegions = regions;
                }
                else
                {
                    string[] excludedNames = exclude.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    filteredRegions = regions.Where(r => !excludedNames.Any(n => n.Equals(r.Name, StringComparison.InvariantCultureIgnoreCase)));
                }

                StringBuilder resultBuilder = new StringBuilder();
                foreach (RegionModel region in filteredRegions)
                {
                    resultBuilder.Append(htmlHelper.DxaRegion(region, containerSize));
                }

                return new MvcHtmlString(resultBuilder.ToString());
            }
        }

        #endregion

        #region Semantic markup extension methods

        /// <summary>
        /// Generates XPM markup for the current Page Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <returns>The XPM markup for the Page.</returns>
        /// <remarks>This method will throw an exception if the current Model does not represent a Page.</remarks>
        public static MvcHtmlString DxaPageMarkup(this HtmlHelper htmlHelper)
        {
            // TODO TSI-776: this method should output "semantic" attributes on the HTML element representing the Page like we do for DxaRegionMarkup, DxaEntityMarkup and DxaPropertyMarkup
            if (!WebRequestContext.Localization.IsXpmEnabled)
            {
                return MvcHtmlString.Empty;
            }

            PageModel page = (PageModel) htmlHelper.ViewData.Model;

            using (new Tracer(htmlHelper, page))
            {
                return new MvcHtmlString(page.GetXpmMarkup(WebRequestContext.Localization));
            }
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for the current Region Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <returns>The HTML/RDFa attributes for the Region. These should be included in an HTML start tag.</returns>
        /// <remarks>This method will throw an exception if the current Model does not represent a Region.</remarks>
        public static MvcHtmlString DxaRegionMarkup(this HtmlHelper htmlHelper)
        {
            RegionModel region = (RegionModel) htmlHelper.ViewData.Model;
            return htmlHelper.DxaRegionMarkup(region);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given Region Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="region">The Region Model to generate semantic markup for.</param>
        /// <returns>The HTML/RDFa attributes for the Region. These should be included in an HTML start tag.</returns>
        public static MvcHtmlString DxaRegionMarkup(this HtmlHelper htmlHelper, RegionModel region)
        {
            return Markup.RenderRegionAttributes(region);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for the current Entity Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <returns>The HTML/RDFa attributes for the Entity. These should be included in an HTML start tag.</returns>
        /// <remarks>This method will throw an exception if the current Model does not represent an Entity.</remarks>
        public static MvcHtmlString DxaEntityMarkup(this HtmlHelper htmlHelper)
        {
            EntityModel entity = (EntityModel) htmlHelper.ViewData.Model;
            return htmlHelper.DxaEntityMarkup(entity);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given Entity Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="entity">The Entity Model to generate semantic markup for.</param>
        /// <returns>The HTML/RDFa attributes for the Entity. These should be included in an HTML start tag.</returns>
        public static MvcHtmlString DxaEntityMarkup(this HtmlHelper htmlHelper, EntityModel entity)
        {
            return Markup.RenderEntityAttributes(entity);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given property of the current Entity Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="index">The index of the property value (for multi-value properties).</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        public static MvcHtmlString DxaPropertyMarkup(this HtmlHelper htmlHelper, string propertyName, int index = 0)
        {
            EntityModel entity = (EntityModel) htmlHelper.ViewData.Model;
            return Markup.RenderPropertyAttributes(entity, propertyName, index);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given property of a given Entity Model.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="entity">The Entity Model.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="index">The index of the property value (for multi-value properties).</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        public static MvcHtmlString DxaPropertyMarkup(this HtmlHelper htmlHelper, EntityModel entity, string propertyName, int index = 0)
        {
            return Markup.RenderPropertyAttributes(entity, propertyName, index);
        }

        /// <summary>
        /// Generates semantic markup (HTML/RDFa attributes) for a given property.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="propertyExpression">A parameterless lambda expression which evaluates to a property of the current Entity Model.</param>
        /// <param name="index">The index of the property value (for multi-value properties).</param>
        /// <returns>The semantic markup (HTML/RDFa attributes).</returns>
        public static MvcHtmlString DxaPropertyMarkup(this HtmlHelper htmlHelper, Expression<Func<object>> propertyExpression, int index = 0)
        {
            MemberExpression memberExpression = propertyExpression.Body as MemberExpression;
            if (memberExpression == null)
            {
                UnaryExpression boxingExpression = propertyExpression.Body as UnaryExpression;
                if (boxingExpression != null)
                {
                    memberExpression = boxingExpression.Operand as MemberExpression;
                }
            }
            if (memberExpression == null)
            {
                throw new DxaException(
                    $"Unexpected expression provided to DxaPropertyMarkup: {propertyExpression.Body.GetType().Name}. Expecting a lambda which evaluates to an Entity Model property."
                    );
            }

            object subject;
            MemberExpression subjectExpression = memberExpression.Expression as MemberExpression;
            if (subjectExpression != null && subjectExpression.Member.Name == "Model")
            {
                // Often the subject of the property expression is the current Model. For example: () => Model.Headline
                // This is a shortcut to prevent having to compile the subject expression for that case.
                subject = htmlHelper.ViewData.Model;
            }
            else
            {
                Expression<Func<object>> entityExpression = Expression.Lambda<Func<object>>(memberExpression.Expression);
                Func<object> entityLambda = entityExpression.Compile();
                subject = entityLambda.Invoke();
            }

            EntityModel entityModel = subject as EntityModel;
            if (entityModel == null)
            {
                throw new DxaException(
                    $"Unexpected type used in DxaPropertyMarkup expression: {subject}. Expecting a lambda which evaluates to an Entity Model property."
                    );
            }

            return Markup.RenderPropertyAttributes(entityModel, memberExpression.Member, index);
        }
        #endregion

        /// <summary>
        /// Renders a given <see cref="RichText"/> instance as HTML.
        /// </summary>
        /// <param name="htmlHelper">The HtmlHelper instance on which the extension method operates.</param>
        /// <param name="richText">The <see cref="RichText"/> instance to render. If the rich text contains Entity Models, those will be rendered using applicable Views.</param>
        /// <returns>The rendered HTML.</returns>
        public static MvcHtmlString DxaRichText(this HtmlHelper htmlHelper, RichText richText)
        {
            if (richText == null)
            {
                return MvcHtmlString.Empty;
            }

            StringBuilder htmlBuilder = new StringBuilder();
            foreach (IRichTextFragment richTextFragment in richText.Fragments)
            {
                EntityModel entityModel = richTextFragment as EntityModel;
                string htmlFragment = (entityModel == null) ? richTextFragment.ToHtml() : htmlHelper.DxaEntity(entityModel).ToString();
                htmlBuilder.Append(htmlFragment);
            }

            return new MvcHtmlString(htmlBuilder.ToString());
        }

        /// <summary>
        /// Gets the Regions from a Page or Region Model.
        /// </summary>
        /// <param name="model">The Page Or Region Model</param>
        /// <returns>The Regions obtained from the model.</returns>
        private static RegionModelSet GetRegions(object model)
        {
            RegionModelSet result;
            if (model is PageModel)
            {
                result = ((PageModel)model).Regions;
            }
            else
            {
                result = ((RegionModel)model).Regions;
            }
            return result;
        }
    }
}
