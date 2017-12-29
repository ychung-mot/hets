﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using HETSAPI.Models.Impl;

namespace HETSAPI.Controllers
{
    /// <summary>
    /// Default home controller for the HETSAPI
    /// </summary>
    public class HomeController : Controller
    {
        private readonly IHostingEnvironment _env;

        /// <summary>
        /// AuthenticationController Constructir
        /// </summary>
        /// <param name="env"></param>
        public HomeController(IHostingEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Default action
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            HomeModel home = new HomeModel
            {
                UserId = HttpContext.User.Identity.Name,
                DevelopmentEnvironment = _env.IsDevelopment()
            };

            return View(home);
        }
    }
}
