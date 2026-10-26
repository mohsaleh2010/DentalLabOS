// DentalLab OS — progressive enhancement for server-rendered pages.
// Every feature works without JS; this only improves interaction (theme, nav, dialogs, offline state).
(() => {
  "use strict";
  const root = document.documentElement;
  const store = {
    get(k) { try { return localStorage.getItem(k); } catch { return null; } },
    set(k, v) { try { localStorage.setItem(k, v); } catch { /* storage may be unavailable */ } },
  };

  // ---- Theme: light | dark | system (server renders the saved preference; this keeps it in sync) ----
  function applyTheme(theme) {
    root.dataset.theme = theme;
    document.cookie = `dlos-theme=${theme}; path=/; max-age=31536000; samesite=lax`;
    document.querySelectorAll("[data-theme-option]").forEach((el) => {
      el.classList.toggle("is-active", el.dataset.themeOption === theme);
    });
  }
  document.addEventListener("click", (e) => {
    const opt = e.target.closest("[data-theme-option]");
    if (opt) {
      e.preventDefault();
      applyTheme(opt.dataset.themeOption);
    }
    const toggle = e.target.closest("[data-theme-toggle]");
    if (toggle) {
      const dark = root.dataset.theme === "dark" ||
        (root.dataset.theme === "system" && matchMedia("(prefers-color-scheme: dark)").matches);
      applyTheme(dark ? "light" : "dark");
    }
  });

  // ---- App shell: sidebar collapse (desktop) and off-canvas nav (mobile) ----
  const app = document.querySelector(".app");
  if (app) {
    if (store.get("dlos-sidebar") === "collapsed") app.dataset.sidebar = "collapsed";
    document.addEventListener("click", (e) => {
      if (e.target.closest("[data-sidebar-toggle]")) {
        const collapsed = app.dataset.sidebar === "collapsed";
        app.dataset.sidebar = collapsed ? "expanded" : "collapsed";
        store.set("dlos-sidebar", app.dataset.sidebar);
      }
      if (e.target.closest("[data-mobile-nav-toggle]")) {
        app.dataset.mobileNav = app.dataset.mobileNav === "open" ? "closed" : "open";
      }
      if (e.target.closest(".scrim")) app.dataset.mobileNav = "closed";
    });
  }

  // Close <details> menus when clicking outside or pressing Escape.
  document.addEventListener("click", (e) => {
    document.querySelectorAll("details.menu[open]").forEach((d) => { if (!d.contains(e.target)) d.removeAttribute("open"); });
  });
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      document.querySelectorAll("details.menu[open]").forEach((d) => d.removeAttribute("open"));
      if (app) app.dataset.mobileNav = "closed";
    }
    // "/" focuses the global search (unless typing in a field).
    if (e.key === "/" && !/^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName ?? "")) {
      const s = document.querySelector("[data-global-search]");
      if (s) { e.preventDefault(); s.focus(); }
    }
  });

  // ---- Confirm dialog for dangerous actions: <button data-confirm="..." data-confirm-title="..."> ----
  const dialog = document.getElementById("confirm-dialog");
  let pending = null;
  document.addEventListener("click", (e) => {
    const trigger = e.target.closest("[data-confirm]");
    if (!trigger || !dialog || trigger.dataset.confirmed === "1") return;
    e.preventDefault();
    pending = trigger;
    dialog.querySelector("[data-confirm-title]").textContent = trigger.dataset.confirmTitle || dialog.dataset.defaultTitle;
    dialog.querySelector("[data-confirm-text]").textContent = trigger.dataset.confirm;
    dialog.showModal();
  });
  dialog?.addEventListener("close", () => {
    if (dialog.returnValue === "ok" && pending) {
      pending.dataset.confirmed = "1";
      pending.click();
      delete pending.dataset.confirmed;
    }
    pending = null;
  });

  // ---- Forms: loading state on submit + unsaved-changes guard (data-guard) ----
  document.addEventListener("submit", (e) => {
    const form = e.target;
    form.dataset.dirty = "0";
    const btn = e.submitter;
    if (btn && btn.classList.contains("btn")) {
      btn.classList.add("is-loading");
      setTimeout(() => btn.setAttribute("aria-disabled", "true"), 0);
    }
  });
  document.querySelectorAll("form[data-guard]").forEach((form) => {
    form.addEventListener("input", () => { form.dataset.dirty = "1"; });
  });
  window.addEventListener("beforeunload", (e) => {
    if (document.querySelector("form[data-guard][data-dirty='1']")) { e.preventDefault(); e.returnValue = ""; }
  });

  // ---- Copy to clipboard: <button data-copy="#id"> ----
  document.addEventListener("click", async (e) => {
    const btn = e.target.closest("[data-copy]");
    if (!btn) return;
    const src = document.querySelector(btn.dataset.copy);
    const text = src?.value ?? src?.textContent ?? "";
    try {
      await navigator.clipboard.writeText(text.trim());
      toast(btn.dataset.copiedText || "✓");
    } catch { /* clipboard blocked: the text is still selectable */ }
  });

  // ---- Permission matrix: toggle a whole module ----
  document.addEventListener("click", (e) => {
    const t = e.target.closest("[data-check-module]");
    if (!t) return;
    e.preventDefault();
    const boxes = document.querySelectorAll(`input[data-module="${t.dataset.checkModule}"]:not(:disabled)`);
    const allOn = [...boxes].every((b) => b.checked);
    boxes.forEach((b) => { b.checked = !allOn; });
    t.closest("form")?.dispatchEvent(new Event("input", { bubbles: true }));
  });

  // ---- Toasts ----
  function toast(message, isError = false) {
    let host = document.querySelector(".toasts");
    if (!host) { host = document.createElement("div"); host.className = "toasts"; host.setAttribute("role", "status"); document.body.appendChild(host); }
    const el = document.createElement("div");
    el.className = "toast" + (isError ? " is-error" : "");
    el.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/_content/DentalLabOS.SharedUI/icons/sprite.svg#i-${isError ? "circle-alert" : "circle-check"}"></use></svg><div></div>`;
    el.querySelector("div").textContent = message;
    host.appendChild(el);
    setTimeout(() => el.remove(), 4200);
  }
  window.dlos = { toast };
  document.querySelectorAll("[data-toast]").forEach((el) => toast(el.dataset.toast, el.dataset.toastError === "1"));

  // ---- Online / offline indicator ----
  const setOnline = () => root.classList.toggle("is-offline", !navigator.onLine);
  addEventListener("online", setOnline);
  addEventListener("offline", setOnline);
  setOnline();
})();
