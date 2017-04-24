﻿using Blog.Models;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using Microsoft.AspNet.Identity;

namespace Blog.Controllers
{
    public class ArticleController : Controller
    {
        //
        // GET: Article
        public ActionResult Index()
        {
            return RedirectToAction("List");
        }

        //
        // GET: Article/My
        public ActionResult My(string authorId, string sortArgs)
        {
            if (authorId == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Verify that the request is from the author
            if (this.User.Identity.GetUserId() != authorId && !this.User.IsInRole("Admin"))
            {
                return RedirectToAction("List", new { authorId = authorId });
            }

            using (var dataabase = new BlogDbContext())
            {
                // Get articles
                var articles = dataabase.Articles
                    .Where(a => a.AuthorId.Equals(authorId))
                    .ToList();

                // check for sorting argument
                switch (sortArgs)
                {
                    case null:
                        break;
                    case "Views":
                        articles = articles.OrderByDescending(a => a.Visits).ToList();
                        break;
                    case "Date":
                        articles = articles.OrderByDescending(a => a.DateCreated).ToList();
                        break;
                    default:
                        break;
                }

                return View(articles);
            }
        }

        //
        // GET: Article/List
        public ActionResult List(string authorId = null, int? categoryId = null, int? tagId = null)
        {
            using (var database = new BlogDbContext())
            {
                // Get articles from the database
                var articles = database.Articles
                    .Include(a => a.Author)
                    .Include(a => a.Tags)
                    .ToList();

                // Check for arguments
                if (authorId != null)
                {
                    articles = articles.Where(a => a.Author.Id.Equals(authorId)).ToList();
                }
                else if (categoryId != null)
                {
                    articles = articles.Where(a => a.Category.Id == categoryId).ToList();
                }
                else if (tagId != null)
                {
                    articles = database.Tags.Where(t => t.Id == tagId).FirstOrDefault().Articles.ToList();
                }

                if (articles == null)
                {
                    return HttpNotFound();
                }

                return View(articles);
            }
        }

        //
        // GET: Article/Edit
        [Authorize]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var database = new BlogDbContext())
            {
                // Get article from the database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .First();

                if (!IsUserAuthorizedToEdit(article))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                // Check if the article exists
                if (article == null)
                {
                    return HttpNotFound();
                }

                // Create the view model
                var model = new ArticleViewModel();
                model.Id = article.Id;
                model.Title = article.Title;
                model.Content = article.Content;
                model.CategoryId = article.CategoryId;
                model.Categories = database.Categories.OrderBy(c => c.Name).ToList();
                model.Tags = string.Join(", ", article.Tags.Select(t => t.Name));

                // Pass the view mode to view
                return View(model);
            }
        }

        //
        // POST: Article/Edit
        [HttpPost]
        [Authorize]
        public ActionResult Edit(ArticleViewModel model)
        {
            // Check if model state is valid
            if (ModelState.IsValid)
            {
                using (var database = new BlogDbContext())
                {
                    // Get article from database
                    var article = database.Articles
                        .FirstOrDefault(a => a.Id == model.Id);

                    // Set article properties
                    article.Title = model.Title;
                    article.Content = model.Content;
                    article.CategoryId = model.CategoryId;
                    this.SetArticleTags(article, model, database);

                    // Save article state in database
                    database.Entry(article).State = EntityState.Modified;
                    database.SaveChanges();

                    // Redirect to Details page
                    return RedirectToAction("Details", "Article", new { id = article.Id });
                }
            }

            // If model state is invalid return the same view
            using (var database = new BlogDbContext())
            {
                model.Categories = database.Categories.OrderBy(c => c.Name).ToList();
                return View(model);
            }
        }

        //
        // GET: Article/Delete
        [Authorize]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var database = new BlogDbContext())
            {
                // Get article from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .First();

                if (!IsUserAuthorizedToEdit(article))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                // Check if the article exists
                if (article == null)
                {
                    return HttpNotFound();
                }

                // Create article view model
                var model = new ArticleViewModel();
                model.Title = article.Title;
                model.Content = article.Content;
                model.CategoryId = article.CategoryId;
                model.Category = article.Category;

                ViewBag.TagsString = string.Join(", ", article.Tags.Select(t => t.Name));

                // Pass article to view
                return View(model);
            }
        }

        //
        // POST: Article/Delete
        [Authorize]
        [HttpPost]
        [ActionName("Delete")]
        public ActionResult DeleteConfirmed(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var database = new BlogDbContext())
            {
                // Get article from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .First();

                // Check if article exists
                if (article == null)
                {
                    return HttpNotFound();
                }

                // Check if tag will be empty
                foreach (var tag in article.Tags.ToList())
                {
                    if (tag.Articles.Count <= 1)
                    {
                        database.Tags.Remove(tag);
                    }
                }

                // Remove comments
                var comments = article.Comments;
                foreach (var comment in comments.ToList())
                {
                    database.Comments.Remove(comment);
                }

                // Delete article from database
                database.Articles.Remove(article);
                database.SaveChanges();

                // Redirect to index page
                return RedirectToAction("Index");
            }
        }


        //
        // GET: Article/Create
        [Authorize]
        public ActionResult Create()
        {
            using (var database = new BlogDbContext())
            {
                var model = new ArticleViewModel();
                model.Categories = database.Categories.OrderBy(c => c.Name).ToList();

                return View(model);
            }
        }

        //
        // POST: Article/Create
        [HttpPost]
        [Authorize]
        public ActionResult Create(ArticleViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var database = new BlogDbContext())
                {
                    // Get author id
                    var authorId = database.Users
                        .Where(u => u.UserName == this.User.Identity.Name)
                        .First()
                        .Id;

                    // Create article
                    var article = new Article(authorId, model.Title, model.Content, model.CategoryId);

                    this.SetArticleTags(article, model, database);

                    // Save article in DB
                    database.Articles.Add(article);
                    database.SaveChanges();

                    return RedirectToAction("Index", "Home");
                }
            }

            // Give Categories to the mopdel
            using (var database = new BlogDbContext())
            {
                model.Categories = database.Categories.OrderBy(c => c.Name).ToList();
                return View(model);
            }
        }

        //
        // GET: Article/Details
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var database = new BlogDbContext())
            {
                // Get the article from the database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .Include(a => a.Tags)
                    .Include(a => a.Category)
                    .Include(a => a.Comments)
                    .FirstOrDefault();

                if (article == null)
                {
                    return HttpNotFound();
                }

                string userAgent = System.Web.HttpContext.Current.Request.UserAgent.ToLower();
                if (!IsCrawlByBot(userAgent))
                {
                    article.Visits++;
                    database.Entry(article).State = EntityState.Modified;
                    database.SaveChanges();
                }

                ViewBag.DateString = DateToString(article.DateCreated);

                return View(article);
            }
        }

        //
        // For creating comments directly from the details page
        // POST: Article/Details
        [HttpPost]
        [Authorize]
        public ActionResult Comment(Article model)
        {
            using (var database = new BlogDbContext())
            {
                var article = database.Articles.Where(a => a.Id == model.Id);

                if (article != null)
                {
                    var authorId = User.Identity.GetUserId();
                    var comment = new Comment(authorId, model.Id, model.Comment);

                    database.Comments.Add(comment);
                    database.SaveChanges();
                }

                return RedirectToAction("Details", new { id = model.Id });
            }
        }

        public bool IsUserAuthorizedToEdit(Article article)
        {
            bool isAdmin = this.User.IsInRole("Admin");
            bool isAuthor = article.IsAuthor(this.User.Identity.Name);

            return isAdmin || isAuthor;
        }

        private void SetArticleTags(Article article, ArticleViewModel model, BlogDbContext database)
        {
            // Spli tags
            var tagsStrings = model.Tags
                .Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLower())
                .Distinct();

            // Clear current article tags
            article.Tags.Clear();

            // Set new article tags
            foreach (var tagStering in tagsStrings)
            {
                // Get tag from databaase by its name
                Tag tag = database.Tags.FirstOrDefault(t => t.Name.Equals(tagStering));

                // If the tag is null, create new tag
                if (tag == null)
                {
                    tag = new Tag() { Name = tagStering };
                    database.Tags.Add(tag);
                }

                // Add tag to article tags
                article.Tags.Add(tag);
            }
        }

        private string DateToString(DateTime date)
        {
            return date.ToString("dd-MM-yyyy");
        }

        private static bool IsCrawlByBot(string userAgent)
        {
            List<string> Crawlers = new List<string>()
            {
                "googlebot","bingbot","yandexbot","ahrefsbot","msnbot","linkedinbot","exabot","compspybot",
                "yesupbot","paperlibot","tweetmemebot","semrushbot","gigabot","voilabot","adsbot-google",
                "botlink","alkalinebot","araybot","undrip bot","borg-bot","boxseabot","yodaobot","admedia bot",
                "ezooms.bot","confuzzledbot","coolbot","internet cruiser robot","yolinkbot","diibot","musobot",
                "dragonbot","elfinbot","wikiobot","twitterbot","contextad bot","hambot","iajabot","news bot",
                "irobot","socialradarbot","ko_yappo_robot","skimbot","psbot","rixbot","seznambot","careerbot",
                "simbot","solbot","mail.ru_bot","spiderbot","blekkobot","bitlybot","techbot","void-bot",
                "vwbot_k","diffbot","friendfeedbot","archive.org_bot","woriobot","crystalsemanticsbot","wepbot",
                "spbot","tweetedtimes bot","mj12bot","who.is bot","psbot","robot","jbot","bbot","bot"
            };

            bool iscrawler = Crawlers.Contains(userAgent);
            return iscrawler;
        }
    }
}