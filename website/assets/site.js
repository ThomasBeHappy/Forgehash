/**
 * ForgeHash docs site config + shared chrome.
 *
 * On github.io project pages, owner/repo and the pages base path are inferred
 * from the URL (works for /Forgehash/ and /ForgeHash/).
 */
(function () {
  "use strict";

  var defaults = {
    repo: "https://github.com/ThomasBeHappy/Forgehash",
    pagesBase: ""
  };

  var cfg = Object.assign({}, defaults, window.FORGEH || {});

  try {
    var host = location.hostname || "";
    var m = host.match(/^([a-z0-9-]+)\.github\.io$/i);
    if (m) {
      var parts = location.pathname.split("/").filter(Boolean);
      var repoName = parts.length > 0 ? parts[0] : "Forgehash";
      if (!window.FORGEH || !window.FORGEH.repo) {
        cfg.repo = "https://github.com/" + m[1] + "/" + repoName;
      }
      // Project pages live under /RepoName/
      if (parts.length > 0) {
        cfg.pagesBase = "/" + parts[0];
      }
    }
  } catch (e) {
    /* ignore */
  }

  window.FORGEH = cfg;

  var NAV = [
    { href: "index.html", label: "Overview", id: "index" },
    { href: "usage.html", label: ".NET usage", id: "usage" },
    { href: "languages.html", label: "Languages", id: "languages" },
    { href: "implementing.html", label: "Implementing", id: "implementing" },
    { href: "vectors.html", label: "Vectors", id: "vectors" },
    { href: "research.html", label: "Research", id: "research" },
    { href: "research-report.html", label: "Report", id: "research-report" },
    { href: "security.html", label: "Security", id: "security" },
    { href: "spec.html", label: "Specification", id: "spec" }
  ];

  function pageId() {
    var meta = document.querySelector('meta[name="forgeh-page"]');
    if (meta && meta.content) return meta.content;
    var file = (location.pathname.split("/").pop() || "index.html").toLowerCase();
    if (!file || file === "" || file.indexOf(".") === -1) return "index";
    return file.replace(/\.html$/, "");
  }

  function repoUrl(path) {
    var base = cfg.repo.replace(/\/$/, "");
    var p = String(path || "").replace(/^\//, "");
    return base + "/blob/main/" + p;
  }

  function rewriteRepoLinks(root) {
    var scope = root || document;
    scope.querySelectorAll("[data-repo]").forEach(function (el) {
      var path = el.getAttribute("data-repo");
      var href = repoUrl(path);
      if (el.tagName === "A") {
        el.setAttribute("href", href);
        if (href.indexOf("http") === 0) {
          el.setAttribute("rel", "noopener noreferrer");
        }
      }
    });
  }

  function buildNavList(current) {
    return NAV.map(function (item) {
      var cur = item.id === current ? ' aria-current="page"' : "";
      return '<li><a href="' + item.href + '"' + cur + ">" + item.label + "</a></li>";
    }).join("");
  }

  function ensureChrome() {
    var current = pageId();
    var header = document.getElementById("site-header");
    if (header && !header.dataset.ready) {
      header.dataset.ready = "1";
      header.innerHTML =
        '<a class="skip-link" href="#main">Skip to content</a>' +
        '<div class="brand-row">' +
        '<img class="brand-mark" src="assets/mark.png" width="32" height="32" alt="">' +
        '<div>' +
        '<a class="brand" href="index.html">ForgeHash</a>' +
        '<p class="brand-meta">ForgeHash-B3 · docs · experimental</p>' +
        "</div>" +
        '<button type="button" class="nav-toggle" id="nav-toggle" aria-expanded="false" aria-controls="site-nav">Menu</button>' +
        "</div>" +
        '<nav class="site-nav" id="site-nav" aria-label="Documentation">' +
        "<ul>" +
        buildNavList(current) +
        "</ul>" +
        "</nav>";

      var toggle = document.getElementById("nav-toggle");
      var nav = document.getElementById("site-nav");
      if (toggle && nav) {
        toggle.addEventListener("click", function () {
          var open = nav.classList.toggle("is-open");
          toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });
      }
    }

    var side = document.getElementById("side-nav");
    if (side && !side.dataset.ready) {
      side.dataset.ready = "1";
      side.innerHTML =
        "<h2>On this site</h2><ul>" + buildNavList(current) + "</ul>";
    }

    var footer = document.getElementById("site-footer");
    if (footer && !footer.dataset.ready) {
      footer.dataset.ready = "1";
      footer.innerHTML =
        '<span>ForgeHash-B3 · experimental cryptography</span>' +
        '<span>' +
        '<a data-repo="SPECIFICATION.md" href="../SPECIFICATION.md">Specification</a> · ' +
        '<a data-repo="LICENSE" href="../LICENSE">License</a> · ' +
        '<a data-repo="SECURITY.md" href="../SECURITY.md">Security</a>' +
        "</span>";
    }

    rewriteRepoLinks(document);
  }

  window.FORGEH.repoUrl = repoUrl;
  window.FORGEH.rewriteRepoLinks = rewriteRepoLinks;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", ensureChrome);
  } else {
    ensureChrome();
  }
})();
