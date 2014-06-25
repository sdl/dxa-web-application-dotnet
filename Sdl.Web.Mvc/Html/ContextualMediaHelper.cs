﻿using Sdl.Web.Mvc.Html;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sdl.Web.Mvc.Html
{
    public class ContextualMediaHelper : BaseMediaHelper
    {
        public string ImageResizeRoute { get; set; }
        public ContextualMediaHelper() : base()
        {
            ImageResizeUrlFormat = "/{0}/scale/{1}x{2}/source/site{3}";
            ImageResizeRoute = "cid";
        }

        public override string GetResponsiveImageUrl(string url, double aspect, string widthFactor, int containerSize = 0)
        {
            int width = GetResponsiveWidth(widthFactor, containerSize);
            //Round the width to the nearest set limit point - important as we do not want 
            //to swamp the cache with lots of different sized versions of the same image
            for (int i = 0; i < ImageWidths.Count; i++)
            {
                if (width <= ImageWidths[i])
                {
                    width = ImageWidths[i];
                    break;
                }
            }
            //Height is calculated from the aspect ratio
            int height = (int)Math.Ceiling(width / aspect);
            //Build the URL
            return String.Format(ImageResizeUrlFormat, ImageResizeRoute, width, height, url);
        }
    }
}
