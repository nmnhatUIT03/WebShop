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
    public class PageController : Controller
    {
        private readonly webshopContext _context;
        public PageController(webshopContext context)
        {
            _context = context;
        }

        [Route("page.html", Name = "Page")]
        public IActionResult Index(int? page)
        {
            var pageNumber = page == null || page <= 0 ? 1 : page.Value;
            var pageSize = 20;
            var lsPages = _context.Pages.AsNoTracking()

                .OrderByDescending(x => x.PageId);
            PagedList<Page> models = new PagedList<Page>(lsPages, pageNumber, pageSize);
            ViewBag.CurrentPage = pageNumber;
            return View(models);
        }

        public IActionResult Details(int id)
        {

            var page = _context.Pages.AsNoTracking().SingleOrDefault(x => x.PageId == id);
            if (page == null)
            {
                return RedirectToAction("Index");
            }

            return View(page);
        }
    }
}
