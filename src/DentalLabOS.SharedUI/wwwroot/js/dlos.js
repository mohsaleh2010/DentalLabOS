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

  // ---- File drop zone: <label data-dropzone><input type=file><span data-file-name></span></label> ----
  document.querySelectorAll("[data-dropzone]").forEach((zone) => {
    const input = zone.querySelector("input[type=file]");
    const name = zone.querySelector("[data-file-name]");
    input.addEventListener("change", () => {
      const files = [...input.files];
      name.textContent = files.length > 1 ? files.map((f) => f.name).join("، ") : files[0]?.name || "";
    });
    ["dragenter", "dragover"].forEach((ev) => zone.addEventListener(ev, (e) => { e.preventDefault(); zone.classList.add("is-over"); }));
    ["dragleave", "drop"].forEach((ev) => zone.addEventListener(ev, (e) => { e.preventDefault(); zone.classList.remove("is-over"); }));
    zone.addEventListener("drop", (e) => {
      if (e.dataTransfer.files.length) { input.files = e.dataTransfer.files; input.dispatchEvent(new Event("change")); }
    });
  });

  // ---- Item form: show stock or service fields by type; a new item takes its category's default type ----
  document.querySelectorAll("form[data-item-form]").forEach((form) => {
    const type = form.querySelector("[data-item-type]");
    const category = form.querySelector("[data-category]");
    const stock = ["Material", "Consumable", "FinishedGood"];
    const sync = () => {
      const isStock = stock.includes(type.value);
      form.querySelector('[data-for="stock"]').hidden = !isStock;
      form.querySelector('[data-for="service"]').hidden = isStock;
    };
    type.addEventListener("change", sync);
    if (form.dataset.itemForm === "new") {
      category.addEventListener("change", () => {
        const t = category.selectedOptions[0]?.dataset.defaultType;
        if (t) { type.value = t; sync(); }
      });
    }
    sync();
  });


  // ---- Auto-submit filters: <select data-autosubmit> / <input type=checkbox data-autosubmit> ----
  document.addEventListener("change", (e) => {
    const el = e.target.closest("[data-autosubmit]");
    if (el && el.form) el.form.requestSubmit ? el.form.requestSubmit() : el.form.submit();
  });

  // ---- Print: <button data-print> ----
  document.addEventListener("click", (e) => { if (e.target.closest("[data-print]")) window.print(); });

  const fetchJson = async (url) => {
    const res = await fetch(url, { headers: { Accept: "application/json" }, credentials: "same-origin" });
    if (!res.ok) throw new Error(String(res.status));
    return res.json();
  };
  const fill = (select, items, emptyText, keep) => {
    const current = keep ?? select.value;
    select.replaceChildren();
    if (emptyText !== null) select.append(new Option(emptyText, ""));
    items.forEach((i) => select.append(new Option(i.label, i.value, false, String(i.value) === current)));
  };
  const debounce = (fn, ms) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

  // ---- Tooth chart: live count and quick selection ----
  document.querySelectorAll("[data-tooth-chart]").forEach((chart) => {
    const count = chart.querySelector("[data-tooth-count]");
    const boxes = () => [...chart.querySelectorAll("input[type=checkbox]")];
    const sync = () => {
      const n = boxes().filter((b) => b.checked).length;
      if (count) count.textContent = count.dataset.template.replace("{0}", n);
      chart.dispatchEvent(new CustomEvent("teeth-changed", { bubbles: true }));
    };
    chart.querySelectorAll("[data-teeth-select]").forEach((btn) => {
      btn.hidden = false;
      btn.addEventListener("click", () => {
        const which = btn.dataset.teethSelect;
        chart.querySelectorAll("[data-tooth]").forEach((b) => {
          const q = b.value[0];
          if (which === "none") b.checked = false;
          else if (which === "upper" && (q === "1" || q === "2")) b.checked = true;
          else if (which === "lower" && (q === "3" || q === "4")) b.checked = true;
        });
        if (which === "none") boxes().forEach((b) => { b.checked = false; });
        chart.closest("form")?.dispatchEvent(new Event("input", { bubbles: true }));
        sync();
      });
    });
    chart.addEventListener("change", sync);
  });

  // ---- Customer picker: search the lab's customers and load the chosen customer's dentists ----
  document.querySelectorAll("[data-customer-picker]").forEach((picker) => {
    const form = picker.closest("form");
    const search = picker.querySelector("[data-customer-search]");
    const select = picker.querySelector("[data-customer-select]");
    const dentist = form.querySelector("[data-dentist-select]");
    const empty = select.options[0]?.textContent ?? "";
    search?.addEventListener("input", debounce(async () => {
      try {
        const items = await fetchJson(`${picker.dataset.customersUrl}&q=${encodeURIComponent(search.value.trim())}`);
        fill(select, items, empty);
        if (items.length === 1 && !select.value) { select.value = items[0].value; select.dispatchEvent(new Event("change", { bubbles: true })); }
      } catch { /* keep the current list */ }
    }, 250));
    select.addEventListener("change", async () => {
      if (!dentist) return;
      if (!select.value) { fill(dentist, [], dentist.dataset.emptyText); return; }
      try {
        const items = await fetchJson(`${picker.dataset.dentistsUrl}&customerId=${select.value}`);
        fill(dentist, items, dentist.dataset.emptyText, "");
        if (items.length === 1) dentist.value = items[0].value;
      } catch { /* the server validates the dentist anyway */ }
    });
  });

  // ---- Priority: the reason is asked for only when the priority is raised ----
  document.querySelectorAll("form").forEach((form) => {
    const reason = form.querySelector("[data-priority-reason]");
    if (!reason) return;
    form.addEventListener("change", (e) => {
      if (e.target.matches("[data-priority]")) reason.hidden = e.target.value === "Normal";
    });
  });

  // ---- Intake: service → suggested due date, required fields, route; live price for permitted users ----
  document.querySelectorAll("form[data-intake]").forEach((form) => {
    const service = form.querySelector("[data-service-select]");
    if (!service) return;
    const due = form.querySelector("[data-due-date]");
    const summaryWf = form.querySelector("[data-summary-workflow]");
    const summaryReq = form.querySelector("[data-summary-required]");
    const price = form.querySelector("[data-summary-price]");
    const source = form.querySelector("[data-summary-source]");
    const labels = Object.fromEntries((summaryReq?.dataset.labels || "").split("|").filter(Boolean).map((p) => p.split("=")));
    let dueTouched = !!due?.value;
    due?.addEventListener("input", () => { dueTouched = true; });
    const customerId = () => form.querySelector("[data-customer-select]")?.value || form.querySelector("[data-customer-fixed]")?.value || "";
    const quantity = () => {
      const q = form.querySelector("[data-quantity]")?.value;
      if (q) return q;
      const n = [...form.querySelectorAll("[data-tooth]:checked")].length;
      return Math.max(1, n);
    };
    const quote = debounce(async () => {
      if (!form.dataset.quoteUrl || !price) return;
      if (!service.value || !customerId()) { price.textContent = "—"; if (source) source.textContent = ""; return; }
      try {
        const q = await fetchJson(`${form.dataset.quoteUrl}&customerId=${customerId()}&serviceId=${service.value}&quantity=${quantity()}`);
        price.textContent = q.total ?? "—";
        if (source) source.textContent = q.source ? `(${q.price} · ${q.source})` : "";
      } catch { price.textContent = "—"; }
    }, 300);
    const sync = () => {
      const opt = service.selectedOptions[0];
      const required = (opt?.dataset.required || "").split(",").filter(Boolean);
      form.querySelectorAll("[data-req-for]").forEach((m) => { m.hidden = !required.includes(m.dataset.reqFor); });
      if (summaryWf) summaryWf.textContent = opt?.dataset.workflow || "—";
      if (summaryReq) summaryReq.textContent = required.length ? required.map((r) => labels[r] || r).join("، ") : "—";
      if (due && opt?.dataset.due && !dueTouched) due.value = opt.dataset.due;
      quote();
    };
    service.addEventListener("change", sync);
    form.addEventListener("change", (e) => { if (e.target.matches("[data-customer-select], [data-quantity]")) quote(); });
    form.addEventListener("teeth-changed", quote);
    sync();
  });

  // ---- Select all rows: <input data-check-all="name"> + optional [data-selected-count] ----
  document.querySelectorAll("[data-check-all]").forEach((all) => {
    const form = all.closest("form");
    const boxes = () => [...form.querySelectorAll(`input[type=checkbox][name="${all.dataset.checkAll}"]`)];
    const counter = form.querySelector("[data-selected-count]");
    const sync = () => {
      const n = boxes().filter((b) => b.checked).length;
      if (counter) counter.textContent = counter.dataset.template.replace("{0}", n);
      all.checked = n > 0 && n === boxes().length;
      all.indeterminate = n > 0 && n < boxes().length;
    };
    all.addEventListener("change", () => { boxes().forEach((b) => { b.checked = all.checked; }); sync(); });
    form.addEventListener("change", (e) => { if (e.target !== all && e.target.name === all.dataset.checkAll) sync(); });
    sync();
  });

  // ---- Workflow designer: add, remove and reorder stage rows; same step = parallel ----
  document.querySelectorAll("form[data-workflow-designer]").forEach((form) => {
    const body = form.querySelector("[data-stage-rows] tbody");
    const preview = form.querySelector("[data-flow-preview]");
    const rows = () => [...body.querySelectorAll("[data-stage-row]")];
    const reindex = () => {
      rows().forEach((row, i) => row.querySelectorAll("[data-f]").forEach((el) => { el.name = `Input.Stages[${i}].${el.dataset.f}`; }));
      // A checklist only applies to a stage that needs a quality pass.
      rows().forEach((row) => {
        const qc = row.querySelector('[data-f="RequiresQualityPass"]');
        const tpl = row.querySelector('[data-f="QualityTemplateId"]');
        if (qc && tpl) tpl.hidden = !qc.checked;
      });
      if (!preview) return;
      const groups = new Map();
      rows().forEach((row) => {
        const name = row.querySelector('[data-f="Name"]').value.trim();
        const step = parseInt(row.querySelector('[data-f="Step"]').value, 10) || 1;
        if (!name) return;
        if (!groups.has(step)) groups.set(step, []);
        groups.get(step).push(name);
      });
      preview.replaceChildren(...[...groups.keys()].sort((a, b) => a - b).map((k) => {
        const li = document.createElement("li");
        groups.get(k).forEach((n) => { const s = document.createElement("span"); s.textContent = n; li.append(s); });
        if (groups.get(k).length > 1) li.dataset.parallel = preview.dataset.parallelText;
        return li;
      }));
    };
    const blank = (step) => {
      const row = rows()[rows().length - 1].cloneNode(true);
      row.querySelectorAll("input, select").forEach((el) => {
        if (el.type === "checkbox") el.checked = false;
        else if (el.dataset.f === "Step") el.value = step;
        else if (el.dataset.f === "TargetHours") el.value = "0";
        else el.value = "";
      });
      return row;
    };
    const maxStep = () => Math.max(0, ...rows().map((r) => parseInt(r.querySelector('[data-f="Step"]').value, 10) || 0));
    form.querySelectorAll("[data-row-add], [data-row-add-parallel], [data-row-up], [data-row-down], [data-row-remove]").forEach((b) => { b.hidden = false; });
    form.addEventListener("click", (e) => {
      const btn = e.target.closest("button");
      if (!btn) return;
      const row = btn.closest("[data-stage-row]");
      if (btn.matches("[data-row-add]")) { body.append(blank(maxStep() + 1)); }
      else if (btn.matches("[data-row-add-parallel]")) { body.append(blank(Math.max(1, maxStep()))); }
      else if (btn.matches("[data-row-remove]") && row) { if (rows().length > 1) row.remove(); }
      else if (btn.matches("[data-row-up]") && row?.previousElementSibling) { row.previousElementSibling.before(row); }
      else if (btn.matches("[data-row-down]") && row?.nextElementSibling) { row.nextElementSibling.after(row); }
      else return;
      reindex();
      form.dataset.dirty = "1";
    });
    form.addEventListener("input", reindex);
    form.addEventListener("change", reindex);
    reindex();
  });

  // ---- Simple row lists: <form data-row-list="Input.Items"> with [data-rows] > [data-row] holding [data-f] fields ----
  document.querySelectorAll("form[data-row-list]").forEach((form) => {
    const prefix = form.dataset.rowList;
    const body = form.querySelector("[data-rows]");
    const rows = () => [...body.querySelectorAll("[data-row]")];
    const reindex = () => rows().forEach((row, i) => {
      row.querySelectorAll("[data-f]").forEach((el) => { el.name = `${prefix}[${i}].${el.dataset.f}`; });
      const no = row.querySelector("[data-row-no]");
      if (no) no.textContent = i + 1;
    });
    form.querySelectorAll("[data-row-add], [data-row-up], [data-row-down], [data-row-remove]").forEach((b) => { b.hidden = false; });
    form.addEventListener("click", (e) => {
      const btn = e.target.closest("button");
      if (!btn) return;
      const row = btn.closest("[data-row]");
      if (btn.matches("[data-row-add]")) {
        const copy = rows()[rows().length - 1].cloneNode(true);
        copy.querySelectorAll("input, select, textarea").forEach((el) => { if (el.type === "checkbox") el.checked = false; else el.value = ""; });
        body.append(copy);
        copy.querySelector('input:not([type="hidden"])')?.focus();
      }
      else if (btn.matches("[data-row-remove]") && row) { if (rows().length > 1) row.remove(); }
      else if (btn.matches("[data-row-up]") && row?.previousElementSibling) { row.previousElementSibling.before(row); }
      else if (btn.matches("[data-row-down]") && row?.nextElementSibling) { row.nextElementSibling.after(row); }
      else return;
      reindex();
      form.dataset.dirty = "1";
    });
  });

  // ---- Conditional fields: <div data-show-when="Result=Rejected,AdjustmentRequested"> follows the checked radio or select ----
  const showWhen = [...document.querySelectorAll("[data-show-when]")];
  if (showWhen.length) {
    const current = (form, name) => {
      const el = form.querySelector(`[name="${name}"]:checked`) || form.querySelector(`select[name="${name}"]`);
      return el ? el.value : "";
    };
    const apply = () => showWhen.forEach((el) => {
      const [name, values] = el.dataset.showWhen.split("=");
      const form = el.closest("form") || document;
      const on = values.split(",").includes(current(form, name));
      el.hidden = !on;
      el.querySelectorAll("input, select, textarea").forEach((f) => { f.disabled = !on; });
    });
    document.addEventListener("change", apply);
    apply();
  }

  // ---- Inspection checklist: pass-all shortcut, live tally, and acceptance blocked by a failed critical item ----
  document.querySelectorAll("form[data-inspection]").forEach((form) => {
    const tally = form.querySelector("[data-tally]");
    const accept = form.querySelector('input[name="Result"][value="Accepted"]');
    const blocked = form.querySelector("[data-critical-blocked]");
    const update = () => {
      const items = [...form.querySelectorAll("[data-item]")];
      const answered = items.filter((i) => i.querySelector("input[type=radio]:checked")).length;
      const failed = items.filter((i) => i.querySelector('input[type=radio][value="false"]:checked'));
      const critical = failed.some((i) => i.hasAttribute("data-critical"));
      items.forEach((i) => i.classList.toggle("is-failed", !!i.querySelector('input[type=radio][value="false"]:checked')));
      if (tally) tally.textContent = tally.dataset.template.replace("{0}", answered).replace("{1}", items.length).replace("{2}", failed.length);
      if (accept) {
        accept.disabled = critical;
        if (critical && accept.checked) { accept.checked = false; }
      }
      if (blocked) blocked.hidden = !critical;
    };
    form.querySelector("[data-pass-all]")?.addEventListener("click", () => {
      form.querySelectorAll('[data-item] input[type=radio][value="true"]').forEach((r) => { r.checked = true; });
      update();
      form.dispatchEvent(new Event("change", { bubbles: true }));
    });
    form.querySelector("[data-pass-all]")?.removeAttribute("hidden");
    form.addEventListener("change", update);
    update();
  });

  // ---- Online / offline indicator ----
  const setOnline = () => root.classList.toggle("is-offline", !navigator.onLine);
  addEventListener("online", setOnline);
  addEventListener("offline", setOnline);
  setOnline();
})();
