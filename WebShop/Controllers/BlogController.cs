using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PagedList.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebShop.Models;

namespace WebShop.Controllers
{
    public class BlogController : Controller
    {
        private readonly webshopContext _context;

        public BlogController(webshopContext context)
        {
            _context = context;
        }

        [Route("blog.html", Name = "Blog")]
        public IActionResult Index(int? page)
        {
            var pageNumber = page == null || page <= 0 ? 1 : page.Value;
            var pageSize = 10;
            var lsTinTuc = _context.TinTucs.AsNoTracking()

                .OrderByDescending(x => x.PostId);
            PagedList<TinTuc> models = new PagedList<TinTuc>(lsTinTuc, pageNumber, pageSize);
            ViewBag.CurrentPage = pageNumber;
            return View(models);
        }

        public IActionResult Details(int id)
        {

            var tintuc = _context.TinTucs.AsNoTracking().SingleOrDefault(x => x.PostId == id);
            if (tintuc == null)
            {
                return RedirectToAction("Index");
            }

            return View(tintuc);
        }
    }
}
